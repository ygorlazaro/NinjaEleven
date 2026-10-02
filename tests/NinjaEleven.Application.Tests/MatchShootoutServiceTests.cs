using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A second leg of a cup tie that is level after ninety minutes goes to penalties, and the
/// penalties are taken in front of the crowd — one at a time, with the two managers naming
/// the men who take them.
///
/// The engine holds the rules; what these tests hold down is the seam around it, and the
/// seam is where this went wrong once: the match was never told which tie it was playing,
/// so a level aggregate could not be recognised and the shootout could not open no matter
/// how complete the rules were. So the fixture here is a real second leg, the tie is a real
/// tie, and the ninety minutes are played out until the aggregate is level — which is the
/// only way to be sure the test is about the aggregate and not about whichever seed drew.
/// </summary>
public class MatchShootoutServiceTests
{
    /// <summary>
    /// Enough ticks for ninety minutes of football and the ticks the clock is held for,
    /// plus the shootout, which is a kick a tick.
    /// </summary>
    private const int WholeMatch = 900;

    /// <summary>
    /// The men on the pitch at kick-off: a whole eleven, with one keeper in it, because a
    /// club that starts without one is a club the engine has to fix before it can play.
    /// </summary>
    private static readonly Position[] StartingEleven =
    [
        Position.GK, Position.DEF, Position.DEF, Position.DEF, Position.MID,
        Position.MID, Position.MID, Position.ATT, Position.ATT, Position.ATT, Position.ATT
    ];

    /// <summary>
    /// How many men are on the bench. One of them stays there for the whole match, which
    /// is what gives the shootout pool a man outside it.
    /// </summary>
    private const int BenchSize = 7;

    /// <summary>
    /// The energy every man in this world starts a match with: nobody here is a substitute
    /// who has to be managed carefully, and a tired squad would make the shootout's pool
    /// depend on the injuries of a single seed.
    /// </summary>
    private const int SquadEnergy = 90;

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
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly MatchSessionRegistry _sessions = new();

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly CompetitionSeason _competitionSeason;
    private readonly Round _round;
    private readonly MatchDay _matchDay;
    private readonly Team _home;
    private readonly Team _away;
    private readonly Fixture _fixture;
    private readonly CupTie _tie;
    private readonly Match _firstLeg;
    private readonly List<Player> _roster = [];
    private readonly List<PlayerSeasonState> _states = [];

    /// <summary>
    /// The matches this world has started, kept because a repository that cannot hand a
    /// match back is a repository that has forgotten it: every command after the start reads
    /// the match again.
    /// </summary>
    private readonly List<Match> _started = [];

    /// <summary>
    /// The fixture and the tie the match is being played in. They are fields rather than
    /// constants because a fixture is only ever played once: the search for a seed has to
    /// draw a throwaway leg for every seed it tries, and put this pair back afterwards.
    /// </summary>
    private Fixture? _playing;
    private CupTie? _playingTie;

