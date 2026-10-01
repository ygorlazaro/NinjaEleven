using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The rules a manager can break are decided by the service, not by the client: the
/// starting eleven, the five substitutions and the recovery of a match whose working
/// memory was lost. These tests drive the real service over mocked repositories.
/// </summary>
public class MatchServiceTests
{
    private const int SquadSize = 11;
    private const int BenchSize = 7;
    private const int MaxSubstitutions = 5;

    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<ICupTieRepository> _cupTies = new(MockBehavior.Loose);
    private readonly Mock<ITrophyRepository> _trophies = new(MockBehavior.Loose);
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<ISponsorRepository> _sponsors = new(MockBehavior.Loose);
    private readonly Mock<ISponsorContractRepository> _sponsorContracts = new(MockBehavior.Loose);
    private readonly List<FinanceMovement> _book = [];
    private readonly MatchDay _matchDay;
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ITeamMatchPlanRepository> _plans = new(MockBehavior.Loose);
    private readonly MatchSessionRegistry _sessions = new();

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly CompetitionSeason _competitionSeason;
    private readonly Round _round;
    private readonly Team _home;
    private readonly Team _away;
    private readonly Fixture _fixture;
    private readonly List<Player> _roster = [];
    private readonly List<PlayerSeasonState> _states = [];
    private readonly List<Match> _started = [];

