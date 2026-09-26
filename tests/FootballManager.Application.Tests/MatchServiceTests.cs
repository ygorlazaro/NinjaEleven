using FootballManager.Application.Abstractions;
using FootballManager.Application.Matches;
using FootballManager.Application.Repositories;
using FootballManager.Application.Services;
using FootballManager.Domain.Competitions;
using FootballManager.Domain.Enums;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Common;
using FootballManager.Domain.Players;
using FootballManager.Domain.Seasons;
using FootballManager.Domain.Teams;
using Moq;
using Xunit;
using Match = FootballManager.Domain.Matches.Match;

namespace FootballManager.Application.Tests;

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
            new DateOnly(1994, 1, 1),
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
                    new DateOnly(1997, 1, 1),
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
        _players.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Player>>(_roster));
        _players.Setup(repo => repo.ListSeasonStatesAsync(_seasonId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid? teamId, CancellationToken __) => Task.FromResult<IReadOnlyList<PlayerSeasonState>>(
                _states.Where(state => teamId is null || state.TeamId == teamId).ToList()));
        _fixtures.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Fixture?>(_fixture));
        _fixtures.Setup(repo => repo.ListByRoundAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<Fixture>>([_fixture]));
        _rounds.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Round?>(_round));
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

    private MatchService CreateService() => new(
        _matches.Object,
        _teams.Object,
        _players.Object,
        _fixtures.Object,
        _rounds.Object,
        _competitions.Object,
        _sessions,
        _unitOfWork.Object);

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
                new DateOnly(1995, 1, 1),
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
                new DateOnly(1996, 1, 1),
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
    public async Task Start_abandons_a_match_whose_session_was_lost_and_reopens_the_fixture()
    {
        var service = CreateService();
        var chosen = HomeSquadIds().Take(SquadSize).ToList();
        var orphan = Match.Create(_fixture.Id, _home.Id, _away.Id);
        orphan.KickOff(11);
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
    public async Task Substitute_swaps_the_rosterand_counts_the_change()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var lineup = serviceState(matchId).HomeLineup;
        var outgoing = lineup.First(p => p.Position != Position.GK);
        var incoming = serviceState(matchId).HomeBench[0];

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
        state.HomeLineup.First(player => player.Position == Position.GK).SendOff();

        // The engine promotes an outfielder to the goal, so the club is never short of one.
        await service.TickAsync(matchId);
        Assert.Contains(serviceState(matchId).HomeLineup, player => player.KeepsGoal);

        var outgoing = serviceState(matchId).HomeLineup.First(player => player.Position != Position.GK);
        var incoming = serviceState(matchId).HomeBench[0];

        var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, incoming.PlayerId);

        Assert.True(result.Accepted);
        Assert.Contains(serviceState(matchId).HomeLineup, player => player.KeepsGoal);
    }

    [Fact]
    public async Task A_squad_is_returned_in_position_order_and_by_name_inside_it()
    {
        var seasons = new Mock<ISeasonRepository>(MockBehavior.Loose);
        seasons.Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season.Create("Temporada 2026", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)));

        var service = new PlayerService(_players.Object, _teams.Object, seasons.Object);

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
    public async Task Substitute_stops_after_five_changes()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        for (var change = 0; change < MaxSubstitutions; change++)
        {
            var state = serviceState(matchId);
            var outgoing = state.HomeLineup.First(p => p.Position != Position.GK);
            var result = await service.SubstituteAsync(matchId, _home.Id, outgoing.PlayerId, state.HomeBench[0].PlayerId);
            Assert.True(result.Accepted);
        }

        var final = serviceState(matchId);
        var error = await ThrowsDomainAsync(() => service.SubstituteAsync(
            matchId, _home.Id, final.HomeLineup.First(p => p.Position != Position.GK).PlayerId, final.HomeBench[0].PlayerId));

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

        for (var tick = 0; tick < 60 && !serviceState(matchId).HalfTimePauseActive; tick++)
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
    public async Task Tick_advances_one_minute_and_records_the_goals()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var before = serviceState(matchId).HomeScore + serviceState(matchId).AwayScore;
        var result = await service.TickAsync(matchId);

        Assert.True(result.Accepted);
        Assert.Equal(1, serviceState(matchId).Minute);
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

        _players.Verify(repo => repo.UpdateSeasonState(It.IsAny<PlayerSeasonState>()), Times.Exactly(tookPart.Count));
    }

    private async Task<Guid> StartAsync(MatchService service)
    {
        var result = await service.StartAsync(_fixture.Id, 7, _home.Id, HomeSquadIds().Take(SquadSize).ToList());
        Assert.True(result.Accepted);
        return result.MatchId;
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
        // A goalkeeper is the worst taker on the pitch, so he is not the first name the
        // engine offers: outfield players come first, the best of them at the top.
        Assert.Equal(Position.GK, view.Penalty.Candidates[^1].Position);

        var taker = view.Penalty.Candidates.First(candidate => candidate.Position == Position.ATT);
        var taken = await service.SelectPenaltyTakerAsync(matchId, _home.Id, taker.PlayerId);

        Assert.True(taken.Accepted);
        Assert.False(serviceState(matchId).PenaltyAwaitingSelection);
        Assert.Contains(taken.Events, e => e.Type == MatchEventType.PenaltyTaken);

        var after = serviceState(matchId).HomeScore + serviceState(matchId).AwayScore;
        var scored = taken.Events.Any(e => e.Description.Contains("converte", StringComparison.Ordinal));

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

        // The home club is not the one being penalised.
        var wrong = await ThrowsDomainAsync(() => service.SelectPenaltyTakerAsync(
            matchId, _home.Id, view.Penalty.Candidates[0].PlayerId));

        Assert.Equal("PenaltyNotForTeam", wrong.Code);

        var taken = await service.SelectPenaltyTakerAsync(
            matchId, _away.Id, view.Penalty.Candidates[0].PlayerId);

        Assert.True(taken.Accepted);
        Assert.Contains(taken.Events, e => e.Type == MatchEventType.PenaltyTaken);
        Assert.False(serviceState(matchId).PenaltyAwaitingSelection);
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
    public async Task An_injury_sustained_in_a_match_carries_into_the_season()
    {
        var service = CreateService();
        var matchId = await StartAsync(service);

        var state = serviceState(matchId);
        var injured = state.HomeLineup.First(player => player.Position == Position.ATT);
        injured.Injure(Injury.Light);

        for (var minute = 0; minute < 300 && runningState(matchId) is { } running && !running.MatchFinished; minute++)
        {
            await TakePenaltyIfItIsTheManagersTurnAsync(service, matchId);
            var tick = await service.TickAsync(matchId);

            if (!tick.Accepted)
            {
                await service.ContinueSecondHalfAsync(matchId);
            }
        }

        var seasonState = _states.Single(candidate => candidate.PlayerId == injured.PlayerId);

        Assert.Equal(Injury.Light, seasonState.Injury);
        Assert.Equal(2, seasonState.InjuryMatchesRemaining);
        Assert.False(seasonState.IsAvailable);
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

    private MatchState serviceState(Guid matchId)
    {
        Assert.True(_sessions.TryGet(matchId, out var session));
        return session!.State;
    }

    /// <summary>
    /// The running state, or null once the match is over: the service drops the session
    /// when it finishes, which is how a caller knows there is nothing left to tick.
    /// </summary>
    private MatchState? runningState(Guid matchId) =>
        _sessions.TryGet(matchId, out var session) && !session!.State.MatchFinished ? session.State : null;
}