    public MatchShootoutServiceTests()
    {
        _competitionSeason = CompetitionSeason.Create(Guid.NewGuid(), _seasonId);
        _round = Round.Create(_competitionSeason.Id, 1);
        _matchDay = MatchDay.Create(_seasonId, 1, new DateOnly(2026, 3, 1));
        _round.ScheduleOn(_matchDay.Id);
        _matchDays.Setup(repo => repo.GetAsync(_matchDay.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_matchDay);

        _home = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 80);
        _away = Team.Create("Estrela do Norte", "EDN", "#0F5132", "#FFD700", 70);
        AddSquad(_home);
        AddSquad(_away);

        // The tie: the first leg is already behind us and the second leg is this fixture.
        var firstLegFixture = Fixture.Create(_round.Id, _home.Id, _away.Id);
        _fixture = Fixture.Create(_round.Id, _away.Id, _home.Id);
        _tie = CupTie.Create(_competitionSeason.Id, 1, _home.Id, _away.Id);
        _tie.SetLegs(firstLegFixture.Id, _fixture.Id);

        _firstLeg = Match.Create(firstLegFixture.Id, _home.Id, _away.Id);
        _firstLeg.ApplyEngineState(90, FirstLegHome, FirstLegAway, 1);
        _firstLeg.Finish();

        _playing = _fixture;
        _playingTie = _tie;

        _cupTies.Setup(repo => repo.GetByLegAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _playingTie);
        _cupTies.Setup(repo => repo.ListByRoundAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, int round, CancellationToken __) => Task.FromResult<IReadOnlyList<CupTie>>(
                // The other ties of the round, one of which has not been played: a round is
                // only over when every tie in it is, and this one is not over. The test is
                // about the match and the tie, not about the round after it.
                _tiesOf(round)));
        _matches.Setup(repo => repo.GetByFixtureAsync(firstLegFixture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_firstLeg);
        _matches.Setup(repo => repo.GetByFixtureAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid fixtureId, CancellationToken _) => Task.FromResult<Match?>(
                fixtureId == firstLegFixture.Id
                    ? _firstLeg
                    : _started.LastOrDefault(match => match.FixtureId == fixtureId)));
        _matches.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Match?>(
                _started.LastOrDefault(match => match.Id == id)));
        _matches.Setup(repo => repo.AddAsync(It.IsAny<Match>(), It.IsAny<CancellationToken>()))
            .Returns((Match match, CancellationToken _) =>
            {
                _started.Add(match);
                return Task.CompletedTask;
            });
        _matches.Setup(repo => repo.AddEventsAsync(
                It.IsAny<IEnumerable<MatchEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _fixtures.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _playing);
        _teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Team?>(
                id == _home.Id ? _home : id == _away.Id ? _away : null));
        _teams.Setup(repo => repo.GetSquadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, Guid _, CancellationToken __) => Task.FromResult<IReadOnlyList<TeamMembership>>(
                _states
                    .Where(state => state.TeamId == teamId)
                    .Select(state => TeamMembership.Create(state.PlayerId, teamId, new DateOnly(2026, 1, 1)))
                    .ToList()));
        // The contracts a club holds right now, which is where a match reads the number on a
        // player's back. The same book as above, read the way the pitch reads it.
        _teams.Setup(repo => repo.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid teamId, CancellationToken __) => Task.FromResult<IReadOnlyList<TeamMembership>>(
                _states
                    .Where(state => state.TeamId == teamId)
                    .Select(state => TeamMembership.Create(state.PlayerId, teamId, new DateOnly(2026, 1, 1)))
                    .ToList()));
        // The same book read the way a table reads it: the squads of every club at once, and
        // the men behind them at once, rather than a query per club and a query per man.
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
        _players.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Player>>(_roster));
        _players.Setup(repo => repo.ListSeasonStatesAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid teamId, CancellationToken __) => Task.FromResult<IReadOnlyList<PlayerSeasonState>>(
                _states.Where(state => state.TeamId == teamId).ToList()));
        _players.Setup(repo => repo.GetSeasonStateForUpdateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid playerId, Guid _, CancellationToken __) => Task.FromResult<PlayerSeasonState?>(
                _states.FirstOrDefault(state => state.PlayerId == playerId)));

        // The world the match is played in: one round, one matchday, one competition season.
        _rounds.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Round?>(_round));
        _rounds.Setup(repo => repo.ListByMatchDayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Round>>([_round]));
        _fixtures.Setup(repo => repo.ListByRoundAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Fixture>>([_fixture]));
        _fixtures.Setup(repo => repo.ListByRoundIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Fixture>>([_fixture]));
        _competitions.Setup(repo => repo.GetSeasonByIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<CompetitionSeason?>(_competitionSeason));
        _competitions.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Competition?>(null));
    }

    /// <summary>
    /// A club of men for a match: a whole eleven with a keeper in it, and a bench behind
    /// them. The bench is here to be a man who never came on — the pool of a shootout is
    /// the men who played, so a test about the pool needs somebody outside it.
    /// </summary>
    private void AddSquad(Team team)
    {
        foreach (var position in StartingEleven)
        {
            _roster.Add(PlayerOf(team, position, team.ShortName + " Player " + _roster.Count, SquadEnergy));
        }

        for (var bench = 0; bench < BenchSize; bench++)
        {
            _roster.Add(PlayerOf(team, Position.MID, team.ShortName + " Bench " + _roster.Count, SquadEnergy));
        }
    }

    /// <summary>
    /// The ties of this cup: the one being played, and one in the same round that has not
    /// been — a round of sixteen is four ties, and it is not over while one of them waits.
    /// </summary>
    private List<CupTie> _tiesOf(int round) =>
    [
        _tie,
        CupTie.Create(_competitionSeason.Id, round, Guid.NewGuid(), Guid.NewGuid())
    ];

    /// <summary>
    /// A man in this world, in this club's season, with a number on his back.
    /// </summary>
    private Player PlayerOf(Team team, Position position, string name, int energy)
    {
        var player = Player.Create(
            name,
            31,
            position,
            speed: 12,
            accuracy: 12,
            dribbling: 12,
            heading: 12,
            strength: 12,
            goalkeeperPower: position == Position.GK ? 18 : 0,
            reflexes: position == Position.GK ? 16 : 0);

        _states.Add(PlayerSeasonState.Create(player.Id, _seasonId, team.Id, energy));

        return player;
    }

    /// <summary>
    /// The first leg, drawn so that this seed's ninety minutes can leave the aggregate
    /// level. The legs swap ends, so a level aggregate is
    /// <c>first leg home + this away == first leg away + this home</c>, and a first leg
    /// cannot be bent to fit a ninety that cannot be levelled.
    /// </summary>
    private const int FirstLegHome = 1;
    private const int FirstLegAway = 0;

    [Fact]
    public async Task A_second_leg_level_on_the_aggregate_waits_at_the_spot_for_the_managers_order()
    {
        // The manager is the away side, which is the first leg's home club: the club whose
        // goals the aggregate is counted against. He has to be able to name his five, and
        // the match has to stand still until he does.
        var seed = await SeedOfALevellableSecondLegAsync();

        var service = CreateService();
        var started = await service.StartAsync(_fixture.Id, seed, _home.Id, headless: true);

        Assert.True(started.Accepted);
        var matchId = started.MatchId;

        var state = await PlayToTheSpotAsync(service, matchId);

        Assert.NotNull(state.Shootout);
        Assert.True(state.Shootout!.AwaitingOrder);

        // The engine drew the other club's five on its own — nobody is watching it, and a
        // shootout that waited for a manager nobody has is a shootout that never runs — while
        // the manager's own side is still unnamed, and the pool he names from is the men who
        // finished the match.
        var managerIsHome = state.Shootout.HomeTeamId == _home.Id;
        var drawn = managerIsHome ? state.Shootout.AwayTakers : state.Shootout.HomeTakers;
        var unnamed = managerIsHome ? state.Shootout.HomeTakers : state.Shootout.AwayTakers;

        Assert.Equal(5, drawn.Count);
        Assert.Empty(unnamed);
        Assert.NotEmpty(state.Shootout.Candidates);

        // The order the manager names is his own, in the order he names it, and the shootout
        // is no longer waiting once he has.
        var chosen = state.Shootout.Candidates.Take(5).Select(player => player.PlayerId).ToList();
        var named = await service.NameShootoutOrderAsync(matchId, _home.Id, chosen);

        Assert.True(named.Accepted);

        var afterNaming = await service.GetStateAsync(matchId);
        Assert.False(afterNaming.Shootout!.AwaitingOrder);
        Assert.Equal(
            chosen,
            managerIsHome ? afterNaming.Shootout.HomeTakers : afterNaming.Shootout.AwayTakers);

        // And the first kick is taken, which is the point of all of it: a manager who has
        // been standing at the spot watches his men walk up.
        var kicked = await service.TickAsync(matchId);
        Assert.True(kicked.Accepted);
        Assert.Single((await service.GetStateAsync(matchId)).Shootout!.Kicks);
    }

    /// <summary>
    /// A match nobody is watching plays its own penalties out.
    ///
    /// The manager's order is asked for and the engine's is drawn, so a tie decided while
    /// the crowd is watching and a tie decided while the crowd is not have to reach the same
    /// answer by the same road. If the headless match stopped at the spot, every cup tie the
    /// manager is not watching would sit at ninety minutes for ever — and it is the *engine*
    /// that plays the fixtures of a matchday he is not in.
    /// </summary>
    [Fact]
    public async Task A_match_nobody_is_watching_plays_its_own_penalties_out()
    {
        var seed = await SeedOfALevellableSecondLegWith(null);

        var service = CreateService();
        var started = await service.StartAsync(_fixture.Id, seed, headless: true);
        var atTheSpot = await PlayToTheSpotAsync(service, started.MatchId);

        Assert.True(
            atTheSpot.Shootout is { AwaitingOrder: false },
            "Nobody is watching, so nobody has an order to give.");

        // Kick after kick, the same way the loop drives a match nobody is watching, until
        // the tie is decided. Each kick is read out as it is taken, because a shootout the
        // crowd is not told about is a tie decided behind closed doors: the feed is the
        // only account of who took them, and a kick that is not in it did not happen.
        MatchStateView state = atTheSpot;
        var narrated = new List<MatchEngineEvent>();

        for (var kick = 0; kick < WholeMatch; kick++)
        {
            state = await service.GetStateAsync(started.MatchId);

            if (state.IsFinished)
            {
                break;
            }

            var ticked = await service.TickAsync(started.MatchId);

            Assert.True(
                ticked.Accepted,
                $"The match refused a kick with: {ticked.ErrorMessage} "
                + $"(status={state.Status} half={state.Half} paused={state.IsPaused} "
                + $"halfTime={state.IsHalfTime} kicks={state.Shootout?.Kicks.Count})");

            narrated.AddRange(ticked.Events);
        }

        Assert.True(state.IsFinished, "The penalties never finished.");

        var kicks = narrated.Where(e => e.Type == MatchEventType.PenaltyShootoutKick).ToList();
        Assert.NotEmpty(kicks);

        // Every kick in the state is in the feed, and no kick is in the feed twice: the feed
        // is a list of things that happened, not a stream that repeats itself.
        Assert.Equal(narrated.Count(e => e.Type == MatchEventType.PenaltyShootoutKick), kicks.Count);
        Assert.Contains(
            narrated,
            e => e.Type == MatchEventType.MatchFinished);

        // The score the two managers see at the end is the ninety minutes; the shootout is
        // the answer to the tie and not a goal added to it.
        Assert.Equal(atTheSpot.HomeScore, state.HomeScore);
        Assert.Equal(atTheSpot.AwayScore, state.AwayScore);
    }

    [Fact]
    public async Task A_manager_cannot_name_a_man_who_did_not_play()
    {
        var seed = await SeedOfALevellableSecondLegAsync();
        var service = CreateService();
        var started = await service.StartAsync(_fixture.Id, seed, _home.Id, headless: true);
        var state = await PlayToTheSpotAsync(service, started.MatchId);

        // A man from the other club, and a man who never came on: the same refusal, because
        // the pool is what the Laws leave him to choose from and it is not larger.
        var stranger = _roster.First(player => !state.Shootout!.Candidates
            .Any(candidate => candidate.PlayerId == player.Id)).Id;

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.NameShootoutOrderAsync(started.MatchId, _home.Id, new[] { stranger }));
    }

    /// <summary>
    /// Plays the match until it is over or standing at the spot, answering every decision
    /// the manager is asked for along the way — the same answers a manager watching would
    /// give — and hands back the state it stopped on, which is the only way the caller can
    /// read the score the ninety minutes arrived at.
    /// </summary>
    private async Task<MatchStateView> PlayToTheSpotAsync(MatchService service, Guid matchId)
    {
        for (var tick = 0; tick < WholeMatch; tick++)
        {
            var state = await service.GetStateAsync(matchId);

            if (state.IsFinished || state.Shootout is not null)
            {
                return state;
            }

            if (state.IsHalfTime)
            {
                await service.ContinueSecondHalfAsync(matchId);
                continue;
            }

            if (state.Injury.AwaitingSubstitution && state.Injury.PlayerId is { } hurt
                && state.Injury.Team == 2)
            {
                // The away side is the manager's, so the replacement is his to name. The
                // engine covers the other club itself and never asks.
                var replacement = (await service.GetLineupAsync(matchId, _home.Id))
                    .HomeBench
                    .FirstOrDefault(player => !player.SubbedOff && !player.RedCard);

                if (replacement is not null)
                {
                    await service.SubstituteAsync(matchId, _home.Id, hurt, replacement.PlayerId);
                }

                continue;
            }

            if (state.Penalty.AwaitingSelection)
            {
                var taker = state.Penalty.Candidates.FirstOrDefault();
                if (taker is not null)
                {
                    await service.SelectPenaltyTakerAsync(matchId, _home.Id, taker.PlayerId);
                }

                continue;
            }

            await service.TickAsync(matchId);
        }

        throw new InvalidOperationException("The match never reached the end of its ninety minutes.");
    }

    /// <summary>
    /// A seed whose ninety minutes leave the tie level on the aggregate, so that the test
    /// is about the aggregate rule and not about whichever seed happened to draw.
    ///
    /// Every seed is looked for on a throwaway leg of the same tie: the same two clubs, the
    /// same manager, the same ends — because a match with a manager in it is not the same
    /// match as one without him, and a fixture is only ever played once. The seed it hands
    /// back is then played again on the real leg, and it is the same ninety minutes,
    /// because the seed is the whole of what the engine draws from.
    /// </summary>
    private Task<int> SeedOfALevellableSecondLegAsync(Guid? managerTeamId = null) =>
        SeedOfALevellableSecondLegWithAsync(managerTeamId ?? _home.Id);

    /// <summary>
    /// The same search for a match nobody is watching, which is a different match: no
    /// manager means the engine picks a keeper of his own accord, makes his own changes and
    /// gives a score of its own, so the seed that leaves this tie level with a manager in it
    /// is not the seed that leaves it level without one.
    /// </summary>
    private Task<int> SeedOfALevellableSecondLegWithAsync(Guid? managerTeamId) =>
        SeedOfALevellableSecondLegWith(managerTeamId);

    private async Task<int> SeedOfALevellableSecondLegWith(Guid? managerTeamId)
    {
        for (var seed = 1; seed <= TwoHundredSeeds; seed++)
        {
            // Level across is not the score of this leg: the legs swap ends, so the first
            // leg's home goals are added to this leg's away ones, and the first leg's away
            // goals to this leg's home ones. A test that compared the ninety minutes to the
            // ninety minutes would be testing a rule that does not exist.
            var first = Fixture.Create(_round.Id, _home.Id, _away.Id);
            var second = Fixture.Create(_round.Id, _away.Id, _home.Id);
            var tie = CupTie.Create(_competitionSeason.Id, FirstLegRound, _home.Id, _away.Id);
            tie.SetLegs(first.Id, second.Id);

            _playing = second;
            _playingTie = tie;

            var service = CreateService();
            var started = await service.StartAsync(second.Id, seed, managerTeamId, headless: true);
            var state = await PlayToTheSpotAsync(service, started.MatchId);

            _playing = _fixture;
            _playingTie = _tie;

            if (FirstLegHome + state.AwayScore == FirstLegAway + state.HomeScore)
            {
                return seed;
            }
        }

        throw new InvalidOperationException(
            $"No seed in {TwoHundredSeeds} leaves the aggregate level.");
    }

    /// <summary>
    /// How many seeds to try before giving up on finding one that leaves the tie level.
    /// A one-goal margin is a common enough scoreline that a hundred ought to hold one.
    /// </summary>
    private const int TwoHundredSeeds = 200;

    /// <summary>
    /// The round of the cup this tie is in. The round of sixteen is the one that has
    /// another tie waiting, which is what keeps the progression from drawing the next round
    /// under a test that is about the match.
    /// </summary>
    private const int FirstLegRound = 1;

    // The match reads the standing order off the board, and the board reads the calendar off
    // the matchday, so the shared service is built once and handed to both rather than
    // written out twice and left to drift apart.
    private MatchService CreateService()
    {
        var matchday = CreateMatchdayService();

        return new MatchService(
        _matches.Object,
        _teams.Object,
        _players.Object,
        _fixtures.Object,
        _rounds.Object,
        _competitions.Object,
        _sessions,
        new AttendanceContextFactory(
            _rounds.Object,
            _fixtures.Object,
            _competitions.Object,
            new StandingsService(
                _rounds.Object,
                _fixtures.Object,
                _matches.Object,
                _competitions.Object,
                _teams.Object)),
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
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FinanceService>.Instance),
            InboxTestFactory.Create(_teams),
            _teams.Object,
            new Random()),
        matchday,
        new TacticsService(
            Mock.Of<NinjaEleven.Application.Repositories.ITeamMatchPlanRepository>(),
            _teams.Object,
            _fixtures.Object,
            _matches.Object,
            new TeamService(
                _teams.Object,
                _players.Object,
                _seasons.Object,
                _matches.Object,
                Mock.Of<IClubEventRepository>(),
                _unitOfWork.Object),
            matchday,
            _unitOfWork.Object,
            MatchTestContext.Clock),
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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FinanceService>.Instance),
        new SponsorOfferService(
            _sponsors.Object,
            _sponsorContracts.Object,
            _teams.Object,
            _finance.Object,
            _seasons.Object,
            InboxTestFactory.Create(_teams),
            _unitOfWork.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SponsorOfferService>.Instance),
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
        new StandingsService(
            _rounds.Object,
            _fixtures.Object,
            _matches.Object,
            _competitions.Object,
            _teams.Object),
        MatchTestContext.Host,
        MatchTestContext.World(),
        MatchTestContext.Clock,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchService>.Instance);
    }

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
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FinanceService>.Instance),
            InboxTestFactory.Create(_teams),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ScorerPrizeService>.Instance),
        new TransferService(
            Mock.Of<NinjaEleven.Application.Repositories.ITransferRepository>(),
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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransferService>.Instance),
        _unitOfWork.Object,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchdayService>.Instance);
}