    public MatchServiceTests()
    {
        _competitionSeason = CompetitionSeason.Create(Guid.NewGuid(), _seasonId);
        _round = Round.Create(_competitionSeason.Id, 1);
        _matchDay = MatchDay.Create(_seasonId, 1, new DateOnly(2026, 3, 1));
        _round.ScheduleOn(_matchDay.Id);
        _matchDays.Setup(repo => repo.GetAsync(_matchDay.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_matchDay);
        _home = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 80);
        _away = Team.Create("Estrela do Norte", "EDN", "#0F5132", "#FFD700", 70);
        _fixture = Fixture.Create(_round.Id, _home.Id, _away.Id);

        AddSquad(_home, 90);
        AddSquad(_away, 80);

        // A second, strong goalkeeper: a keeper is rated highly for being a keeper, so a
        // plain rating sort would put him in the eleven next to the first one. That is the
        // case the one-goalkeeper rule has to survive.
        var reserve = Player.Create(
            $"{_home.ShortName} Reserva",
            30,
            Position.GK,
            speed: 12,
            accuracy: 12,
            dribbling: 12,
            heading: 12,
            strength: 12,
            goalkeeperPower: 18,
            reflexes: 17);
        _roster.Add(reserve);
        _states.Add(PlayerSeasonState.Create(reserve.Id, _seasonId, _home.Id, 60));

        // Depth on both clubs, so a club has more outfield candidates than the eleven
        // needs and the choice of who is left out is really a decision.
        foreach (var team in new[] { _home, _away })
        {
            for (var extra = 0; extra < 3; extra++)
            {
                var player = Player.Create(
                    $"{team.ShortName} Extra {extra}",
                    28,
                    Position.MID,
                    speed: 11,
                    accuracy: 11,
                    dribbling: 11,
                    heading: 11,
                    strength: 11,
                    goalkeeperPower: 0,
                    reflexes: 0);

                _roster.Add(player);
                _states.Add(PlayerSeasonState.Create(player.Id, _seasonId, team.Id, 75));
            }
        }

        _teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Team?>(
                id == _home.Id ? _home : id == _away.Id ? _away : null));
        // Who is on each club's books, which is what the wage bill is worked out from: a
        // season state without a membership is a player on nobody's contract, and the club
        // that has to pay for him is not a club at all. The membership carries a wage because
        // that is where the wage lives: a salary worked out from the man on the day of the
        // payday would be a second number, and a test about the books should be reading the
        // one the club actually agreed.
        _teams.Setup(repo => repo.GetSquadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, Guid _, CancellationToken __) => Task.FromResult<IReadOnlyList<TeamMembership>>(
                _states
                    .Where(state => state.TeamId == teamId)
                    .Select(state => TeamMembership.Create(
                        state.PlayerId,
                        teamId,
                        new DateOnly(2026, 1, 1),
                        wage: WagesOf(state)))
                    .ToList()));
        // The same contracts read as the ones a club holds right now, which is what a match
        // reads to put a number on a back. A squad is a season's book; a live contract is the
        // deal that has not been called off, and they are not the same list for a player who
        // arrived after the season opened.
        _teams.Setup(repo => repo.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, CancellationToken __) => Task.FromResult<IReadOnlyList<TeamMembership>>(
                _states
                    .Where(state => state.TeamId == teamId)
                    .Select(state => TeamMembership.Create(
                        state.PlayerId,
                        teamId,
                        new DateOnly(2026, 1, 1),
                        wage: WagesOf(state)))
                    .ToList()));
        // The same book read the way a table reads it: every club's squad in one go, and the
        // men behind them in another. A table that asked club by club would be a table that
        // times out, so the batched readers are what the tests hold it to.
        _teams.Setup(repo => repo.GetSquadsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> teamIds, Guid _, CancellationToken __) =>
                Task.FromResult<Dictionary<Guid, IReadOnlyList<TeamMembership>>>(
                    teamIds.ToDictionary(
                        teamId => teamId,
                        teamId => (IReadOnlyList<TeamMembership>)_states
                            .Where(state => state.TeamId == teamId)
                            .Select(state => TeamMembership.Create(state.PlayerId, teamId, new DateOnly(2026, 1, 1)))
                            .ToList())));
        _teams.Setup(repo => repo.GetPlayersAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> playerIds, CancellationToken __) =>
                Task.FromResult<Dictionary<Guid, Player>>(
                    _roster.Where(player => playerIds.Contains(player.Id))
                        .ToDictionary(player => player.Id)));
        // A book that remembers what was written in it, because the two rules of a ledger
        // are the two questions it has to answer honestly: what the balance is now, and
        // whether a line that must happen once has already happened.
        _finance.Setup(repo => repo.AddAsync(It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Returns((FinanceMovement movement, CancellationToken _) =>
            {
                _book.Add(movement);
                return Task.CompletedTask;
            });
        _finance.Setup(repo => repo.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, CancellationToken _) => Task.FromResult<FinanceMovement?>(
                _book.LastOrDefault(movement => movement.TeamId == teamId)));
        _finance.Setup(repo => repo.ExistsInSeasonAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, Guid seasonId, FinanceMovementKind kind, CancellationToken _) =>
                Task.FromResult(_book.Any(movement =>
                    movement.TeamId == teamId && movement.SeasonId == seasonId && movement.Kind == kind)));
        // The guard that makes a line happen once per match, and once per match *of that
        // kind*: a match writes a gate line and a wage line, and a book that asked only about
        // the match would answer for the wages as well and refuse to pay them.
        _finance.Setup(repo => repo.ExistsForMatchAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, Guid matchId, FinanceMovementKind kind, CancellationToken _) =>
                Task.FromResult(_book.Any(movement =>
                    movement.TeamId == teamId && movement.MatchId == matchId && movement.Kind == kind)));
        _players.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Player>>(_roster));
        _players.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Player?>(
                _roster.FirstOrDefault(player => player.Id == id)));
        _players.Setup(repo => repo.ListSeasonStatesAsync(_seasonId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid? teamId, CancellationToken __) => Task.FromResult<IReadOnlyList<PlayerSeasonState>>(
                _states.Where(state => teamId is null || state.TeamId == teamId).ToList()));
        _fixtures.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Fixture?>(_fixture));
        _fixtures.Setup(repo => repo.ListByRoundAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Fixture>>([_fixture]));
        _rounds.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Round?>(_round));
        // The matchday is the unit of football: the windows of the day and their fixtures are
        // what a match start asks about, so a stub that answers "there is one round and one
        // fixture in it" is the world these tests are played in.
        _rounds.Setup(repo => repo.ListByMatchDayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Round>>([_round]));
        _fixtures.Setup(repo => repo.ListByRoundIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Fixture>>([_fixture]));
        _competitions.Setup(repo => repo.GetSeasonByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<CompetitionSeason?>(_competitionSeason));
        _players.Setup(repo => repo.GetSeasonStateForUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid playerId, Guid _, CancellationToken __) => Task.FromResult<PlayerSeasonState?>(
                _states.FirstOrDefault(state => state.PlayerId == playerId)));
        _matches.Setup(repo => repo.GetByFixtureAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid fixtureId, CancellationToken _) => Task.FromResult<Match?>(
                _started.LastOrDefault(match => match.FixtureId == fixtureId)));
        _matches.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Match?>(
                _started.LastOrDefault(match => match.Id == id)));
        _matches.Setup(repo => repo.AddAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns((Match match, CancellationToken _) =>
            {
                _started.Add(match);
                return Task.CompletedTask;
            });
        _matches.Setup(repo => repo.AddEventsAsync(It.IsAny<IEnumerable<MatchEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// The crowd factory a match starts with.
    ///
    /// These tests are about the match, not about a stadium, and none of them builds a season's
    /// worth of division behind the fixture. The factory answers with its neutral context for
    /// a fixture it can find no competition for, which is the honest answer and the reason the
    /// fallback exists at all.
    /// </summary>
    private AttendanceContextFactory CreateAttendanceContextFactory() => new(
        _rounds.Object,
        _fixtures.Object,
        _competitions.Object,
        new StandingsService(
            _rounds.Object,
            _fixtures.Object,
            _matches.Object,
            _competitions.Object,
            _teams.Object));

    private MatchService CreateService()
    {
        // Two services want each other here — the match reads the standing order off the
        // board, and the board reads the calendar off the matchday — so the shared one is
        // built once and handed to both rather than written out twice and left to drift.
        var matchday = CreateMatchdayService();

        return new MatchService(
        _matches.Object,
        _teams.Object,
        _players.Object,
        _fixtures.Object,
        _rounds.Object,
        _competitions.Object,
        _sessions,
        CreateAttendanceContextFactory(),
        _unitOfWork.Object,
        new CupProgressionService(
            _cupTies.Object,
            _trophies.Object,
            _matches.Object,
            _rounds.Object,
            _fixtures.Object,
            _matchDays.Object,
            _competitions.Object,
            _seasons.Object,
            _unitOfWork.Object,
            new FinanceService(
                _finance.Object,
                _teams.Object,
                _players.Object,
                _fixtures.Object,
                _rounds.Object,
                _matchDays.Object,
                _seasons.Object,
                InboxTestFactory.Create(_teams),
                _unitOfWork.Object,
                NullLogger<FinanceService>.Instance),
            InboxTestFactory.Create(_teams),
            _teams.Object,
            new Random()),
        matchday,
        CreateTacticsService(matchday),
        new FinanceService(
            _finance.Object,
            _teams.Object,
            _players.Object,
            _fixtures.Object,
            _rounds.Object,
            _matchDays.Object,
            _seasons.Object,
            InboxTestFactory.Create(_teams),
            _unitOfWork.Object,
            NullLogger<FinanceService>.Instance),
        new SponsorOfferService(
            _sponsors.Object,
            _sponsorContracts.Object,
            _teams.Object,
            _finance.Object,
            _seasons.Object,
            InboxTestFactory.Create(_teams),
            _unitOfWork.Object,
            NullLogger<SponsorOfferService>.Instance),
        InboxTestFactory.Create(_teams),
        new MatchContextService(
            _matches.Object,
            _fixtures.Object,
            _rounds.Object,
            _competitions.Object,
            _matchDays.Object,
            _seasons.Object,
            _cupTies.Object,
            _teams.Object),
        CreateStandingsService(),
        MatchTestContext.Host,
        MatchTestContext.World(),
        MatchTestContext.Clock,
        NullLogger<MatchService>.Instance);
    }

    /// <summary>
    /// The real standings service over the same loose mocks, so a test that finishes a match
    /// reaches the table the report reads without standing up a division behind it. The
    /// competition repository is loose, so the table comes back empty and the report is
    /// written without one — which is the same answer a manager would get from a Supercup.
    /// </summary>
    private StandingsService CreateStandingsService() => new(
        _rounds.Object,
        _fixtures.Object,
        _matches.Object,
        _competitions.Object,
        _teams.Object);

    private MatchdayService CreateMatchdayService() => new(
        _matchDays.Object,
        _rounds.Object,
        _fixtures.Object,
        _competitions.Object,
        _matches.Object,
        new ScorerPrizeService(
            _competitions.Object,
            _players.Object,
            _teams.Object,
            CreateFinance(),
            InboxTestFactory.Create(_teams),
            NullLogger<ScorerPrizeService>.Instance),
        new TransferService(
            Mock.Of<ITransferRepository>(),
            _teams.Object,
            _players.Object,
            _seasons.Object,
            _competitions.Object,
            _rounds.Object,
            _matches.Object,
            _finance.Object,
            InboxTestFactory.Create(_teams),
            new ManagedClubs(),
            _unitOfWork.Object,
            NullLogger<TransferService>.Instance),
        _unitOfWork.Object,
        NullLogger<MatchdayService>.Instance);

    private TacticsService CreateTacticsService(MatchdayService matchday) => new(
        _plans.Object,
        _teams.Object,
        _fixtures.Object,
        _matches.Object,
        new TeamService(
            _teams.Object,
            _players.Object,
            _seasons.Object,
            _matches.Object,
            _unitOfWork.Object),
        matchday,
        _unitOfWork.Object,
        MatchTestContext.Clock);

    private FinanceService CreateFinance() => new(
        _finance.Object,
        _teams.Object,
        _players.Object,
        _fixtures.Object,
        _rounds.Object,
        _matchDays.Object,
        _seasons.Object,
        InboxTestFactory.Create(_teams),
        _unitOfWork.Object,
        NullLogger<FinanceService>.Instance);

    private void AddSquad(Team team, int energy)
    {
        var positions = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.DEF, Position.MID,
            Position.MID, Position.MID, Position.ATT, Position.ATT, Position.ATT, Position.ATT
        };

        foreach (var position in positions)
        {
            var player = Player.Create(
                $"{team.ShortName} Player {_roster.Count}",
                29,
                position,
                speed: 12,
                accuracy: 12,
                dribbling: 12,
                heading: 12,
                strength: 12,
                goalkeeperPower: position == Position.GK ? 18 : 0,
                reflexes: position == Position.GK ? 16 : 0);

            _roster.Add(player);
            _states.Add(PlayerSeasonState.Create(player.Id, _seasonId, team.Id, energy));
        }

        // The bench the manager can bring on.
        for (var i = 0; i < BenchSize; i++)
        {
            var player = Player.Create(
                $"{team.ShortName} Bench {_roster.Count}",
                30,
                Position.MID,
                speed: 10,
                accuracy: 10,
                dribbling: 10,
                heading: 10,
                strength: 10,
                goalkeeperPower: 0,
                reflexes: 0);

            _roster.Add(player);
            _states.Add(PlayerSeasonState.Create(player.Id, _seasonId, team.Id, energy));
        }
    }

    private List<Guid> AwaySquadIds() =>
        _states.Where(state => state.TeamId == _away.Id).Select(state => state.PlayerId).ToList();

    private List<Guid> HomeSquadIds() =>
        _states.Where(state => state.TeamId == _home.Id).Select(state => state.PlayerId).ToList();

    /// <summary>
    /// The wage a mock contract is signed on, read off the man as the rules would price him.
    ///
    /// It is deliberately the real formula rather than a round number: a test that paid a club
    /// a made-up wage would pass whether or not the bill were read from the contract, and the
    /// whole point of the wage living on the contract is that a bill can be checked against it.
    /// </summary>
    private decimal WagesOf(PlayerSeasonState state) =>
        PlayerValuation.SeasonWage(
            _roster.Single(player => player.Id == state.PlayerId),
            state);

    private Guid HomeKeeperId() =>
        _states.Single(state =>
            state.TeamId == _home.Id
            && _roster.Single(player => player.Id == state.PlayerId).Position == Position.GK
            && !_roster.Single(player => player.Id == state.PlayerId).Name.EndsWith("Reserva")).PlayerId;

    private static async Task<Domain.Common.DomainValidationException> ThrowsDomainAsync(Func<Task> action) =>
        await Assert.ThrowsAsync<Domain.Common.DomainValidationException>(action);

    [Fact]
    public async Task Start_uses_the_eleven_the_manager_chose()
    {
        var chosen = HomeSquadIds().Take(SquadSize).ToList();

        var result = await CreateService().StartAsync(_fixture.Id, 7, _home.Id, chosen);

        Assert.True(result.Accepted);
        Assert.NotEqual(Guid.Empty, result.MatchId);
        Assert.True(_sessions.TryGet(result.MatchId, out var session));
        Assert.Equal(chosen.OrderBy(id => id), session!.State.HomeLineup.Select(p => p.PlayerId).OrderBy(id => id));
        Assert.Equal(BenchSize, session.State.HomeBench.Count);
        Assert.Equal(FixtureStatus.InProgress, _fixture.Status);
    }

    [Fact]
    public async Task A_saved_plan_answered_against_a_thinner_squad_still_goes_out_in_its_own_shape()
    {
        // The bug this holds down: a plan of 4-3-3 whose striker and left-back were unavailable
        // came out with two goalkeepers and two strikers missing. The gaps were filled from the
        // head of the staff's eleven, and that list is ordered with the keeper first, so the
        // first replacement was always a second keeper — and the shape the engine measures is
        // read off the eleven, so a saved tactic arrived at the pitch as somebody else's eleven.
        foreach (var position in new[] { Position.DEF, Position.ATT })
        {
            for (var extra = 0; extra < 2; extra++)
            {
                var player = Player.Create(
                    $"{_home.ShortName} Cover {position} {extra}",
                    27,
                    position,
                    speed: 11,
                    accuracy: 11,
                    dribbling: 11,
                    heading: 11,
                    strength: 11,
                    goalkeeperPower: 0,
                    reflexes: 0);

                _roster.Add(player);
                _states.Add(PlayerSeasonState.Create(player.Id, _seasonId, _home.Id, 90));
            }
        }

        var plan = HomeIdsByPosition(Position.GK).Take(1)
            .Concat(HomeIdsByPosition(Position.DEF).Take(4))
            .Concat(HomeIdsByPosition(Position.MID).Take(3))
            .Concat(HomeIdsByPosition(Position.ATT).Take(3))
            .ToList();

        Assert.Equal(SquadSize, plan.Count);

        _plans
            .Setup(repo => repo.GetAsync(_home.Id, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamMatchPlan.Create(_home.Id, _seasonId, "433", plan, [], MatchTestContext.Clock.UtcNow));

        // Two of the men he named cannot play today: the world moved under the plan, which is
        // exactly the case the plan exists to survive. One is a defender and one a striker,
        // because a gap in one line is a gap in that line and not in the eleven at large.
        _stateOf(plan[1]).AddInjury(Injury.Grave, 3);
        _stateOf(plan[10]).AddInjury(Injury.Grave, 3);

        var result = await CreateService().StartAsync(_fixture.Id, 7);

        Assert.True(result.Accepted);
        Assert.True(_sessions.TryGet(result.MatchId, out var session));

        var eleven = session!.State.HomeLineup;

        // One keeper, and the shape the manager saved rather than the shape two spare men
        // happen to add up to.
        Assert.Equal(SquadSize, eleven.Count);
        Assert.Single(eleven.Where(player => player.Position == Position.GK));
        Assert.Equal("4-3-3", Formation.FromComposition(eleven).ToString());

        // Every man he named who can play is still there, and the two who cannot are the
        // only two names that changed.
        Assert.DoesNotContain(eleven, player => player.PlayerId == plan[1]);
        Assert.DoesNotContain(eleven, player => player.PlayerId == plan[10]);
        Assert.Equal(SquadSize - 2, plan.Count(id => eleven.Any(player => player.PlayerId == id)));

        // The replacements came from the lines the gaps were in, which is what "the shape
        // survived" means in names rather than in arithmetic.
        Assert.Equal(4, eleven.Count(player => player.Position == Position.DEF));
        Assert.Equal(3, eleven.Count(player => player.Position == Position.MID));
        Assert.Equal(3, eleven.Count(player => player.Position == Position.ATT));
    }

    private PlayerSeasonState _stateOf(Guid playerId) =>
        _states.Single(state => state.PlayerId == playerId);

    private List<Guid> HomeIdsByPosition(Position position) =>
        _states
            .Where(state => state.TeamId == _home.Id
                && _roster.Single(player => player.Id == state.PlayerId).Position == position)
            .Select(state => state.PlayerId)
            .ToList();

    [Fact]
    public async Task Start_refuses_an_eleven_without_a_goalkeeper()
    {
        var keeper = HomeKeeperId();
        var withoutKeeper = HomeSquadIds().Where(id => id != keeper).Take(SquadSize).ToList();

        var error = await ThrowsDomainAsync(() => CreateService().StartAsync(_fixture.Id, 7, _home.Id, withoutKeeper));

        Assert.Equal("GoalkeeperRequired", error.Code);
    }

    [Fact]
    public async Task A_club_picks_exactly_one_goalkeeper_for_a_match_it_watches_no_lineup_of()
    {
        // The opponent's eleven is chosen by the service, so the one-goalkeeper rule has
        // to hold there too: a club with three keepers must not field two of them, which
        // is what a pure rating sort would do.
        var result = await CreateService().StartAsync(_fixture.Id, 7);

        Assert.True(result.Accepted);
        Assert.True(_sessions.TryGet(result.MatchId, out var session));

        foreach (var lineup in new[] { session!.State.HomeLineup, session.State.AwayLineup })
        {
            Assert.Equal(SquadSize, lineup.Count);
            Assert.Single(lineup.Where(player => player.Position == Position.GK));
        }
    }

    [Fact]
    public async Task Start_refuses_two_goalkeepers()
    {
        var keeper = HomeKeeperId();
        var reserve = _states.Single(state => _roster.Single(player => player.Id == state.PlayerId).Name.EndsWith("Reserva")).PlayerId;
        var withTwoKeepers = HomeSquadIds()
            .Where(id => id != keeper)
            .Take(SquadSize - 2)
            .Append(keeper)
            .Append(reserve)
            .ToList();

        var error = await ThrowsDomainAsync(() => CreateService().StartAsync(_fixture.Id, 7, _home.Id, withTwoKeepers));

        Assert.Equal("GoalkeeperRequired", error.Code);
    }

    [Fact]
    public async Task Start_refuses_an_eleven_that_is_not_exactly_eleven_players()
    {
        var tooFew = HomeSquadIds().Take(SquadSize - 1).ToList();

        var error = await ThrowsDomainAsync(() => CreateService().StartAsync(_fixture.Id, 7, _home.Id, tooFew));

        Assert.Equal("InvalidLineup", error.Code);
    }

    [Fact]
    public async Task Start_refuses_a_player_who_is_not_available()
    {
        var chosen = HomeSquadIds().Take(SquadSize).ToList();
        chosen[3] = Guid.NewGuid();

        var error = await ThrowsDomainAsync(() => CreateService().StartAsync(_fixture.Id, 7, _home.Id, chosen));

        Assert.Equal("PlayerNotAvailable", error.Code);
    }

    [Fact]
    public async Task Start_refuses_a_team_that_is_not_playing_the_fixture()
    {
        var chosen = HomeSquadIds().Take(SquadSize).ToList();

        var error = await ThrowsDomainAsync(() => CreateService().StartAsync(_fixture.Id, 7, Guid.NewGuid(), chosen));

        Assert.Equal("TeamNotInMatch", error.Code);
    }

    [Fact]
    public async Task Start_of_a_live_fixture_joins_the_running_match_instead_of_creating_a_second_one()
    {
        var service = CreateService();
        var chosen = HomeSquadIds().Take(SquadSize).ToList();

        var first = await service.StartAsync(_fixture.Id, 7, _home.Id, chosen);
        var second = await service.StartAsync(_fixture.Id, 7, _home.Id, chosen);

        Assert.Equal(first.MatchId, second.MatchId);
        Assert.True(second.Accepted);
    }

    [Fact]
    public async Task A_manager_who_claims_the_match_the_world_left_him_gets_the_clock_back()
    {
        // The world opens the manager's own match and stops, and the background loop is told
        // to keep its hands off it so the match is still at minute zero when he arrives. The
        // claim is what hands the clock back: a loop that went on leaving it alone would show
        // a manager a match that never moves while he watches it.
        var service = CreateService();

        var started = await service.StartAsync(_fixture.Id, 7, headless: true);
        Assert.True(_sessions.TryGet(started.MatchId, out var left));
        left.Driver = MatchDriver.LeftForTheManager;

        Assert.True(service.AttachManager(started.MatchId, _home.Id));

        Assert.True(_sessions.TryGet(started.MatchId, out var claimed));
        Assert.Equal(MatchDriver.None, claimed.Driver);
        Assert.Equal(_home.Id, claimed.State.ManagerTeamId);
        Assert.False(claimed.AutoContinue);
    }

    [Fact]
    public async Task A_match_left_on_the_touchline_and_never_touched_is_given_back_to_the_world()
    {
        // The world leaves the manager's own match open and waits. If he never comes, that
        // match belongs to nobody: it is still at minute zero, and a world waiting for ever
        // for a manager who is not coming is a world that never plays another day.
        var service = CreateService();

        var started = await service.StartAsync(_fixture.Id, 7, headless: true);
        Assert.True(_sessions.TryGet(started.MatchId, out var left));
        left.Driver = MatchDriver.LeftForTheManager;

        Assert.True(await service.ReleaseTheUntouchedMatchAsync(_fixture.Id));
        Assert.Equal(FixtureStatus.Scheduled, _fixture.Status);
        Assert.False(_sessions.TryGet(started.MatchId, out _));
    }

    [Fact]
    public async Task A_match_the_loop_is_driving_is_nobody_untouched()
    {
        // The other side of the same question: a match that is not waiting for a manager is
        // somebody's match, whatever minute it is on. Releasing it would take a game away
        // from whoever is playing it, which is the mistake this whole rule exists to stop.
        var service = CreateService();

        await service.StartAsync(_fixture.Id, 7, headless: true);

        Assert.False(await service.ReleaseTheUntouchedMatchAsync(_fixture.Id));
        Assert.Equal(FixtureStatus.InProgress, _fixture.Status);
    }

    [Fact]
    public async Task Start_abandons_a_match_whose_session_was_lost_and_reopens_the_fixture()
    {
        var service = CreateService();
        var chosen = HomeSquadIds().Take(SquadSize).ToList();
        var orphan = Match.Create(_fixture.Id, _home.Id, _away.Id);
        orphan.KickOff(11, null, default);
        orphan.StartFirstHalf();
        orphan.ApplyEngineState(20, 1, 0, 6);
        _fixture.MarkInProgress();
        _started.Add(orphan);

        _players.Setup(repo => repo.GetSeasonStateForUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid playerId, Guid _, CancellationToken __) => Task.FromResult<PlayerSeasonState?>(
                _states.FirstOrDefault(state => state.PlayerId == playerId)));
        _matches.Setup(repo => repo.GetByFixtureAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Match?>(orphan));

        var result = await service.StartAsync(_fixture.Id, 7, _home.Id, chosen);

        Assert.True(result.Accepted);
        Assert.NotEqual(orphan.Id, result.MatchId);
        Assert.True(orphan.IsFinished, "the orphan has to be closed so history keeps a single row");
        _matches.Verify(repo => repo.Update(orphan), Times.Once);
    }

    [Fact]
    public async Task Giving_up_on_a_match_closes_it_and_reopens_its_fixture()
    {
        // The case a headless run creates: this process kicked the match off and then could
        // not play it to the end. Left as it is, the match stays on the pitch, its session
        // stays in this process's registry, and the next run of the window is told the match is
        // already being played — by a driver that stopped.
        var service = CreateService();
        var matchId = await StartAsync(service);

        var given = await service.AbandonAsync(matchId);

        Assert.True(given);
        Assert.True(_started.Single(m => m.Id == matchId).IsFinished);
        Assert.Equal(MatchStatus.Abandoned, _started.Single(m => m.Id == matchId).Status);
        Assert.Equal(FixtureStatus.Scheduled, _fixture.Status);
        Assert.False(_sessions.TryGet(matchId, out _), "the session is what refused the next run");
    }

    [Fact]
    public async Task Giving_up_on_a_match_that_has_been_decided_changes_nothing()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        _started.Single(match => match.Id == matchId).Finish();

        Assert.False(await service.AbandonAsync(matchId));
    }

    [Fact]
    public async Task A_club_playing_in_this_process_is_told_which_match()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var live = await service.GetLiveMatchForTeamAsync(_home.Id);

        Assert.NotNull(live);
        Assert.Equal(matchId, live!.MatchId);
        Assert.True(live.IsHome);
        Assert.Equal("Clube Aurora", live.HomeTeamName);
        Assert.Equal("Estrela do Norte", live.AwayTeamName);
    }

    [Fact]
    public async Task A_club_playing_in_another_process_is_still_told_which_match()
    {
        // The Scheduler plays the matches nobody is watching and this process holds the
        // sockets, so the working memory of a live match is in the other one. A badge answered
        // from this registry alone would go quiet at exactly the moment there is a match to
        // watch, and the manager would learn his club was playing from the fixture list.
        var service = CreateService();
        var theirs = Match.Create(_fixture.Id, _home.Id, _away.Id);
        theirs.KickOff(11, null, new AttendanceContext(
            Tier: 1,
            HomePosition: 1,
            ClubsInDivision: 16,
            HomeSquadStars: 4,
            AwaySquadStars: 4,
            DivisionAverageStars: 1,
            Matchday: 1,
            TotalMatchdays: 30,
            Importance: MatchImportance.Normal));
        theirs.StartFirstHalf();

        _matches.Setup(repo => repo.ListLiveExceptHostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveMatchRow> { new(theirs, Guid.NewGuid()) });

        _teams.Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { _home, _away });

        var live = await service.GetLiveMatchForTeamAsync(_away.Id);

        Assert.NotNull(live);
        Assert.Equal(theirs.Id, live!.MatchId);
        Assert.False(live.IsHome);
        Assert.Equal("Clube Aurora", live.HomeTeamName);
        Assert.Equal("Estrela do Norte", live.AwayTeamName);
    }

    [Fact]
    public async Task A_club_that_is_not_playing_anything_is_told_nothing()
    {
        _matches.Setup(repo => repo.ListLiveExceptHostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveMatchRow>());

        Assert.Null(await CreateService().GetLiveMatchForTeamAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_match_in_this_process_is_answered_from_here_and_not_from_the_rows()
    {
        // The registry is the working memory, and it is the truth about a match this process
        // is driving: the row is only rewritten on the snapshot cadence, so a match that
        // kicked off a minute ago is a row that still says minute zero.
        var service = CreateService();
        await StartAsync(service);

        _matches.Verify(
            repo => repo.ListLiveExceptHostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Giving_up_on_a_match_that_does_not_exist_changes_nothing()
    {
        var service = CreateService();

        Assert.False(await service.AbandonAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Recovering_an_interrupted_match_drops_its_session_as_well()
    {
        // The path a restart takes, and the one that used to leak: the recovery closes the
        // match and reopens the fixture, and a match left in the registry is a match the next
        // run of the fixture is refused at the door — "already being played here", said by a
        // process that is playing nothing. A window of a matchday that hit this could never
        // close itself again.
        var service = CreateService();
        var matchId = await StartAsync(service);
        Assert.True(_sessions.TryGet(matchId, out _));

        _matches.Setup(repo => repo.ListUnfinishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match> { _started.Single(match => match.Id == matchId) });

        // The recovery also asks the matchday to close the windows that were played and never
        // closed, so the calendar is not a day behind its own results.
        _rounds.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round>());

        await service.RecoverInterruptedMatchesAsync();

        Assert.False(_sessions.TryGet(matchId, out _), "the session is what refused the next run");
        Assert.Equal(FixtureStatus.Scheduled, _fixture.Status);
    }

    [Fact]
    public async Task A_process_on_its_way_out_hands_back_the_match_it_was_playing()
    {
        // A restart is a thing that happens to a manager, not only to a process: the match he
        // is watching is the one that stops. Without this the row keeps the last heartbeat of
        // a process that is gone, and the next process waits out a five-minute lease over a
        // match nobody is playing — a frozen scoreboard charged to whoever was watching it.
        var service = CreateService();
        var matchId = await StartAsync(service);
        var started = _started.Single(match => match.Id == matchId);
        started.ClaimSession(MatchTestContext.Host.HostId, DateTimeOffset.UtcNow);

        _matches.Setup(repo => repo.ListUnfinishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match> { started });

        var released = await service.ReleaseTheMatchesOfThisHostAsync();

        Assert.Equal(1, released);
        Assert.Null(started.SessionHeartbeatAt);
        Assert.True(
            started.CanBeReclaimedBy("outro-processo", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)),
            "a match nobody is playing may be taken at once, rather than after a lease nobody is renewing");

        // The football that was played stays played. A handover is not a defeat.
        Assert.Equal(MatchStatus.InProgress, started.Status);
    }

    [Fact]
    public async Task A_sweep_leaves_alone_the_match_this_process_is_playing()
    {
        // The sweep that runs every thirty seconds is not the rescue that runs at a restart,
        // and the difference is a match on the right now. A lease is a claim on a match and a
        // process always holds the lease on its own, so a sweep that asked only about the
        // lease closed every match the loop was driving — the manager's own match, abandoned
        // at the minute he was watching it.
        var service = CreateService();
        var matchId = await StartAsync(service);

        _matches.Setup(repo => repo.ListUnfinishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match> { _started.Single(match => match.Id == matchId) });

        _rounds.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round>());

        var abandoned = await service.RecoverInterruptedMatchesAsync(
            CancellationToken.None,
            leaveRunningMatchesAlone: true);

        Assert.Empty(abandoned);
        Assert.True(_sessions.TryGet(matchId, out _), "the match on the pitch is not a corpse");
        Assert.NotEqual(MatchStatus.Abandoned, _started.Single(match => match.Id == matchId).Status);
    }

    [Fact]
    public async Task A_match_left_running_on_a_decided_fixture_is_abandoned_and_the_fixture_stays_decided()
    {
        // A live match holds its fixture: the database refuses a second one while the first is
        // running, and no walk ever asks about a fixture the world has already settled. So a
        // match left on the pitch of a decided fixture is a claim on a fixture that can never
        // be played again, held by a match nobody is driving.
        var service = CreateService();
        var matchId = await StartAsync(service);
        _fixture.MarkFinished();

        _matches.Setup(repo => repo.ListUnfinishedOnFinishedFixturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<Match> { _started.Single(match => match.Id == matchId) });

        Assert.Equal(1, await service.AbandonMatchesOnDecidedFixturesAsync());

        // Abandoned and not finished: the match never reached full time, and a row recording
        // a score that was never played is a lie in the results table.
        Assert.Equal(MatchStatus.Abandoned, _started.Single(match => match.Id == matchId).Status);

        // The fixture is not reopened. It was decided, the result is in the table, and
        // reopening it would have the world play the same match twice.
        Assert.Equal(FixtureStatus.Finished, _fixture.Status);
        Assert.False(_sessions.TryGet(matchId, out _));
    }

    [Fact]
    public async Task A_window_with_no_match_holding_a_decided_fixture_is_not_swept()
    {
        _matches.Setup(repo => repo.ListUnfinishedOnFinishedFixturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match>());

        Assert.Equal(0, await CreateService().AbandonMatchesOnDecidedFixturesAsync());
    }

    [Fact]
    public async Task A_fixture_closed_over_a_matchday_nobody_finished_goes_back_on_the_schedule()
    {
        // The bug this holds down. A fixture's own column is a claim about a match, and three
        // readers believed it without checking: the window that closes itself, the claim that
        // completes it, and the orphan sweep. A fixture that reached "finished" with nothing but
        // an abandoned match under it was therefore closed over, completed, and read as
        // settled — while the table above it was a game short, for the rest of the season. Six
        // fixtures of a second division sat in that state and eleven of its sixteen clubs
        // finished the campaign short a match each.
        var service = CreateService();
        var lost = Fixture.Create(_round.Id, _home.Id, _away.Id);
        lost.MarkFinished();

        _fixtures.Setup(repo => repo.ListFinishedWithoutAFinishedMatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Fixture> { lost });

        Assert.Equal(1, await service.ReopenTheFixturesNobodyDecidedAsync());

        // Back on the schedule, which is the whole repair: the claim reads the fixtures rather
        // than its own columns, so a window with a fixture owed is a window the world walks
        // again.
        Assert.Equal(FixtureStatus.Scheduled, lost.Status);
    }

    [Fact]
    public async Task A_fixture_whose_match_reached_the_final_whistle_is_left_decided()
    {
        // The other half of the same rule, and the one that would be expensive to get wrong.
        // Reopening a fixture that really was played puts a finished matchday back on the
        // schedule and has the world play all thirty-four of them twice — so the repair is
        // asked of the matches rather than of the column, and a fixture with a result behind
        // it is never in the answer.
        var service = CreateService();
        _fixture.MarkFinished();

        _fixtures.Setup(repo => repo.ListFinishedWithoutAFinishedMatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Fixture>());

        Assert.Equal(0, await service.ReopenTheFixturesNobodyDecidedAsync());
        Assert.Equal(FixtureStatus.Finished, _fixture.Status);
    }

    [Fact]
    public async Task A_kick_off_that_takes_a_match_over_starts_a_new_one_and_leaves_the_old_match_closed()
    {
        // The other caller of the same close. A match whose working memory this process no
        // longer has is a match it is entitled to take over, and taking it over means closing it
        // and starting again — not handing the manager the match that is already on the pitch.
        var service = CreateService();
        var firstMatchId = await StartAsync(service);
        _sessions.Remove(firstMatchId);

        var second = await service.StartAsync(_fixture.Id, 8, _home.Id, HomeSquadIds().Take(SquadSize).ToList());

        Assert.True(second.Accepted);
        Assert.NotEqual(firstMatchId, second.MatchId);
        Assert.Equal(MatchStatus.Abandoned, _started.Single(match => match.Id == firstMatchId).Status);
        Assert.False(_sessions.TryGet(firstMatchId, out _));
    }

    [Fact]
    public async Task A_headless_run_takes_over_a_match_nobody_is_driving()
    {
        // The fixture that can never be played again. A run created the match, left its
        // working memory on the registry and stopped for any reason at all; every run after
        // that one found the same session, was told "already being played here" by a process
        // that was playing nothing, and the matchday sat there for ever. The registry holds
        // memory, not a claim — a headless caller takes the match over and plays it.
        var service = CreateService();
        var stuckMatchId = await StartAsync(service);
        Assert.True(_sessions.TryGet(stuckMatchId, out _));

        var taken = await service.StartAsync(
            _fixture.Id,
            seed: 9,
            userTeamId: null,
            starterIds: HomeSquadIds().Take(SquadSize).ToList(),
            headless: true);

        Assert.True(taken.Accepted);
        Assert.NotEqual(stuckMatchId, taken.MatchId);
        Assert.Equal(MatchStatus.Abandoned, _started.Single(match => match.Id == stuckMatchId).Status);
        Assert.False(_sessions.TryGet(stuckMatchId, out _), "the old match's memory is nobody's now");
        Assert.Equal(FixtureStatus.InProgress, _fixture.Status);
    }

    [Fact]
    public async Task A_manager_is_still_sent_to_the_match_that_is_already_being_played()
    {
        // The other half of the same rule, and the one that must not be given up: a manager
        // arriving at a match somebody is watching joins it rather than restarting it.
        var service = CreateService();
        var matchId = await StartAsync(service);

        var joined = await service.StartAsync(
            _fixture.Id,
            seed: 9,
            userTeamId: _home.Id,
            starterIds: HomeSquadIds().Take(SquadSize).ToList());

        Assert.True(joined.Accepted);
        Assert.Equal(matchId, joined.MatchId);
        Assert.Equal(MatchRefusal.AlreadyRunningHere, joined.Reason);
        Assert.Equal(1, _started.Count);
    }

    [Fact]
    public async Task A_substitution_tells_the_recovery_how_long_each_of_its_two_played()
    {
        // The minutes are what the recovery is measured against, and they are stamped by the
        // substitution itself. A man swapped on at the twelfth minute has played twelve
        // minutes, not a match, and the end of the match reads these two numbers.
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var outgoing = state.HomeLineup.First(player => player.Position != Position.GK);
        var incoming = Available(state);

        // The match is run on a little before the change, so the two men are not swapped at
        // the whistle — where every man would be credited with the same ninety minutes and
        // the two bands would be indistinguishable.
        for (var tick = 0; tick < 2; tick++)
        {
            await service.TickAsync(matchId);
        }

        var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, incoming.PlayerId);

        Assert.True(result.Accepted);

        var after = serviceState(matchId);
        var minute = after.Minute;
        var manOn = after.HomeLineup.Single(player => player.PlayerId == incoming.PlayerId);
        var manOff = after.HomeBench.Single(player => player.PlayerId == outgoing.PlayerId);

        Assert.Equal(minute, manOn.EnteredAtMinute);
        Assert.Equal(minute, manOff.LeftAtMinute);

        // The man who came off is finished at that minute, and the man who came on is still
        // out there, so he is owed the rest of the game. The two are not the same number and
        // the recovery needs them apart: a full match and no match at all.
        Assert.Equal(minute, manOff.MinutesPlayed(MatchRules.MinutesInAMatch));
        Assert.Equal(MatchRules.MinutesInAMatch - minute, manOn.MinutesPlayed(MatchRules.MinutesInAMatch));
    }

    [Fact]
    public async Task A_man_who_never_left_the_pitch_is_credited_with_the_whole_match()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var starter = serviceState(matchId).HomeLineup.First(player => player.Position != Position.GK);

        Assert.Equal(0, starter.EnteredAtMinute);
        Assert.Equal(90, starter.MinutesPlayed(90));
    }

    [Fact]
    public async Task Substitute_swaps_the_rosterand_counts_the_change()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var lineup = serviceState(matchId).HomeLineup;
        var outgoing = lineup.First(p => p.Position != Position.GK);
        var incoming = Available(serviceState(matchId));

        var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, incoming.PlayerId);

        Assert.True(result.Accepted);
        var state = serviceState(matchId);
        Assert.Equal(1, state.SubstitutionsHome);
        Assert.Contains(state.HomeLineup, p => p.PlayerId == incoming.PlayerId);
        Assert.DoesNotContain(state.HomeLineup, p => p.PlayerId == outgoing.PlayerId);
        Assert.Contains(state.HomeBench, p => p.PlayerId == outgoing.PlayerId);
        Assert.Contains(result.Events, e => e.Type == MatchEventType.SubstitutionMade);
    }

    [Fact]
    public async Task The_last_goalkeeper_can_only_be_replaced_by_another_goalkeeper()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var keeper = state.HomeLineup.Single(player => player.KeepsGoal);
        var outfielder = state.HomeBench.First(player => player.Position != Position.GK);

        var error = await ThrowsDomainAsync(() =>
            service.SubstituteAsync(matchId, _home.Id, keeper.PlayerId, outfielder.PlayerId));

        Assert.Equal("GoalkeeperRequired", error.Code);
    }

    [Fact]
    public async Task A_club_whose_goalkeeper_was_sent_off_keeps_playing_and_substituting()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        state.HomeLineup.First(player => player.Position == Position.GK).SendOff(0);

        // The engine promotes an outfielder to the goal, so the club is never short of one.
        await service.TickAsync(matchId);
        Assert.Contains(serviceState(matchId).HomeLineup, player => player.KeepsGoal);

        var outgoing = serviceState(matchId).HomeLineup.First(player => player.Position != Position.GK);
        var incoming = Available(serviceState(matchId));

        var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, incoming.PlayerId);

        Assert.True(result.Accepted);
        Assert.Contains(serviceState(matchId).HomeLineup, player => player.KeepsGoal);
    }

    [Fact]
    public async Task A_squad_is_returned_in_position_order_and_by_name_inside_it()
    {
        var seasons = new Mock<ISeasonRepository>(MockBehavior.Loose);
        seasons.Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)));

        var service = new PlayerService(_players.Object, _teams.Object, seasons.Object, _matches.Object);

        var squad = (await service.GetSquadAsync(_home.Id, _seasonId)).ToList();

        var ranks = new[] { Position.GK, Position.DEF, Position.MID, Position.ATT };
        var ordered = squad
            .OrderBy(player => Array.IndexOf(ranks, player.Player.Position))
            .ThenBy(player => player.Player.Name, StringComparer.OrdinalIgnoreCase)
            .Select(player => player.Player.Id)
            .ToList();

        Assert.Equal(ordered, squad.Select(player => player.Player.Id));
    }

    [Fact]
    public async Task The_lineup_and_the_bench_come_back_in_position_order()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var lineup = await service.GetLineupAsync(matchId, _home.Id);

        foreach (var players in new[] { lineup.HomeLineup, lineup.HomeBench })
        {
            var ranks = players
                .Select(player => Array.IndexOf(new[] { Position.GK, Position.DEF, Position.MID, Position.ATT }, player.Position))
                .ToList();

            Assert.Equal(ranks.OrderBy(rank => rank).ToList(), ranks);
        }
    }

    [Fact]
    public async Task Substitute_refuses_a_player_who_is_not_on_the_pitch()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        var benchPlayer = serviceState(matchId).HomeBench[1];

        var error = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _home.Id, benchPlayer.PlayerId, serviceState(matchId).HomeBench[0].PlayerId));

        Assert.Equal("PlayerNotOnPitch", error.Code);
    }

    [Fact]
    public async Task Substitute_refuses_a_player_who_is_not_on_the_bench()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        var onPitch = serviceState(matchId).HomeLineup.First(p => p.Position != Position.GK);

        var error = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _home.Id, onPitch.PlayerId, Guid.NewGuid()));

        Assert.Equal("PlayerNotOnBench", error.Code);
    }

    [Fact]
    public async Task Substitute_refuses_a_player_substituting_himself()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        var onPitch = serviceState(matchId).HomeLineup[2];

        var error = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _home.Id, onPitch.PlayerId, onPitch.PlayerId));

        Assert.Equal("InvalidSubstitution", error.Code);
    }

    [Fact]
    public async Task A_substitution_only_moves_two_players_and_leaves_everybody_else_alone()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        // Play some football first, so the men on the pitch are actually tired and a change
        // of the numbers would be visible.
        for (var tick = 0; tick < 20; tick++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await service.TickAsync(matchId);
        }

        var before = serviceState(matchId);
        var energyBefore = Snapshot(before);

        var outgoing = before.HomeLineup.First(p => p.Position != Position.GK && p.IsOnPitch);
        var incoming = Available(before);

        var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, incoming.PlayerId);
        Assert.True(result.Accepted);

        var after = serviceState(matchId);
        var energyAfter = Snapshot(after);

        // A substitution is a swap of two names. It is not a spell: nobody else in the
        // match may recover a single point of energy because the manager used a change, and
        // the men involved keep exactly the energy they had.
        var movers = new HashSet<Guid> { outgoing.PlayerId, incoming.PlayerId };

        foreach (var (playerId, energy) in energyBefore)
        {
            Assert.True(
                energyAfter[playerId] == energy,
                movers.Contains(playerId)
                    ? "the two men in a swap keep their own energy"
                    : $"player {playerId} went from {energy} to {energyAfter[playerId]} without being substituted");
        }
    }

    /// <summary>
    /// The energy of every man in the match, by id. Bench included: a bench that quietly
    /// refreshed is exactly the bug this guards.
    /// </summary>
    private static Dictionary<Guid, int> Snapshot(MatchState state) =>
        state.HomeLineup.Concat(state.HomeBench)
            .Concat(state.AwayLineup.Concat(state.AwayBench))
            .GroupBy(player => player.PlayerId)
            .ToDictionary(group => group.Key, group => group.First().Energy);

    /// <summary>
    /// The first reserve who can actually be sent on. A bench is not a queue of waiting
    /// men: it also holds the substitutes who have already been taken off, and naming one
    /// of them again is refused.
    /// </summary>
    private static MatchPlayerSnapshot Available(MatchState state)
    {
        var ok = state.HomeBench.Where(p => !p.SubbedOff && !p.RedCard).ToList();
        Assert.NotEmpty(ok);
        return ok[0];
    }

    [Fact]
    public async Task A_substitute_who_has_been_taken_off_cannot_be_named_again()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        // Change one, and keep the id of the man who came off: he is on the bench again,
        // which is exactly why a bench is not a list of men who are waiting to play.
        var first = serviceState(matchId);
        var spent = first.HomeLineup.First(p => p.Position != Position.GK && p.IsOnPitch);
        var reserve = Available(first);
        Assert.True((await service.SubstituteAsync(matchId, _home.Id, spent.PlayerId, reserve.PlayerId)).Accepted);

        // Naming him now is the same change twice, and football does not give a substitute
        // back — so the manager is told no, with a code rather than a shrug.
        var error = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _home.Id,
            serviceState(matchId).HomeLineup.First(p => p.Position != Position.GK && p.IsOnPitch).PlayerId,
            spent.PlayerId));

        Assert.Equal("PlayerAlreadySubstituted", error.Code);
    }

    [Fact]
    public async Task Substitute_stops_after_five_changes()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        for (var change = 0; change < MaxSubstitutions; change++)
        {
            var state = serviceState(matchId);
            var outgoing = state.HomeLineup.First(p => p.Position != Position.GK && p.IsOnPitch);
            var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, Available(state).PlayerId);
            Assert.True(result.Accepted);
        }

        var final = serviceState(matchId);
        var error = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _home.Id,
            final.HomeLineup.First(p => p.Position != Position.GK && p.IsOnPitch).PlayerId,
            Available(final).PlayerId));

        Assert.Equal("SubstitutionLimitReached", error.Code);
    }

    [Fact]
    public async Task Substitute_never_changes_the_opposing_teams_lineup()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        var awayBefore = serviceState(matchId).AwayLineup.Select(p => p.PlayerId).ToList();

        var state = serviceState(matchId);
        await service.SubstituteAsync(
            matchId,
            _home.Id,
            state.HomeLineup.First(p => p.Position != Position.GK).PlayerId,
            state.HomeBench[0].PlayerId);

        Assert.Equal(awayBefore, serviceState(matchId).AwayLineup.Select(p => p.PlayerId));
        Assert.Equal(0, serviceState(matchId).SubstitutionsAway);
    }

    [Fact]
    public async Task Continue_second_half_only_works_from_the_half_time_pause()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var tooEarly = await service.ContinueSecondHalfAsync(matchId);
        Assert.False(tooEarly.Accepted);

        for (var tick = 0; tick < 200 && !serviceState(matchId).HalfTimePauseActive; tick++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await service.TickAsync(matchId);
        }

        Assert.True(serviceState(matchId).HalfTimePauseActive);

        var resumed = await service.ContinueSecondHalfAsync(matchId);

        Assert.True(resumed.Accepted);
        var state = serviceState(matchId);
        Assert.False(state.HalfTimePauseActive);
        Assert.Equal(1, state.Half);
        Assert.Contains(resumed.Events, e => e.Type == MatchEventType.SecondHalfStarted);
    }

    [Fact]
    public async Task Tick_advances_half_a_minute_and_records_the_goals()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var before = serviceState(matchId).HomeScore + serviceState(matchId).AwayScore;
        var result = await service.TickAsync(matchId);

        // A tick is half a minute of football. At one tick per minute a goal, a card and a
        // substitution all shared the same instant, and the feed stopped reading as a
        // sequence of things that happened.
        Assert.True(result.Accepted);
        Assert.Equal(0, serviceState(matchId).Minute);
        Assert.Equal(MatchRules.SecondsPerTick, serviceState(matchId).Seconds);
        Assert.True(
            serviceState(matchId).HomeScore + serviceState(matchId).AwayScore >= before);
        _matches.Verify(repo => repo.AddEventsAsync(It.IsAny<IEnumerable<MatchEvent>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task A_finished_match_writes_the_goals_of_its_players_into_the_season()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        var scorers = new Dictionary<Guid, int>();

        // Play it to full time: the engine decides who scores, the test only checks that
        // whatever it decided reaches the season state.
        for (var minute = 0; minute < 300 && runningState(matchId) is { } state && !state.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            foreach (var goal in tick.Events.Where(engineEvent => engineEvent.Type == MatchEventType.GoalScored))
            {
                if (goal.PlayerId is not { } scorerId)
                {
                    continue;
                }

                scorers[scorerId] = scorers.TryGetValue(scorerId, out var already) ? already + 1 : 1;
            }

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        Assert.True(scorers.Count > 0, "the engine should have awarded at least one goal in a full match");

        foreach (var (playerId, goals) in scorers)
        {
            var seasonState = _states.Single(state => state.PlayerId == playerId);

            Assert.Equal(goals, seasonState.Goals);
            _players.Verify(
                repo => repo.UpdateSeasonState(It.Is<PlayerSeasonState>(state => state.Id == seasonState.Id)),
                Times.Once);
        }
    }

    [Fact]
    public async Task Every_player_who_played_carries_the_energy_the_match_cost_him()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);
        MatchState? lastState = null;

        for (var minute = 0; minute < 300 && runningState(matchId) is { } state && !state.MatchFinished; minute++)
        {
            lastState = state;
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);
            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        Assert.NotNull(lastState);

        // The eleven that started plus anyone who came on, on both clubs.
        var tookPart = lastState!.HomeLineup
            .Concat(lastState.HomeBench)
            .Concat(lastState.AwayLineup)
            .Concat(lastState.AwayBench)
            .Select(player => player.PlayerId)
            .Distinct()
            .ToList();

        // Eleven on the pitch and seven on the bench, for each of the two clubs. The
        // reserve goalkeeper seeded for the lineup tests is the one that never makes it.
        Assert.Equal(2 * (SquadSize + BenchSize), tookPart.Count);

        foreach (var playerId in tookPart)
        {
            var seasonState = _states.Single(state => state.PlayerId == playerId);
            Assert.InRange(seasonState.Energy, 1, 100);
        }

        // Everyone in the day is written back, and so is every man of the two clubs who was
        // not even on the bench: he is given a full rest, which is the point of having a
        // squad. The count is therefore the whole of the two squads rather than the eighteen
        // on the day.
        var squads = await Task.WhenAll(
            _teams.Object.GetSquadAsync(_home.Id, _seasonId),
            _teams.Object.GetSquadAsync(_away.Id, _seasonId));

        var squad = new HashSet<Guid>(
            squads.SelectMany(members => members).Select(membership => membership.PlayerId));

        _players.Verify(repo => repo.UpdateSeasonState(It.IsAny<PlayerSeasonState>()), Times.Exactly(squad.Count));
    }

    [Fact]
    public async Task A_player_who_was_not_even_on_the_bench_recovers_a_whole_day_off()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        // A man of the club who was not in the eighteen of the day. The eleven and the seven
        // are the day; a club of twenty-three has five men who were not even invited, and they
        // are the ones who had the day off.
        var inTheDay = new HashSet<Guid>(HomeSquadIds().Take(SquadSize + BenchSize));
        var rested = _states.Where(state => !inTheDay.Contains(state.PlayerId)).ToList();

        Assert.NotEmpty(rested);

        foreach (var before in rested)
        {
            before.DrainEnergy(20);
        }

        for (var minute = 0; minute < 300 && runningState(matchId) is { } state && !state.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);
            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        foreach (var after in rested)
        {
            // The full-rest band starts where the on-the-bench band ends, so a man who did not
            // travel can never be given the same recovery as a man who sat on the bench.
            Assert.InRange(after.Energy, 1 + EnergyRecoveryRules.MinFullRest, 100);
        }
    }

    /// <summary>
    /// Two dark shirts on one pitch are two dark shirts, and the second shirt a club drew is
    /// what the game changes into when that happens.
    ///
    /// The decision is stamped on the match rather than worked out when the lineup is read, so
    /// a manager who reloads his own match at minute seventy is looking at the same two shirts
    /// he was looking at before. The assertion is that the two shirts the fixture was actually
    /// played in can be told apart, rather than which of the two clubs changed: the draw is over
    /// the answers that work, and pinning one of them here would pin a coin.
    /// </summary>
    [Fact]
    public async Task Two_dark_clubs_do_not_turn_out_in_two_shirts_that_look_the_same()
    {
        // Both clubs dark, and each of them with a second shirt that is nothing like its first.
        _home.SetHomeKit(new KitDesign("#123a8f", "#ffffff", KitPattern.Solid));
        _home.SetAwayKit(new KitDesign("#ffd700", "#123a8f", KitPattern.HorizontalStripe));
        _away.SetHomeKit(new KitDesign("#0d5c2e", "#ffd700", KitPattern.Solid));
        _away.SetAwayKit(new KitDesign("#ffffff", "#0d5c2e", KitPattern.HorizontalStripe));

        var service = CreateService();
        var matchId = await StartAsync(service);

        var lineup = await service.GetLineupAsync(matchId, _home.Id);

        var worn = new[] { (lineup.HomeKitSide, lineup.HomeTeam), (lineup.AwayKitSide, lineup.AwayTeam) }
            .Select(pair => pair.Item1 is KitSide.Away ? pair.Item2.AwayKit : pair.Item2.HomeKit)
            .ToArray();

        Assert.All(worn, kit => Assert.NotNull(kit));
        Assert.False(KitClash.AreIndistinguishable(worn[0]!, worn[1]!));
    }

    /// <summary>
    /// A fixture is played in the shirts the clubs turn out in, and says so on the lineup, so a
    /// screen can put the right one beside a name without working it out for itself.
    /// </summary>
    [Fact]
    public async Task A_fixture_between_two_clubs_that_never_drew_a_kit_is_played_in_their_own_colours()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var lineup = await service.GetLineupAsync(matchId, _home.Id);

        Assert.Equal(KitSide.Home, lineup.HomeKitSide);
        Assert.Equal(KitSide.Home, lineup.AwayKitSide);
    }

    private async Task<Guid> StartAsync(MatchService service)
    {
        var result = await service.StartAsync(_fixture.Id, 7, _home.Id, HomeSquadIds().Take(SquadSize).ToList());
        Assert.True(result.Accepted);
        return result.MatchId;
    }

    /// <summary>
    /// The other thing a match can stop and wait for. What the engine decides is covered in
    /// the domain; what the manager is handed is a question, and this is that question as
    /// the screen receives it.
    /// </summary>
    [Fact]
    public async Task A_serious_injury_to_the_managers_club_waits_for_him_to_name_the_replacement()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var hurt = state.HomeLineup.First(player => player.Position == Position.DEF);

        // What the engine leaves behind when the answer is the manager's to give.
        state.InjuryAwaitingSubstitution = true;
        state.InjuryPlayerId = hurt.PlayerId;
        state.InjuryTeam = 1;
        state.PendingInjuryMatchesOut = 3;

        var minute = state.Minute;
        var tick = await service.TickAsync(matchId);

        // The clock stands still while the manager decides.
        Assert.Empty(tick.Events);
        Assert.Equal(minute, serviceState(matchId).Minute);
        Assert.True(serviceState(matchId).InjuryAwaitingSubstitution);

        var view = await service.GetStateAsync(matchId);

        // The screen is told who, for which club, and how bad it is, so the dialog can open
        // already pointed at him instead of asking the manager to go and find him.
        Assert.True(view.Injury.AwaitingSubstitution);
        Assert.Equal(hurt.PlayerId, view.Injury.PlayerId);
        Assert.Equal(hurt.Name, view.Injury.PlayerName);
        Assert.Equal(1, view.Injury.Team);
        Assert.Equal(Injury.Grave, view.Injury.Severity);
    }

    [Fact]
    public async Task The_hurt_player_is_still_in_the_eleven_while_the_answer_is_outstanding()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var hurt = state.HomeLineup.First(player => player.Position == Position.MID);

        state.InjuryAwaitingSubstitution = true;
        state.InjuryPlayerId = hurt.PlayerId;
        state.InjuryTeam = 1;
        state.PendingInjuryMatchesOut = 2;

        // He is playing until the change is made. A player who is not on the pitch cannot
        // be the one a substitution is made for, and the eleven under the scoreboard would
        // show a club that had already picked somebody.
        Assert.True(hurt.IsOnPitch);
        Assert.Equal(Injury.None, hurt.Injury);
        Assert.Equal(11, state.HomeLineup.Count);
    }

    [Fact]
    public async Task A_match_waits_on_nobody_when_nobody_is_hurt()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var view = await service.GetStateAsync(matchId);

        Assert.False(view.Injury.AwaitingSubstitution);
        Assert.Null(view.Injury.PlayerId);
    }

    /// <summary>
    /// The note every man is carrying, on the tick, while the match is being played.
    /// </summary>
    /// <remarks>
    /// This is the whole of "live", and it was worth writing down because the number is
    /// stored rather than worked out on demand: a rating read from the persisted statistics
    /// exists only once the whistle has gone, and a screen that read it from there showed a
    /// settled opinion of a match that was still being played. It was built from the state the
    /// tick already carries so that the eleven under the scoreboard moves with the football
    /// rather than being a second thing to go and fetch.
    /// </remarks>
    [Fact]
    public async Task Every_man_on_the_pitch_carries_a_number_while_the_match_is_being_played()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        // Past the fifth minute, which is where the first note appears at all: a man who has
        // not played five minutes has a cameo and not a match. The numbers are read at minute
        // zero because the clock is, so the assertion that nothing is rated before then is
        // part of what is being held here.
        var atTheWhistle = await service.GetStateAsync(matchId);
        Assert.All(atTheWhistle.LiveRatings, rating => Assert.Null(rating.Rating));

        await PlayUntil(service, matchId, minute: 20);

        var view = await service.GetStateAsync(matchId);

        // Both elevens, because a manager watching a match he is not in reads the same eleven
        // and the same eleven has to be rated for him too.
        Assert.Equal(22, view.LiveRatings.Count);
        Assert.Equal(
            view.LiveRatings.Select(rating => rating.PlayerId).Distinct().Count(),
            view.LiveRatings.Count);

        // The men who have been on the pitch long enough, rated; the ones who have not, not.
        Assert.Contains(view.LiveRatings, rating => rating.MinutesPlayed > 0);

        var rated = view.LiveRatings.Where(rating => rating.Rating is not null).ToList();
        Assert.NotEmpty(rated);
        Assert.All(rated, rating => Assert.InRange(rating.Rating!.Value, 0d, 10d));

        // The band travels with the number rather than being worked out by whoever reads it,
        // and the two have to agree — that is the whole claim of carrying both.
        Assert.All(rated, rating =>
            Assert.Equal(Domain.Matches.MatchRating.BandOf(rating.Rating), rating.RatingBand));
    }

    /// <summary>
    /// The number moves with the football, which is the other half of being live.
    /// </summary>
    /// <remarks>
    /// A card frozen at its kick-off value is not a live rating, it is a number that happens
    /// to be on screen during a match. The engine takes the eleven out to the final whistle and
    /// at least one of them has to have had a worse evening than he started with, so the two
    /// reads below cannot be equal.
    /// </remarks>
    [Fact]
    public async Task The_number_on_a_card_moves_as_the_match_does()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var atTheWhistle = await service.GetStateAsync(matchId);

        await PlayUntil(service, matchId, minute: 45);

        var later = await service.GetStateAsync(matchId);

        Assert.NotEqual(
            atTheWhistle.LiveRatings.Select(rating => rating.Rating).ToArray(),
            later.LiveRatings.Select(rating => rating.Rating).ToArray());
    }

    [Fact]
    public async Task A_penalty_of_the_managers_club_waits_for_him_to_name_the_taker()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;

        var before = state.HomeScore + state.AwayScore;
        var tick = await service.TickAsync(matchId);

        // The clock stands still while the manager decides.
        Assert.Empty(tick.Events);
        Assert.True(serviceState(matchId).PenaltyAwaitingSelection);

        var view = await service.GetStateAsync(matchId);
        Assert.True(view.Penalty.AwaitingSelection);
        Assert.Equal(11, view.Penalty.Candidates.Count);

        // The list the dialog is built from is ordered by the chance the engine would roll
        // for each candidate against the goalkeeper he is shooting at, so the top of the
        // list really is the best man available to take it.
        var keeper = serviceState(matchId).AwayLineup.FirstOrDefault(p => p.KeepsGoal);
        var chances = view.Penalty.Candidates
            .Select(candidate => MatchEngine.PenaltyConversion(candidate, keeper))
            .ToList();

        Assert.All(chances, chance => Assert.InRange(chance, 0.60, 0.92));
        Assert.Equal(chances.OrderByDescending(chance => chance), chances);
        Assert.Contains(view.Penalty.Candidates, candidate => candidate.Position == Position.GK);

        var taker = view.Penalty.Candidates.First(candidate => candidate.Position == Position.ATT);
        var taken = await service.SelectPenaltyTakerAsync(matchId, _home.Id, taker.PlayerId);

        Assert.True(taken.Accepted);
        Assert.False(serviceState(matchId).PenaltyAwaitingSelection);
        Assert.Contains(taken.Events, e => e.Type == MatchEventType.PenaltyTaken);

        var after = serviceState(matchId).HomeScore + serviceState(matchId).AwayScore;

        // A converted penalty is a goal, and GoalScored is the only thing that says so. The
        // wording is not asked about: the narration says this in several ways on purpose,
        // and a test that reads the prose breaks every time a line is reworded.
        var scored = taken.Events.Any(e => e.Type == MatchEventType.GoalScored);

        Assert.Equal(scored, after > before);
    }

    [Fact]
    public async Task The_taker_of_a_penalty_has_to_be_on_the_pitch()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 1;

        var benched = state.HomeBench.First().PlayerId;

        var error = await ThrowsDomainAsync(() =>
            service.SelectPenaltyTakerAsync(matchId, _home.Id, benched));

        Assert.Equal("PlayerNotOnPitch", error.Code);
    }

    [Fact]
    public async Task A_manager_of_the_away_team_takes_his_own_penalty()
    {
        var service = CreateService();
        var result = await service.StartAsync(_fixture.Id, 7, _away.Id, AwaySquadIds().Take(SquadSize).ToList());
        Assert.True(result.Accepted);
        var matchId = result.MatchId;

        // The state tells the client which club it is commanding: the away one.
        var state = await service.GetStateAsync(matchId);
        Assert.Equal(_away.Id, state.UserTeamId);

        var lineup = await service.GetLineupAsync(matchId, _away.Id);
        Assert.Equal(1, lineup.UserTeamIndex);

        // A penalty for the away club, which is the manager's own.
        var live = serviceState(matchId);
        live.PenaltyAwaitingSelection = true;
        live.PenaltyTeam = 2;

        var view = await service.GetStateAsync(matchId);
        Assert.True(view.Penalty.AwaitingSelection);

        // The home club is not the manager's, and a manager commands his own club only. He
        // gets that answer first, and it is the honest one: he has no eleven in that side of
        // the fixture, so there is nobody for him to name a taker out of.
        var wrong = await ThrowsDomainAsync(() => service.SelectPenaltyTakerAsync(
            matchId, _home.Id, view.Penalty.Candidates[0].PlayerId));

        Assert.Equal("NotYourTeam", wrong.Code);

        // And his own club, asked to take a penalty awarded to the other one, is refused for
        // the other reason: the penalty is not his. A manager may not hand his taker to the
        // opposition, so the two refusals stay two and neither answers for the other.
        live.PenaltyTeam = 1;
        var viewForHome = await service.GetStateAsync(matchId);
        Assert.False(viewForHome.Penalty.AwaitingSelection);

        var notMine = await ThrowsDomainAsync(() => service.SelectPenaltyTakerAsync(
            matchId, _away.Id, view.Penalty.Candidates[0].PlayerId));

        Assert.Equal("PenaltyNotForTeam", notMine.Code);

        live.PenaltyTeam = 2;
        var taken = await service.SelectPenaltyTakerAsync(
            matchId, _away.Id, view.Penalty.Candidates[0].PlayerId);

        Assert.True(taken.Accepted);
        Assert.Contains(taken.Events, e => e.Type == MatchEventType.PenaltyTaken);
        Assert.False(serviceState(matchId).PenaltyAwaitingSelection);
    }

    [Fact]
    public async Task A_manager_cannot_substitute_for_a_club_of_another_match()
    {
        // The manager's own club is in this match, and a matchday carries the other clubs'
        // games to his screen, so a command naming the opposition is one a client can send
        // without meaning to. It is refused: he commands his own club, and only his own.
        var service = CreateService();
        var result = await service.StartAsync(_fixture.Id, 7, _home.Id, HomeSquadIds().Take(SquadSize).ToList());
        Assert.True(result.Accepted);
        var matchId = result.MatchId;

        var state = serviceState(matchId);
        var opponentOnPitch = state.AwayLineup.First(player => player.IsOnPitch);
        var opponentOnBench = state.AwayBench.First(player => !player.SubbedOff);

        var refused = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _away.Id, opponentOnPitch.PlayerId, opponentOnBench.PlayerId));

        Assert.Equal("NotYourTeam", refused.Code);

        // The refusal changed nothing: the side he is not managing still has eleven out there
        // and the man he named is still the one playing, because a refused command is not a
        // change that happened and then was undone.
        var after = serviceState(matchId);
        Assert.Contains(after.AwayLineup, player => player.PlayerId == opponentOnPitch.PlayerId);
        Assert.DoesNotContain(after.AwayLineup, player => player.PlayerId == opponentOnBench.PlayerId);
        Assert.Equal(0, after.SubstitutionsAway);
    }

    [Fact]
    public async Task A_manager_may_still_substitute_for_his_own_club()
    {
        // The other side of the rule above: a refusal that refused everything would be a
        // match the manager cannot manage, which is the worse bug of the two.
        var service = CreateService();
        var result = await service.StartAsync(_fixture.Id, 7, _home.Id, HomeSquadIds().Take(SquadSize).ToList());
        Assert.True(result.Accepted);
        var matchId = result.MatchId;

        var state = serviceState(matchId);
        var ownOnPitch = state.HomeLineup.First(player => player.IsOnPitch && player.Position != Domain.Enums.Position.GK);
        var ownOnBench = state.HomeBench.First(player => !player.SubbedOff);

        var swapped = await service.SubstituteAsync(
            matchId, _home.Id, ownOnPitch.PlayerId, ownOnBench.PlayerId);

        Assert.True(swapped.Accepted);

        var after = serviceState(matchId);
        Assert.Contains(after.HomeLineup, player => player.PlayerId == ownOnBench.PlayerId);
        Assert.Equal(1, after.SubstitutionsHome);
    }

    [Fact]
    public async Task A_penalty_is_refused_once_it_is_no_longer_waiting()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var candidate = serviceState(matchId).HomeLineup[5];
        var result = await service.SelectPenaltyTakerAsync(matchId, _home.Id, candidate.PlayerId);

        Assert.False(result.Accepted);
        Assert.Contains("pênalti", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_engine_picks_the_taker_of_the_opponents_penalty()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        state.PenaltyAwaitingSelection = true;
        state.PenaltyTeam = 2;

        var tick = await service.TickAsync(matchId);

        Assert.Contains(tick.Events, e => e.Type == MatchEventType.PenaltyTaken);
        Assert.False(serviceState(matchId).PenaltyAwaitingSelection);

        var view = await service.GetStateAsync(matchId);
        Assert.False(view.Penalty.AwaitingSelection);
    }

    [Fact]
    public async Task An_injury_that_takes_a_player_off_carries_into_the_season()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var injured = state.HomeLineup.First(player => player.Position == Position.ATT);
        injured.Injure(Injury.Grave, matchesOut: 3);

        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        var seasonState = _states.Single(candidate => candidate.PlayerId == injured.PlayerId);

        // The duration is the one the engine drew, not a fixed sentence: a month of
        // football is not always the same month.
        Assert.Equal(Injury.Grave, seasonState.Injury);
        Assert.Equal(3, seasonState.InjuryMatchesRemaining);
        Assert.False(seasonState.IsAvailable);
    }

    [Fact]
    public async Task ALight_knock_is_played_through_and_costs_only_energy()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var knocked = state.HomeLineup.First(player => player.Position == Position.ATT);
        knocked.Injure(Injury.Light);

        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        var seasonState = _states.Single(candidate => candidate.PlayerId == knocked.PlayerId);

        // A player who felt something and carried on has not been injured as far as the
        // season is concerned: the knock costs him energy, not fixtures. The assertion
        // allows for a later, unrelated injury taking him out — the claim is that the light
        // knock on its own is not an absence.
        Assert.False(seasonState.Injury == Injury.Light && seasonState.InjuryMatchesRemaining > 0);
        Assert.True(seasonState.Energy < 100);
    }

    [Fact]
    public async Task A_finished_match_puts_the_gate_of_both_clubs_in_their_books()
    {
        GiveTheHomeClubAGround();
        var service = CreateService();
        await PlayTheWholeMatchAsync(service, await StartAsync(service));

        var gates = _book.Where(movement => movement.Kind == FinanceMovementKind.GateRevenue).ToList();

        // The public paid at the gate, so the money belongs to the club that hosted the
        // match. Both clubs get a line because both are owed it, and the host's is the
        // larger share of the same takings.
        Assert.Equal(2, gates.Count);
        Assert.All(gates, gate => Assert.True(gate.Amount > 0m));
        Assert.Equal(
            _home.Id,
            gates.OrderByDescending(gate => gate.Amount).First().TeamId);
    }

    [Fact]
    public async Task A_gate_is_written_against_the_day_the_match_was_played()
    {
        GiveTheHomeClubAGround();
        var service = CreateService();
        var matchId = await StartAsync(service);

        await PlayTheWholeMatchAsync(service, matchId);

        var gate = _book.Single(movement =>
            movement.Kind == FinanceMovementKind.GateRevenue && movement.TeamId == _home.Id);

        // A line of money belongs to a day of the season, because that is when the money
        // moved, and a ledger that could not say when is a list of amounts.
        Assert.Equal(_matchDay.Number, gate.MatchDayNumber);
        Assert.Equal(matchId, gate.MatchId);
    }

    [Fact]
    public async Task A_club_pays_its_squad_at_the_end_of_a_league_match()
    {
        GiveTheHomeClubAGround();
        var service = CreateService();
        await PlayTheWholeMatchAsync(service, await StartAsync(service));

        // The bill is settled at the whistle: one line per club for the matchday, written
        // against the day it was played and carrying the match it was paid for.
        var bills = _book.Where(movement => movement.Kind == FinanceMovementKind.Wages).ToList();

        Assert.Equal(2, bills.Count);
        Assert.All(bills, bill => Assert.True(bill.Amount < 0m));
        Assert.All(bills, bill => Assert.Equal(_matchDay.Number, bill.MatchDayNumber));
        Assert.All(bills, bill => Assert.NotEqual(Guid.Empty, bill.MatchId ?? Guid.Empty));

        // Both clubs and both of them once. Set membership rather than an order: which club's
        // bill was written first is a fact about the order the match finished in, and a test
        // that insisted on one of the two orders would be failing for a reason that has
        // nothing to do with the books.
        Assert.Contains(_home.Id, bills.Select(bill => bill.TeamId));
        Assert.Contains(_away.Id, bills.Select(bill => bill.TeamId));
    }

    [Fact]
    public async Task A_match_pays_its_wages_once_and_not_once_per_tick_that_finishes_it()
    {
        GiveTheHomeClubAGround();
        var service = CreateService();
        var matchId = await StartAsync(service);

        await PlayTheWholeMatchAsync(service, matchId);

        // A match that blew its own whistle and one that arrives after it are the same match,
        // and the book is told so by the line existing for that match: no flag, nothing to
        // keep in step, and no way for a client to charge a club twice by asking again.
        var bills = _book
            .Where(movement => movement.Kind == FinanceMovementKind.Wages && movement.TeamId == _home.Id)
            .ToList();

        Assert.Equal(1, bills.Count);

        // Asking again about the same match writes nothing further.
        var again = await service.TickAsync(matchId);
        Assert.False(again.Accepted);
        Assert.Equal(1, _book.Count(movement =>
            movement.Kind == FinanceMovementKind.Wages && movement.TeamId == _home.Id));
    }

    [Fact]
    public async Task A_cup_tie_is_not_paid_in_wages()
    {
        GiveTheHomeClubAGround();

        // The fixture's competition becomes a cup, which is the only thing that changes: a tie
        // is a matchday of the cup's own calendar and not one of the league's, and the league's
        // calendar is what a club's wages are spread over.
        _competitions.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Competition.Create("Copa do Mundo", CompetitionType.Cup));

        var service = CreateService();
        var matchId = await StartAsync(service);

        await PlayTheWholeMatchAsync(service, matchId);

        // The gate is still paid: a cup tie is a match somebody bought a ticket for. The wages
        // are not, because the club entered the tie for the prize and not for the season.
        Assert.NotEmpty(_book.Where(movement => movement.Kind == FinanceMovementKind.GateRevenue));
        Assert.Empty(_book.Where(movement => movement.Kind == FinanceMovementKind.Wages));
    }

    [Fact]
    public async Task A_balance_continues_from_the_line_before_it()
    {
        GiveTheHomeClubAGround();
        var service = CreateService();
        await PlayTheWholeMatchAsync(service, await StartAsync(service));

        // Every line carries the balance it left behind, and the two lines of a club's book
        // are joined by it: the second starts where the first finished. A book whose lines
        // each said their own balance without checking would be two accounts of one match.
        var home = _book.Where(movement => movement.TeamId == _home.Id).ToList();

        Assert.Equal(2, home.Count);
        Assert.Equal(home[0].BalanceAfter + home[1].Amount, home[1].BalanceAfter);
    }

    [Fact]
    public async Task A_rest_worth_more_than_a_match_is_what_makes_rotation_work()
    {
        // The two men are put on the same energy before the whistle, short of full.
        //
        // Two things have to be true for this to measure the rule rather than the fixture.
        // They have to start level, because a squad's men arrive at a match with whatever
        // they had left and comparing two men who began fifteen apart measures nothing but
        // that. And they have to be short of full, because a recovery is added to both and a
        // full man cannot show a difference: the ceiling swallows it.
        //
        // The energy is set before the kick-off because that is when the match takes its
        // snapshot. A squad state changed afterwards is a squad state the match has never
        // heard of, and the man would enter the evening at whatever he had on Tuesday.
        //
        // The whole squad is levelled rather than the two men, because which eleven the staff
        // pick and who is left on the bench is the service's business and not this test's.
        foreach (var teamId in HomeSquadIds())
        {
            _states.Single(state => state.PlayerId == teamId).SetEnergy(70);
        }

        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var starter = state.HomeLineup.First();
        var neverUsed = state.HomeBench.Last();

        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        var starterState = _states.Single(candidate => candidate.PlayerId == starter.PlayerId);
        var restedState = _states.Single(candidate => candidate.PlayerId == neverUsed.PlayerId);

        // The two bands do not overlap, and that gap is the whole reason a squad is
        // rotated: the man who sat on the bench has to come out ahead, and he comes out
        // further ahead than the bands alone because the match took the other one more
        // besides.
        Assert.True(
            restedState.Energy - starterState.Energy >= MatchRules.MinRecoveryAfterResting - MatchRules.MaxRecoveryAfterPlaying,
            $"rested {restedState.Energy}, started-and-played {starterState.Energy}.");
    }

    [Fact]
    public async Task A_suspension_is_served_at_the_kick_off_of_the_next_match()
    {
        var suspended = _states.First(state => state.TeamId == _away.Id);
        suspended.AddSuspension(2);

        var service = CreateService();
        await StartAsync(service);

        Assert.Equal(1, suspended.SuspensionMatches);
        Assert.False(suspended.IsAvailable);
    }

    [Fact]
    public async Task A_player_is_available_again_when_his_suspension_is_served()
    {
        var suspended = _states.First(state => state.TeamId == _away.Id);
        suspended.AddSuspension(1);

        var service = CreateService();
        await StartAsync(service);

        Assert.Equal(0, suspended.SuspensionMatches);
        Assert.True(suspended.IsAvailable);
    }

    /// <summary>
    /// Names a taker when the engine is waiting for one. A penalty of the manager's club
    /// stops the clock until he decides, so any test that plays minutes of football has to
    /// answer it the way the client does.
    /// </summary>
    /// <summary>
    /// Answers the other thing a match can stop and wait for. A serious injury to the
    /// manager's own club holds the clock until he names the replacement, so a test that
    /// plays a match out has to answer it the way a manager would — otherwise the match
    /// stands still for the rest of the test and never reaches full time.
    /// </summary>
    private async Task ReplaceAPlayerWhoCannotContinueAsync(MatchService service, Guid matchId, Guid? keepOnBench = null)
    {
        if (!_sessions.TryGet(matchId, out var session) || !session!.State.InjuryAwaitingSubstitution)
        {
            return;
        }

        var isHome = session.State.InjuryTeam == 1;
        var lineup = isHome ? session.State.HomeLineup : session.State.AwayLineup;
        var bench = isHome ? session.State.HomeBench : session.State.AwayBench;
        var teamId = isHome ? _home.Id : _away.Id;
        var hurt = lineup.Single(player => player.PlayerId == session.State.InjuryPlayerId);

        // <paramref name="keepOnBench"/> is a man a test is measuring as unused, and
        // answering an injury with him would change what the test is about.
        var incoming = bench.FirstOrDefault(player => player.PlayerId != keepOnBench
            && !player.SubbedOff
            && NinjaEleven.Domain.Matches.MatchSubstitution.CanSwap(lineup, hurt, player));

        Assert.NotNull(incoming);

        var result = await service.SubstituteAsync(
            matchId,
            teamId,
            hurt.PlayerId,
            incoming!.PlayerId);

        Assert.True(result.Accepted);
    }

    private async Task TakePenaltyIfItIsTheManagersTurnAsync(MatchService service, Guid matchId)
    {
        if (!_sessions.TryGet(matchId, out var session) || !session!.State.PenaltyAwaitingSelection)
        {
            return;
        }

        var isHome = session.State.PenaltyTeam == 1;
        var lineup = isHome ? session.State.HomeLineup : session.State.AwayLineup;
        var taker = lineup.First(player => player.IsOnPitch && player.Position == Position.ATT);

        var result = await service.SelectPenaltyTakerAsync(
            matchId,
            isHome ? _home.Id : _away.Id,
            taker.PlayerId);

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e.Type == MatchEventType.PenaltyTaken);
    }

    /// <summary>
    /// A match that has been played leaves a line behind for every man who took part in
    /// it, and for nobody who did not.
    ///
    /// This is the record a career is made of. The live session is discarded the moment
    /// the whistle goes, so a line that is not written here did not happen as far as the
    /// rest of the game is concerned — and the season's own counters, which are summed
    /// from these lines, would be counting matches that the history does not show.
    ///
    /// The unused substitute is the case worth locking. He was on the team sheet, he was
    /// named in the squad, and he never touched the ball: an appearance here would be a
    /// man in a suit getting a game.
    /// </summary>
    [Fact]
    public async Task A_played_match_leaves_a_line_for_everybody_who_played_and_nobody_who_did_not()
    {
        var written = new List<MatchPlayerStatistics>();
        _matches.Setup(repository => repository.AddPlayerStatisticsAsync(
                It.IsAny<IEnumerable<MatchPlayerStatistics>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<MatchPlayerStatistics> lines, CancellationToken _) => written.AddRange(lines))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var matchId = await StartAsync(service);

        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            // The interval stops the loop, and only the manager walks out of it.
            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        Assert.NotEmpty(written);

        // A man who was in the eleven kicks off, and a man who came on does not.
        Assert.All(written, line => Assert.False(line.Started && line.CameOn));

        // Every line belongs to this match, and there is only one line per player: a second
        // save would double his season in the history.
        Assert.All(written, line => Assert.Equal(matchId, line.MatchId));
        Assert.Equal(written.Count, written.Select(line => line.PlayerId).Distinct().Count());

        // Whoever played was on the pitch or was taken off, so he is marked one way or the
        // other. Somebody who only ever sat on the bench has neither.
        Assert.Contains(written, line => line.Started);
    }

    /// <summary>
    /// The goals on the player lines are the goals the match actually announced.
    ///
    /// Two accounts of the same afternoon, which could drift apart, and a striker whose
    /// history says four while the feed said five is a history nobody can argue with. The
    /// test does not decide how many goals there should be — that is the engine's call — it
    /// only holds the two accounts to each other.
    /// </summary>
    [Fact]
    public async Task The_goals_on_the_player_lines_are_the_goals_the_match_announced()
    {
        var written = new List<MatchPlayerStatistics>();
        _matches.Setup(repository => repository.AddPlayerStatisticsAsync(
                It.IsAny<IEnumerable<MatchPlayerStatistics>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<MatchPlayerStatistics> lines, CancellationToken _) => written.AddRange(lines))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var matchId = await StartAsync(service);
        var announced = 0;

        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            announced += tick.Events.Count(engineEvent => engineEvent.Type == MatchEventType.GoalScored);

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        Assert.NotEmpty(written);
        Assert.Equal(announced, written.Sum(line => line.Goals + line.OwnGoals));
    }

    /// <summary>
    /// Plays the match forward until the clock says what it says, stepping over the interval
    /// the way the loop does rather than by calling it and hoping.
    /// </summary>
    private static async Task PlayUntil(MatchService service, Guid matchId, int minute)
    {
        for (var tick = 0; tick < 2000; tick++)
        {
            var state = await service.GetStateAsync(matchId);

            if (state.Minute >= minute)
            {
                return;
            }

            if (state.IsHalfTime)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
            else
            {
                await service.TickAsync(matchId);
            }
        }
    }

    private MatchState serviceState(Guid matchId)
    {
        Assert.True(_sessions.TryGet(matchId, out var session));
        return session!.State;
    }

    /// <summary>
    /// The running state, or null once the match is over: the service drops the session
    /// when it finishes, which is how a caller knows there is nothing left to tick.
    /// </summary>
    /// <summary>
    /// Gives the home club a ground, which the rest of these tests do without and the tests
    /// about money cannot: a match played in an empty ground has a crowd of nobody and a gate
    /// of nothing, so there is no money to write down and a test about the books would be
    /// asserting on an absence.
    /// </summary>
    private void GiveTheHomeClubAGround() =>
        _home.SetStadium(Stadium.Create(_home.Id, _home.Name));

    /// <summary>
    /// Plays a match from the whistle to the last tick, answering the things a manager
    /// answers and the engine does not answer itself. What the engine decided is what the
    /// match is afterwards.
    /// </summary>
    private async Task PlayTheWholeMatchAsync(MatchService service, Guid matchId)
    {
        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            await ReplaceAPlayerWhoCannotContinueAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }
    }

    private MatchState? runningState(Guid matchId) =>
        _sessions.TryGet(matchId, out var session) && !session!.State.MatchFinished ? session.State : null;
}
