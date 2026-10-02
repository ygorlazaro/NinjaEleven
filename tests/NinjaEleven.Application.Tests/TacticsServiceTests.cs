using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The board a manager lays his club's next match out on, and the order the kick-off reads
/// off it.
///
/// <para>
/// The tests are about two things that are easy to get quietly wrong: that the order that was
/// written is the order that is read, and that a club whose manager has said nothing still
/// arrives at the pitch. The second one matters because the kick-off is no longer anybody's —
/// the world's schedule opens a match — so a manager who is not there when his club plays
/// still has to get his club out the way he asked.
/// </para>
/// </summary>
public class TacticsServiceTests
{
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<ISeasonRepository> _seasons = new();
    private readonly Mock<IFixtureRepository> _fixtures = new();
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<ITeamMatchPlanRepository> _plans = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMatchDayRepository> _matchDays = new();
    private readonly Mock<ICompetitionRepository> _competitions = new();
    private readonly Mock<IRoundRepository> _rounds = new();

    /// <summary>The calendar a club with nothing left to play is on.</summary>
    public TacticsServiceTests()
    {
        _matchDays
            .Setup(repo => repo.ListBySeasonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // A club that has not played yet, which every board has to be able to answer for. It
        // is asked of the match repository rather than left to Moq's default of null, because
        // null here is not a quiet answer: the board counts the rows it is given.
        _matches
            .Setup(repo => repo.GetTeamHistoryAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _seasonId = Guid.NewGuid();

    private readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private Team? _club;

    [Fact]
    public async Task A_plan_survives_the_round_trip_and_comes_back_the_same()
    {
        var keeper = APlayer("Nelso Borel", Position.GK);
        var squad = AFullSquad();
        var order = squad.Take(MatchRules.SquadSize).Select(player => player.Id).ToList();
        var bench = squad.Skip(MatchRules.SquadSize).Take(3).Select(player => player.Id).ToList();

        var service = CreateService();
        SquadIs(squad);

        // The plan book remembers what was written in it, because a test that checked the
        // row it had just handed the repository proved only that a dictionary works.
        TeamMatchPlan? stored = null;
        _plans
            .Setup(repo => repo.GetAsync(_teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        _plans
            .Setup(repo => repo.AddAsync(It.IsAny<TeamMatchPlan>(), It.IsAny<CancellationToken>()))
            .Callback((TeamMatchPlan plan, CancellationToken _) => stored = plan);

        var saved = await service.SavePlanAsync(_teamId, _seasonId, "4231", order, bench);

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        Assert.Equal("4231", saved.TacticCode);
        Assert.Equal(order, board.Plan!.StarterIds);
        Assert.Equal(bench, board.Plan.BenchIds);
        Assert.Equal("4231", board.Plan.TacticCode);

        // The eleven the manager named is the eleven he gets back, in his order and not in
        // the squad's: an order that came back sorted by position would still be the right
        // men and would not be the same decision.
        Assert.Equal(order, board.Plan.StarterIds);
    }

    [Fact]
    public async Task A_plan_naming_a_man_from_another_club_is_refused()
    {
        var service = CreateService();
        SquadIs(AFullSquad());

        var stranger = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.SavePlanAsync(_teamId, _seasonId, "4231", [stranger], []));

        Assert.Equal("PlayerNotInSquad", error.Code);
    }

    [Fact]
    public async Task A_manager_cannot_name_the_same_man_twice_in_two_different_jobs()
    {
        var service = CreateService();
        SquadIs(AFullSquad());

        var one = APlayer("Um", Position.ATT);
        var two = APlayer("Dois", Position.MID);

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.SavePlanAsync(_teamId, _seasonId, "4231", [one.Id], [one.Id, two.Id]));

        Assert.Equal("PlayerAlreadySelected", error.Code);
    }

    [Fact]
    public async Task A_shape_nobody_recognises_is_refused_with_the_catalogue_in_the_message()
    {
        var service = CreateService();
        SquadIs(AFullSquad());

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.SavePlanAsync(_teamId, _seasonId, "9999", [], []));

        Assert.Equal("UnknownTactic", error.Code);
        // The message carries the codes because the manager is the one who has to fix it,
        // and a refusal that does not say what is acceptable is a question in disguise.
        Assert.Contains("442", error.Message);
    }

    [Fact]
    public async Task A_plan_naming_two_goalkeepers_is_refused()
    {
        var service = CreateService();

        // A club with two keepers in it, which is what a squad looks like whenever a reserve
        // has been signed and the screen lets a manager put him in the eleven.
        var squad = AFullSquad();
        squad.Add(APlayer("GK 99", Position.GK));
        SquadIs(squad);

        var order = squad
            .Where(player => player.Position == Position.GK)
            .Select(player => player.Id)
            .Concat(squad.Where(player => player.Position != Position.GK)
                .Take(MatchRules.SquadSize - 2).Select(player => player.Id))
            .ToList();

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.SavePlanAsync(_teamId, _seasonId, "433", order, []));

        Assert.Equal("GoalkeeperRequired", error.Code);
        // The refusal says how many were named, because "a team needs a goalkeeper" is not an
        // answer to a screen holding two of them.
        Assert.Contains("2", error.Message);
    }

    [Fact]
    public async Task A_manager_who_said_nothing_still_gets_the_shape_his_club_last_played()
    {
        var service = CreateService();

        _plans
            .Setup(repo => repo.GetAsync(_teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamMatchPlan?)null);
        _matches
            .Setup(repo => repo.GetLastTacticCodeAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("352");

        var order = await service.TheOrderAtTheKickOffAsync(_teamId, _seasonId);

        // A club nobody has spoken for goes out the way it has been going out — a habit is a
        // decision the manager took once and never took back, and the fallback is that.
        Assert.Equal("352", order!.TacticCode);
        Assert.Empty(order.StarterIds);
    }

    [Fact]
    public async Task A_manager_who_named_a_shape_but_no_eleven_still_gets_his_shape()
    {
        var service = CreateService();

        _plans
            .Setup(repo => repo.GetAsync(_teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamMatchPlan.Create(_teamId, _seasonId, "541", [], [], _now));

        var order = await service.TheOrderAtTheKickOffAsync(_teamId, _seasonId);

        // Naming a shape is naming a shape. Falling through to the last match played would
        // override a decision the manager made on purpose with one he made eight days ago.
        Assert.Equal("541", order!.TacticCode);
    }

    [Fact]
    public async Task A_manager_who_named_eleven_and_shape_gets_both()
    {
        var service = CreateService();
        var order = Enumerable.Range(0, MatchRules.SquadSize).Select(_ => Guid.NewGuid()).ToList();

        _plans
            .Setup(repo => repo.GetAsync(_teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamMatchPlan.Create(_teamId, _seasonId, "433", order, [], _now));

        var resolved = await service.TheOrderAtTheKickOffAsync(_teamId, _seasonId);

        Assert.Equal("433", resolved!.TacticCode);
        Assert.Equal(order, resolved.StarterIds);
    }

    [Fact]
    public async Task A_board_with_no_fixture_still_reads_the_squad_and_the_plan()
    {
        // The last matchday of a season: there is nothing left to play and the board is
        // still the squad. A screen that emptied itself here would be telling a manager his
        // club has no players when it has twenty-three of them.
        var service = CreateService();
        var squad = AFullSquad();
        SquadIs(squad);
        _plans
            .Setup(repo => repo.GetAsync(_teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamMatchPlan.Create(_teamId, _seasonId, "442", [], [], _now));

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        Assert.Null(board.Next);
        Assert.Null(board.Opponent);
        Assert.Equal(squad.Count, board.Squad.Count);
        Assert.Equal("442", board.Plan!.TacticCode);
    }

    [Fact]
    public async Task A_board_names_the_fixture_it_is_about_and_says_which_pitch()
    {
        var service = CreateService();
        SquadIs(AFullSquad());

        var opponentId = Guid.NewGuid();

        // A calendar with one round left in it, because the board's whole job is to say
        // which fixture it is about and the answer has to come out of the season's own
        // schedule rather than out of a list the screen keeps.
        var fixture = ACalendarWith(opponentId);
        _fixtures
            .Setup(repo => repo.GetAsync(fixture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture);

        _teams
            .Setup(repo => repo.GetAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_club ??= AClub("Renascença", _teamId, 4));
        _teams
            .Setup(repo => repo.GetAsync(opponentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AClub("Adversário", opponentId, 3));

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        // The flag is about the club the board belongs to, and this club is the one at home.
        // The screen prints the two names as a fixture, so it is the opponent's side that is
        // the negation — a board that reported the flag straight through would swap the two
        // clubs and print the fixture about the wrong side of the pitch.
        Assert.True(board.Opponent!.ClubIsAtHome);
        Assert.Equal("Adversário", board.Opponent.Name);
    }

    [Fact]
    public async Task The_squad_on_the_board_is_the_squad_the_club_screen_already_reads()
    {
        var service = CreateService();
        var squad = AFullSquad();
        SquadIs(squad);

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        // Ordered by line, so a manager looking for a centre back is looking in one place
        // rather than hunting through twenty-three names for the fourth defender.
        var positions = board.Squad.Select(row => PositionOrder.Of(row.Position)).ToList();
        var expected = board.Squad
            .Select(row => PositionOrder.Of(row.Position))
            .Distinct()
            .OrderBy(order => order)
            .ToList();

        Assert.Equal(expected, positions.Distinct().OrderBy(order => order));
        Assert.Equal(squad.Select(player => player.Id).Order(), board.Squad.Select(row => row.PlayerId).Order());
    }

    [Fact]
    public async Task A_suspended_man_is_on_the_board_saying_so_and_for_how_long()
    {
        var service = CreateService();
        var squad = AFullSquad();
        var suspended = squad[3];
        SquadIs(squad, (player, state) =>
        {
            if (player.Id == suspended.Id) state.AddSuspension(2);
        });

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        var row = board.Squad.Single(candidate => candidate.PlayerId == suspended.Id);

        // The board refuses him and greys him, which it always did — and now it says why and
        // for how long. A greyed row with no reason is a manager leaving this screen to find
        // out on the club's own page what the board already had.
        Assert.False(row.IsAvailable);
        Assert.Equal(2, row.SuspensionMatches);
        Assert.Equal(0, row.InjuryMatchesRemaining);

        // Only the man who was sent off. The other twenty-two are the same rows they were.
        Assert.All(
            board.Squad.Where(candidate => candidate.PlayerId != suspended.Id),
            candidate =>
            {
                Assert.True(candidate.IsAvailable);
                Assert.Equal(0, candidate.SuspensionMatches);
            });
    }

    [Fact]
    public async Task A_knocked_player_is_on_the_board_saying_so_and_for_how_long()
    {
        var service = CreateService();
        var squad = AFullSquad();
        var hurt = squad[5];
        SquadIs(squad, (player, state) =>
        {
            if (player.Id == hurt.Id) state.AddInjury(Injury.Grave, 3);
        });

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        var row = board.Squad.Single(candidate => candidate.PlayerId == hurt.Id);

        Assert.False(row.IsAvailable);
        Assert.Equal(3, row.InjuryMatchesRemaining);
    }

    [Fact]
    public async Task The_form_is_counted_over_the_rows_the_board_is_showing()
    {
        var service = CreateService();
        SquadIs(AFullSquad());

        _matches
            .Setup(repo => repo.GetTeamHistoryAsync(_teamId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                AResult("Atlético", goalsFor: 2, goalsAgainst: 1),
                AResult("Ferroviária", goalsFor: 0, goalsAgainst: 0),
                AResult("Nautico", goalsFor: 1, goalsAgainst: 2),
                AResult("Operário", goalsFor: 3, goalsAgainst: 0),
                AResult("Palmense", goalsFor: 0, goalsAgainst: 1)
            ]);

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        var summary = board.RecentFormSummary;

        Assert.Equal(5, summary.Played);
        Assert.Equal(2, summary.Wins);
        Assert.Equal(1, summary.Draws);
        Assert.Equal(2, summary.Losses);

        // The two numbers the guide exists to be read for, added up by the service rather than
        // by the screen: a client summing the same five rows is free to count a different five,
        // and then the header and the list under it are two accounts of one week.
        Assert.Equal(6, summary.GoalsFor);
        Assert.Equal(4, summary.GoalsAgainst);
        Assert.Equal(2, summary.GoalDifference);

        // And it is over those rows, not over some longer run the repository happened to hold:
        // the summary is a reading of the list the board sends, never a second question.
        Assert.Equal(board.RecentForm.Count, summary.Played);
    }

    [Fact]
    public async Task A_club_that_has_not_played_has_no_form_rather_than_a_form_of_zero()
    {
        var service = CreateService();
        SquadIs(AFullSquad());

        _matches
            .Setup(repo => repo.GetTeamHistoryAsync(_teamId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var board = await service.GetBoardAsync(_teamId, _seasonId);

        Assert.Empty(board.RecentForm);
        Assert.Equal(RecentFormSummary.None, board.RecentFormSummary);
    }

    private TacticsService CreateService() => new(
        _plans.Object,
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
        CreateMatchdayService(),
        _unitOfWork.Object,
        new FixedClock(_now));

    /// <summary>
    /// A matchday with no days on its calendar, which answers "there is nothing next" for
    /// every club. It is the real service rather than a mock because the methods are not
    /// virtual and a fake of one would only be proving that the fake was called.
    /// </summary>
    private MatchdayService CreateMatchdayService()
    {
        var inbox = InboxTestFactory.Create(_teams);

        var finance = new FinanceService(
            Mock.Of<IFinanceRepository>(),
            _teams.Object,
            _players.Object,
            _fixtures.Object,
            _rounds.Object,
            _matchDays.Object,
            _seasons.Object,
            inbox,
            _unitOfWork.Object,
            NullLogger<FinanceService>.Instance);

        return new MatchdayService(
            _matchDays.Object,
            _rounds.Object,
            _fixtures.Object,
            _competitions.Object,
            _matches.Object,
            new ScorerPrizeService(
                _competitions.Object,
                _players.Object,
                _teams.Object,
                finance,
                inbox,
                NullLogger<ScorerPrizeService>.Instance),
            new TransferService(
                Mock.Of<ITransferRepository>(),
                _teams.Object,
                _players.Object,
                _seasons.Object,
                _competitions.Object,
                _rounds.Object,
                _matches.Object,
                Mock.Of<IFinanceRepository>(),
                inbox,
                new ManagedClubs(),
                _unitOfWork.Object,
                NullLogger<TransferService>.Instance),
            _unitOfWork.Object,
            NullLogger<MatchdayService>.Instance);
    }

    private void SquadIs(IReadOnlyList<Player> squad, Action<Player, PlayerSeasonState>? touch = null)
    {
        _teams
            .Setup(repo => repo.GetAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_club ??= AClub("Renascença", _teamId, 4));

        _seasons
            .Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season.Create(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        _teams
            .Setup(repo => repo.GetSquadAsync(_teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TeamMembership>)squad
                .Select(player => TeamMembership.Create(player.Id, _teamId, new DateOnly(2026, 1, 1)))
                .ToList());

        // The contracts the club holds right now, which is where a match reads the number on a
        // player's back.
        _teams
            .Setup(repo => repo.GetLiveContractsAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TeamMembership>)squad
                .Select(player => TeamMembership.Create(player.Id, _teamId, new DateOnly(2026, 1, 1)))
                .ToList());

        _teams
            .Setup(repo => repo.GetPlayersAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> playerIds, CancellationToken _) =>
                Task.FromResult<Dictionary<Guid, Player>>(squad
                    .Where(player => playerIds.Contains(player.Id))
                    .ToDictionary(player => player.Id)));

        _players
            .Setup(repo => repo.ListSeasonStatesByPlayerIdsAsync(
                _seasonId,
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, IEnumerable<Guid> playerIds, CancellationToken __) =>
                Task.FromResult<IReadOnlyList<PlayerSeasonState>>(squad
                    .Where(player => playerIds.Contains(player.Id))
                    .Select(player =>
                    {
                        var state = PlayerSeasonState.Create(player.Id, _seasonId, _teamId, 90);
                        touch?.Invoke(player, state);
                        return state;
                    })
                    .ToList()));
    }

    /// <summary>
    /// A season with a single round left in it, carrying the fixture that matches the
    /// predicate, and answered through the four repositories the calendar is read from.
    ///
    /// <para>
    /// It is four mocks rather than one because the calendar genuinely is four reads, and a
    /// test that stubbed the calendar as a unit would hold nothing: a change to how the days
    /// and the windows are joined would leave this test green.
    /// </para>
    /// </summary>
    private Fixture ACalendarWith(Guid awayTeamId)
    {
        var day = MatchDay.Create(_seasonId, 2, new DateOnly(2026, 3, 1));
        var view = new CompetitionSeasonView
        {
            Id = Guid.NewGuid(),
            CompetitionId = Guid.NewGuid(),
            SeasonId = _seasonId,
            DivisionId = Guid.NewGuid(),
            Tier = 1,
            CompetitionName = "Brasileirão",
            Type = CompetitionType.League
        };

        var round = Round.Create(view.Id, 2);
        round.ScheduleOn(day.Id);

        var fixture = Fixture.Create(round.Id, _teamId, awayTeamId);

        _matchDays
            .Setup(repo => repo.ListBySeasonAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([day]);
        _matchDays
            .Setup(repo => repo.GetAsync(day.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(day);

        _competitions
            .Setup(repo => repo.ListSeasonViewsAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([view]);

        _rounds
            .Setup(repo => repo.ListByCompetitionSeasonIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([round]);

        _fixtures
            .Setup(repo => repo.ListByRoundIdsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([fixture]);

        // The day is asked about its own waves a second time, when the window reports whether
        // it is the one playing, and it is asked through the matchday's own readers.
        _rounds
            .Setup(repo => repo.ListByMatchDayAsync(day.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([round]);

        return fixture;
    }

    private static Player APlayer(string name, Position position) => Player.Create(
        $"{name} {Random.Shared.Next(1_000, 9_999)}",
        29,
        position,
        speed: 12,
        accuracy: 12,
        dribbling: 12,
        heading: 12,
        strength: 12,
        goalkeeperPower: position == Position.GK ? 18 : 0,
        reflexes: position == Position.GK ? 16 : 0);

    /// <summary>
    /// A squad with a whole team in it, so an order of eleven can be laid without the
    /// service refusing for want of men.
    /// </summary>
    private static List<Player> AFullSquad()
    {
        var positions = new[]
        {
            Position.GK,
            Position.DEF, Position.DEF, Position.DEF,
            Position.MID, Position.MID, Position.MID, Position.MID,
            Position.ATT, Position.ATT, Position.ATT,
            Position.MID, Position.MID, Position.MID, Position.DEF, Position.ATT, Position.ATT
        };

        return positions
            .Select((position, index) => APlayer($"{position} {index}", position))
            .ToList();
    }

    private static Team AClub(string name, Guid id, int rating) =>
        Team.Create(name, name[..3].ToUpperInvariant(), "#000000", "#FFFFFF", rating);

    /// <summary>
    /// One finished match as the club's own history reads it: the two goals already in the
    /// club's order rather than the fixture's, which is what the form is counted from.
    /// </summary>
    private TeamMatchRecord AResult(string opponent, int goalsFor, int goalsAgainst) =>
        new()
        {
            MatchId = Guid.NewGuid(),
            TeamId = _teamId,
            OpponentName = opponent,
            OpponentTeamId = Guid.NewGuid(),
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
            TacticCode = "442",
            OpponentTacticCode = "352"
        };

    /// <summary>A clock that stands still, so a plan's age is a fact rather than a race.</summary>
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}