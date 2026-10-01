using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Seasons;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What the world does when it is asked to play a window it has already been asked to play.
///
/// <para>
/// These are the tests the whole scheduler rests on. A scheduled job is a thing that fires
/// again after a restart, after a misfire, after a deploy, and — because the world is
/// deliberately allowed to have more than one process in it — sometimes at the same instant
/// as another one. None of those may produce a second copy of a match that has already been
/// played, and none of them may leave a window permanently open either.
/// </para>
///
/// <para>
/// The claim is faked here. What is under test is the Application's use of it: that a refused
/// window costs nothing, that a window held by somebody else is left alone, that a window left
/// half played is finished rather than restarted, and that a fixture that throws takes only
/// itself down. The claim itself is a row lock in PostgreSQL, and its correctness is the
/// database's.
/// </para>
/// </summary>
public class CompetitionExecutionServiceTests
{
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ISeasonCloser> _closer = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<IHeadlessMatchPlayer> _player = new(MockBehavior.Loose);
    private readonly Mock<IMatchCleaner> _cleaner = new(MockBehavior.Loose);
    private readonly Mock<IManagedClubReader> _managedClubs = new(MockBehavior.Loose);
    private readonly Mock<ISeasonCalendarBuilder> _calendarBuilder = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _matchDayId = Guid.NewGuid();
    private readonly Guid _editionId = Guid.NewGuid();

    private Round _round = null!;
    private readonly List<Fixture> _window = new();

    /// <summary>The season a hand walks, its days, its editions and the windows played on them.</summary>
    private Season _season = null!;
    private readonly List<MatchDay> _days = new();
    private readonly List<Round> _calendar = new();
    private readonly List<CompetitionSeasonView> _views = new();
    private readonly Dictionary<Guid, List<Fixture>> _fixturesByRound = new();

    public CompetitionExecutionServiceTests()
    {
        _round = Round.Create(_editionId, 1);
        _round.ScheduleOn(_matchDayId);

        for (var fixture = 0; fixture < 4; fixture++)
        {
            _window.Add(Fixture.Create(_round.Id, Guid.NewGuid(), Guid.NewGuid()));
        }

        _fixtures.Setup(repository => repository.ListByRoundAsync(_round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_window);

        _rounds.Setup(repository => repository.GetAsync(_round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_round);

        // No match under any fixture: the reconciliation pass must not conclude that a fixture
        // is already decided just because nobody has written a match for it yet.
        _matches.Setup(repository => repository.ListByFixtureIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Match>());

        // A world of nobody: no club has a person behind it, so a walk named no club plays
        // every fixture. A test that needs a manager says so through ListManagedClubsAsync.
        _managedClubs.Setup(reader => reader.ListManagedClubsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
    }

    private CompetitionExecutionService CreateService(
        Func<Guid, Task<HeadlessMatchResult>>? play = null) =>
        BuildWithStore(new InMemoryRoundExecutionStore(_round, _window), MatchTestContext.World(), play);

    private CompetitionExecutionService BuildWithStore(
        IRoundExecutionStore store,
        IOptions<WorldExecutionOptions> options,
        Func<Guid, Task<HeadlessMatchResult>>? play = null)
    {
        if (play is not null)
        {
            _player.Setup(player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid fixtureId, CancellationToken _) => play(fixtureId));
        }
        else
        {
            _player.Setup(player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid fixtureId, CancellationToken _) => new HeadlessMatchResult(
                    true, Guid.NewGuid(), MatchRefusal.None, null, 2, 1));
        }

        return new CompetitionExecutionService(
            MatchTestContext.Clock,
            MatchTestContext.Host,
            _matchDays.Object,
            _competitions.Object,
            _rounds.Object,
            _fixtures.Object,
            _matches.Object,
            _seasons.Object,
            store,
            _player.Object,
            _cleaner.Object,
            _unitOfWork.Object,
            _closer.Object,
            _managedClubs.Object,
            _calendarBuilder.Object,
            BuildTheStatementService(),
            options,
            NullLogger<CompetitionExecutionService>.Instance);
    }

    /// <summary>
    /// The real statement service over loose mocks, so a test that walks a window can reach
    /// the treasurer without standing up a week of football behind it. The managed-club
    /// reader returns nothing here, so it never reaches the book.
    /// </summary>
    private StatementService BuildTheStatementService()
    {
        var messages = new Mock<IInboxMessageRepository>(MockBehavior.Loose);
        var inbox = new InboxService(
            messages.Object,
            _teams.Object,
            new ManagedClubs(),
            _unitOfWork.Object,
            NullLogger<InboxService>.Instance);

        return new StatementService(
            _finance.Object,
            _teams.Object,
            inbox,
            NullLogger<StatementService>.Instance);
    }

    [Fact]
    public async Task A_window_that_has_been_played_is_not_played_again()
    {
        // Played means every fixture of it finished, so a window that really was played is
        // never walked into again — or the world would replay the season for ever.
        foreach (var fixture in _window)
        {
            fixture.MarkFinished();
        }

        _round.CompleteExecution();
        var service = CreateService();

        var run = await service.PlayRoundAsync(_round.Id);

        Assert.Equal(RoundClaim.AlreadyPlayed, run.Claim);
        Assert.Equal(0, run.Played);
        _player.Verify(player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_window_a_manager_finished_by_hand_is_not_offered_to_the_scheduler()
    {
        // Finished through the last fixture rather than through the claim, which is what a
        // round a person played looks like from here.
        foreach (var fixture in _window)
        {
            fixture.MarkFinished();
        }

        _round.Complete();
        var service = CreateService();

        var run = await service.PlayRoundAsync(_round.Id);

        Assert.Equal(RoundClaim.AlreadyPlayed, run.Claim);
        _player.Verify(player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_poll_releases_the_matches_holding_decided_fixtures_even_when_nothing_is_due()
    {
        // A match left on the pitch of a decided fixture holds that fixture, and nothing else
        // would ever ask about it. So the release is asked for on every poll: a world whose
        // last matchday has been played has no window left to walk, and a sweep that only ran
        // inside a walk would never run again.
        _matchDays.Setup(repository => repository.ListUntilAsync(
                It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay>());

        var runs = await CreateService().PlayDueRoundsAsync(CompetitionType.League);

        Assert.Empty(runs);
        _cleaner.Verify(
            cleaner => cleaner.AbandonMatchesOnDecidedFixturesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_window_releases_the_matches_holding_its_fixtures_before_it_plays_a_single_one()
    {
        _round.TryBeginExecution(MatchTestContext.Now, TimeSpan.FromMinutes(30));
        _round.CompleteExecution();

        var run = await CreateService().PlayRoundAsync(_round.Id);

        _cleaner.Verify(
            cleaner => cleaner.AbandonMatchesOnDecidedFixturesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(run.IsComplete);
    }

    [Fact]
    public async Task A_window_closed_over_a_fixture_nobody_played_is_played_rather_than_lost()
    {
        // The matchday that must not be lost. The window says it was played, one of its four
        // fixtures is still on the schedule, and a window believed on its word here is a
        // matchday the world never offers again — a hole in the season that nothing comes back
        // to fill, because by the time anybody noticed the claim had been taken.
        _round.TryBeginExecution(MatchTestContext.Now, TimeSpan.FromMinutes(30));
        _round.CompleteExecution();

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(RoundClaim.Claimed, run.Claim);
        Assert.Equal(4, run.Played);
        Assert.True(run.IsComplete);
    }

    [Fact]
    public async Task A_poll_puts_the_lost_fixtures_back_on_the_schedule_before_it_closes_any_orphan()
    {
        // The order is the argument. The orphan sweep believes a decided fixture was decided by
        // a match that reached the final whistle, and that is only true once the fixtures that
        // were closed over a matchday nobody played have been put back. Run the other way round
        // the sweep reads the same broken column and closes an orphan above the hole, which is
        // how the second division's six missing results became permanent.
        _matchDays.Setup(repository => repository.ListUntilAsync(
                It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay>());

        var repaired = false;
        _cleaner.Setup(cleaner => cleaner.ReopenTheFixturesNobodyDecidedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                repaired = true;
                return 6;
            });
        _cleaner.Setup(cleaner => cleaner.AbandonMatchesOnDecidedFixturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Assert.True(repaired, "a fixture closed over a hole must be reopened before the hole is read as settled");
                return 0;
            });

        await CreateService().PlayDueRoundsAsync(CompetitionType.League);

        _cleaner.Verify(
            cleaner => cleaner.ReopenTheFixturesNobodyDecidedAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_window_closed_over_a_fixture_nobody_played_goes_back_on_the_schedule()
    {
        // Being played is not the same as being playable again: the closure has to be undone,
        // or the next poll finds the same row and believes the same columns again.
        _round.TryBeginExecution(MatchTestContext.Now, TimeSpan.FromMinutes(30));
        _round.CompleteExecution();

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.True(run.IsComplete);
        Assert.True(_round.HasBeenExecuted);
    }

    [Fact]
    public async Task A_window_closed_over_a_fixture_nobody_played_is_still_offered_by_the_calendar()
    {
        // The same matchday, seen from the other end. Filtering the calendar on the window's
        // own columns is what loses it: the job asks what is due, the window is not in the
        // answer, and nothing that happens afterwards can bring it back. The question the world
        // asks is "what is still owed", and a window with a fixture on the schedule is owed.
        GivenACalendarHolding(_round);
        _round.TryBeginExecution(MatchTestContext.Now, TimeSpan.FromMinutes(30));
        _round.CompleteExecution();

        var due = await CreateService().GetDueRoundsAsync(CompetitionType.League);

        Assert.Contains(due, round => round.RoundId == _round.Id);
    }

    [Fact]
    public async Task A_window_that_really_was_played_is_not_offered_again()
    {
        // The other end of the same rule. A matchday played whole is not owed to anybody, and
        // a calendar that offered it would replay the season for ever.
        GivenACalendarHolding(_round);

        foreach (var fixture in _window)
        {
            fixture.MarkFinished();
        }

        _round.TryBeginExecution(MatchTestContext.Now, TimeSpan.FromMinutes(30));
        _round.CompleteExecution();

        var due = await CreateService().GetDueRoundsAsync(CompetitionType.League);

        Assert.DoesNotContain(due, round => round.RoundId == _round.Id);
    }

    [Fact]
    public async Task A_window_being_played_right_now_is_not_offered_a_second_time()
    {
        // The window the world is playing this very minute. The claim refuses it anyway, but
        // the calendar does not put a window somebody is on the pitch for in the list of things
        // the world owes.
        GivenACalendarHolding(_round);
        _round.TryBeginExecution(MatchTestContext.Now, TimeSpan.FromMinutes(30), "scheduler-1");

        var due = await CreateService().GetDueRoundsAsync(CompetitionType.League);

        Assert.DoesNotContain(due, round => round.RoundId == _round.Id);
    }

    /// <summary>
    /// Puts one window on a calendar whose only day is due, so the due question has something
    /// to answer and the answer is about this window.
    /// </summary>
    private void GivenACalendarHolding(Round round)
    {
        var matchDay = MatchDay.Create(_seasonId, 1, DateOnly.FromDateTime(MatchTestContext.Now.UtcDateTime));
        round.ScheduleOn(matchDay.Id);

        _matchDays.Setup(repository => repository.ListUntilAsync(
                It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay> { matchDay });

        _competitions.Setup(repository => repository.ListSeasonViewsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<CompetitionSeasonView>>
            {
                [_seasonId] = new List<CompetitionSeasonView>
                {
                    new()
                    {
                        Id = _editionId,
                        CompetitionId = Guid.NewGuid(),
                        SeasonId = _seasonId,
                        CompetitionName = "Brasileirão",
                        Type = CompetitionType.League
                    }
                }
            });

        _rounds.Setup(repository => repository.ListByCompetitionSeasonIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { round });

        _fixtures.Setup(repository => repository.ListByRoundIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_window);
    }

    [Fact]
    public async Task A_window_being_played_by_another_process_is_left_to_it()
    {
        var store = new InMemoryRoundExecutionStore(_round, _window);
        var now = MatchTestContext.Now;
        store.Force(round => round.TryBeginExecution(now.AddMinutes(-1), TimeSpan.FromMinutes(30)));

        var run = await BuildWithStore(store, MatchTestContext.World()).PlayRoundAsync(_round.Id);

        Assert.Equal(RoundClaim.HeldByAnotherProcess, run.Claim);
        _player.Verify(player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Every_fixture_of_a_window_is_played()
    {
        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(RoundClaim.Claimed, run.Claim);
        Assert.Equal(4, run.Played);
        Assert.True(run.IsComplete);
        _player.Verify(
            player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task A_fixture_that_was_played_before_the_restart_is_not_played_again()
    {
        // Scenario C: the process went down with two of the window's four fixtures already
        // decided. Those two are finished, so the run that comes back finishes the other two
        // and calls the window complete.
        _window[0].MarkFinished();
        _window[1].MarkFinished();

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(2, run.AlreadyPlayed);
        Assert.Equal(2, run.Played);
        Assert.True(run.IsComplete);

        _player.Verify(
            player => player.PlayAsync(It.Is<Guid>(id => id == _window[0].Id), It.IsAny<CancellationToken>()),
            Times.Never);
        _player.Verify(
            player => player.PlayAsync(It.Is<Guid>(id => id == _window[3].Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_fixture_that_throws_does_not_take_the_fixtures_around_it_down()
    {
        var second = _window[1].Id;

        var run = await CreateService(fixtureId => fixtureId == second
            ? throw new InvalidOperationException("the database said no")
            : Task.FromResult(new HeadlessMatchResult(true, Guid.NewGuid(), MatchRefusal.None, null, 1, 0)))
            .PlayRoundAsync(_round.Id);

        // The other three were played and keep their results...
        Assert.Equal(3, run.Played);
        Assert.Equal(1, run.Failed);

        // ...and the window is not closed over the hole, so the next run comes back to it.
        Assert.False(run.IsComplete);
    }

    [Fact]
    public async Task A_window_is_only_completed_when_every_fixture_is_finished()
    {
        var run = await CreateService(fixtureId => Task.FromResult(new HeadlessMatchResult(
            false, Guid.NewGuid(), MatchRefusal.PlayedByAnotherProcess, "held elsewhere")))
            .PlayRoundAsync(_round.Id);

        Assert.Equal(4, run.PlayedElsewhere);
        Assert.False(run.IsComplete);
        Assert.False(_round.HasBeenExecuted);
    }

    [Fact]
    public async Task A_match_somebody_else_is_playing_does_not_stop_the_window_being_retried()
    {
        // The window stays claimable after a failure rather than being held for the length of
        // the lease by a process that has already given up on it, so the next run of the
        // window — and not the one after the lease runs out — is what finishes it.
        var store = new InMemoryRoundExecutionStore(_round, _window);
        var service = BuildWithStore(
            store,
            MatchTestContext.World(),
            fixtureId => Task.FromResult(new HeadlessMatchResult(
                false, Guid.NewGuid(), MatchRefusal.PlayedByAnotherProcess, "held elsewhere")));

        var run = await service.PlayRoundAsync(_round.Id);

        Assert.False(run.IsComplete);
        Assert.Equal(1, store.ClaimsTaken);

        // The window says it is nobody's again, so the next run may take it rather than
        // waiting out a lease held by a process that has already walked away from it.
        Assert.Equal(RoundExecutionStatus.Scheduled, _round.ExecutionStatus);
        Assert.Null(_round.ExecutionStartedAt);
    }

    [Fact]
    public async Task A_fixture_the_database_had_already_decided_is_marked_finished_rather_than_played()
    {
        // The reconciliation pass: a fixture left "in progress" above a match that reached
        // full time, because the process died between committing the two.
        var decided = _window[0];
        decided.MarkInProgress();

        var played = Match.Create(decided.Id, Guid.NewGuid(), Guid.NewGuid());
        played.Finish();

        _matches.Setup(repository => repository.ListByFixtureIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match> { played });

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.True(decided.Status is FixtureStatus.Finished);
        Assert.Equal(1, run.AlreadyPlayed);
        _player.Verify(
            player => player.PlayAsync(It.Is<Guid>(id => id == decided.Id), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_fixture_whose_match_was_abandoned_is_played_again_rather_than_closed_over()
    {
        // The hole this guards: "is finished" in the domain is also true of a match that was
        // given up on, and a reconciliation pass that reads it that way marks the fixture
        // finished over a match that never reached full time — so the window counts itself
        // complete and the calendar carries on with a matchday that was never played. The
        // fixture of an abandoned match is owed, exactly like a fixture nobody started.
        var owed = _window[0];

        var abandoned = Match.Create(owed.Id, Guid.NewGuid(), Guid.NewGuid());
        abandoned.Abandon();

        _matches.Setup(repository => repository.ListByFixtureIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match> { abandoned });

        var run = await CreateService().PlayRoundAsync(_round.Id);

        // It is played, not skipped: the abandoned match is not a decision, so the fixture is
        // still on the schedule and the window is played out rather than closed over a match
        // that never reached full time.
        Assert.Equal(4, run.Played);
        Assert.Equal(0, run.AlreadyPlayed);
        _player.Verify(
            player => player.PlayAsync(It.Is<Guid>(id => id == owed.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_fixture_somebody_is_playing_right_now_is_left_to_them()
    {
        // The cup window opens every fixture of it at once, so by the time the walk that
        // plays it arrives the day is already on the pitch. The walk used to take those
        // matches over — abandon, begin again, under whoever was watching — and a matchday
        // that leaves four abandoned matches behind every fixture it played is a matchday
        // whose results are a row of second attempts. One match, one driver.
        var beingPlayed = _window[0];
        beingPlayed.MarkInProgress();

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(3, run.Played);
        Assert.Equal(1, run.PlayedElsewhere);

        // The window stays owed rather than closing over a match that is still on the pitch.
        Assert.False(run.IsComplete);
        Assert.Equal(RoundExecutionStatus.Scheduled, _round.ExecutionStatus);

        _player.Verify(
            player => player.PlayAsync(It.Is<Guid>(id => id == beingPlayed.Id), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_match_left_on_the_touchline_that_nobody_touched_is_taken_back_and_played()
    {
        // The world leaves the manager's own match open and waits for him. If he never comes,
        // a world that waited for ever would answer every advance with the same nothing — the
        // same window, the same fixture, the same 0.01 seconds — which is indistinguishable
        // from a broken route. A match still at minute zero with nobody behind it belongs to
        // nobody, so the fixture goes back on the schedule and the day is played out.
        var untouched = _window[0];
        untouched.MarkInProgress();

        _cleaner.Setup(cleaner => cleaner.ReleaseTheUntouchedMatchAsync(
                untouched.Id, It.IsAny<CancellationToken>()))
            .Callback(() => untouched.Reopen())
            .ReturnsAsync(true);

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(4, run.Played);
        Assert.Equal(0, run.PlayedElsewhere);
        Assert.True(run.IsComplete);

        _player.Verify(
            player => player.PlayAsync(untouched.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task The_managers_own_fixture_is_started_and_left_for_him_rather_than_simulated()
    {
        // A manager who cannot watch his own game being played for him is not playing the
        // game. The window opens his fixture — a real kick-off, a real session — and stops:
        // the loop leaves that clock alone, the manager's screen claims the match, and the
        // window closes on the match he finished. Everything else in the day is played out.
        var hisClub = Guid.NewGuid();
        var fixture = Fixture.Create(_round.Id, hisClub, Guid.NewGuid());
        _window.Add(fixture);

        _player.Setup(player => player.StartAndLeaveAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeadlessMatchResult(true, Guid.NewGuid(), MatchRefusal.None, null));

        var run = await CreateService().PlayRoundAsync(_round.Id, hisClub);

        Assert.Equal(1, run.LeftForTheManager);
        Assert.Equal(4, run.Played);
        Assert.False(run.IsComplete);

        _player.Verify(
            player => player.StartAndLeaveAsync(fixture.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _player.Verify(
            player => player.PlayAsync(fixture.Id, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_world_that_knows_who_is_playing_leaves_that_club_alone_without_being_told()
    {
        // A club somebody is in charge of is a fact about the world, not about the call: the
        // scheduler walking the same season is walking it for the same man, so the walk asks
        // the world whose matches are not its to play. A caller that names a club adds to the
        // answer rather than replacing it.
        var hisClub = Guid.NewGuid();
        var fixture = Fixture.Create(_round.Id, hisClub, Guid.NewGuid());
        _window.Add(fixture);

        _managedClubs.Setup(reader => reader.ListManagedClubsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { hisClub });

        _player.Setup(player => player.StartAndLeaveAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeadlessMatchResult(true, Guid.NewGuid(), MatchRefusal.None, null));

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(1, run.LeftForTheManager);
        Assert.Equal(4, run.Played);
        Assert.False(run.IsComplete);
    }

    [Fact]
    public async Task A_world_walked_without_a_manager_simulates_his_fixture_like_any_other()
    {
        // The same window, walked by the scheduler or by a hand that named no club: there is
        // no manager to hand the keyboard to, so his fixture is played like every other one.
        var hisClub = Guid.NewGuid();
        var fixture = Fixture.Create(_round.Id, hisClub, Guid.NewGuid());
        _window.Add(fixture);

        var run = await CreateService().PlayRoundAsync(_round.Id);

        Assert.Equal(5, run.Played);
        Assert.Equal(0, run.LeftForTheManager);
        _player.Verify(
            player => player.PlayAsync(fixture.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Two_processes_asking_for_the_same_window_at_the_same_time_produce_one_run()
    {
        var store = new InMemoryRoundExecutionStore(_round, _window);
        var options = MatchTestContext.World();

        var first = BuildWithStore(store, options);
        var second = BuildWithStore(store, options);

        // The loser has to ask while the winner is still walking the window. The real store
        // gets that from the row lock; a fake that let the walk finish first would hand the
        // second process a window that is already played, which is a different answer from
        // the one under test and one that depends on how the two tasks happened to
        // interleave — a coin in the suite rather than a test of anything. The gate is set up
        // after the services are built, because the last setup of a mock is the one that runs.
        var walking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mayFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _player.Setup(player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, CancellationToken _) =>
            {
                walking.TrySetResult();
                await mayFinish.Task;

                return new HeadlessMatchResult(true, Guid.NewGuid(), MatchRefusal.None, null, 2, 1);
            });

        var winner = first.PlayRoundAsync(_round.Id);
        await walking.Task;

        var loser = await second.PlayRoundAsync(_round.Id);
        mayFinish.TrySetResult();

        var runs = new[] { await winner, loser };

        Assert.Single(runs.Where(run => run.Claim is RoundClaim.Claimed));
        Assert.Single(runs.Where(run => run.Claim is RoundClaim.HeldByAnotherProcess));

        // And the match was played once, not once per process that asked.
        _player.Verify(
            player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    /// <summary>
    /// The world a hand walks: a season with its days, the editions of its competitions, and the
    /// windows a person has put on them. Everything the advance reads is read from here, and a
    /// test that wants a day with a cup leg in it says so rather than describing it.
    /// </summary>
    private Season GivenASeasonWith(params int[] daysWithAChampionshipRound)
    {
        var start = new DateOnly(2026, 3, 1);
        var season = Season.Create(
            1,
            start,
            start.AddDays(CompetitionRules.SeasonMatchDays - 1));
        season.Start();

        _season = season;
        _days.AddRange(Enumerable.Range(1, CompetitionRules.SeasonMatchDays)
            .Select(number => MatchDay.Create(season.Id, number, start.AddDays(number - 1))));

        _views.AddRange(new[]
        {
            AView(CompetitionType.League, tier: 1),
            AView(CompetitionType.League, tier: 2),
            AView(CompetitionType.Cup, tier: null)
        });

        _seasons.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Season> { season });

        _matchDays.Setup(repository => repository.ListBySeasonAsync(
                season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_days);

        _competitions.Setup(repository => repository.ListSeasonViewsAsync(
                season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_views);

        _rounds.Setup(repository => repository.ListByCompetitionSeasonIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_calendar);

        _rounds.Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _calendar.FirstOrDefault(round => round.Id == id));

        _fixtures.Setup(repository => repository.ListByRoundIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids
                .SelectMany(id => _fixturesByRound.GetValueOrDefault(id) ?? new List<Fixture>())
                .ToList());

        _fixtures.Setup(repository => repository.ListByRoundAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Fixture>>(
                    _fixturesByRound.GetValueOrDefault(id) ?? new List<Fixture>()));

        foreach (var day in daysWithAChampionshipRound)
        {
            foreach (var view in _views.Where(view => view.Type is CompetitionType.League))
            {
                AddAWindow(view.Id, CompetitionRules.ChampionshipWindow, day, day);
            }
        }

        return season;
    }

    /// <summary>Puts a cup leg on a day: the window number is the one the calendar gives it.</summary>
    private void GivenACupWindowOn(int day, int cupWindowNumber) =>
        AddAWindow(_views.Single(view => view.Type is CompetitionType.Cup).Id,
            CompetitionRules.CupWindow, day, cupWindowNumber);

    private CompetitionSeasonView AView(CompetitionType type, int? tier) => new()
    {
        Id = Guid.NewGuid(),
        CompetitionId = Guid.NewGuid(),
        SeasonId = _season?.Id ?? _seasonId,
        DivisionId = tier is null ? null : Guid.NewGuid(),
        CompetitionName = type.ToString(),
        Type = type,
        Tier = tier
    };

    private void AddAWindow(Guid editionId, int window, int day, int roundNumber)
    {
        var matchDay = _days.Single(candidate => candidate.Number == day);
        var round = Round.Create(editionId, roundNumber, window);
        round.ScheduleOn(matchDay.Id);
        _calendar.Add(round);

        var fixtures = new List<Fixture>();
        for (var fixture = 0; fixture < 2; fixture++)
        {
            fixtures.Add(Fixture.Create(round.Id, Guid.NewGuid(), Guid.NewGuid()));
        }

        _fixturesByRound[round.Id] = fixtures;
    }

    /// <summary>
    /// The service a hand walks the world with, over the calendar <c>GivenASeasonWith</c> built.
    ///
    /// The player marks each fixture finished as it plays it, because that is what the match
    /// service does when a match reaches full time — and without it the world's own record of
    /// what has been played would never move and every advance would offer the same window.
    ///
    /// <para>
    /// It goes in as <c>BuildWithStore</c>'s own player rather than as a second setup of the
    /// mock. The builder sets the mock up itself, and Moq answers with the last matching setup,
    /// so a setup made here was quietly overwritten by the builder's and the marking never ran
    /// — a test world where no fixture was ever played, which is exactly why the walk used to
    /// decide a window from the window's own row: with nothing marking anything finished, only
    /// the row could tell it what had been done.
    /// </para>
    /// </summary>
    private CompetitionExecutionService CreateAdvanceService()
    {
        var store = new InMemoryRoundExecutionStore(
            _calendar.ToDictionary(round => round.Id),
            id => _fixturesByRound.GetValueOrDefault(id) ?? new List<Fixture>());

        return BuildWithStore(
            store,
            MatchTestContext.World(),
            fixtureId =>
            {
                _fixturesByRound.Values
                    .SelectMany(fixtures => fixtures)
                    .First(fixture => fixture.Id == fixtureId)
                    .MarkFinished();

                return Task.FromResult(
                    new HeadlessMatchResult(true, Guid.NewGuid(), MatchRefusal.None, null, 1, 0));
            });
    }

    [Fact]
    public async Task A_walk_draws_the_calendar_of_the_season_it_is_walking()
    {
        // A season nobody has drawn has no matchdays at all, so the walk found nothing to do
        // and answered "nothing to play" about a season with thirty-four days of football in
        // it. The season a close opens is the worst case: the close draws the next pyramid and
        // leaves the calendar of the new season to whoever noticed, and the Supercup lives on
        // that first day's calendar — so a world that never drew it had no Supercup at all.
        GivenASeasonWith(2);

        var first = await CreateAdvanceService().AdvanceTheWorldAsync();

        _calendarBuilder.Verify(
            builder => builder.DrawAsync(_season.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(2, first.MatchDayNumber);
    }

[Fact]
    public async Task A_hand_walks_the_world_a_window_at_a_time_from_the_first_day_with_football_in_it()
    {
        // Day one is the Supercup's and a first season has none, so the first advance of a new
        // world is the first round of the divisions on day two — and the one after it is day
        // three. A world that jumped from the first round to the fourth would be offering the
        // same window twice, or offering a day the calendar has already passed.
        GivenASeasonWith(2, 3, 4);
        var service = CreateAdvanceService();

        var first = await service.AdvanceTheWorldAsync();

        Assert.Equal(WorldAdvanceKind.Window, first.Kind);
        Assert.Equal(2, first.MatchDayNumber);
        Assert.Equal(CompetitionType.League, first.Wave);
        Assert.Equal("Temporada I", first.SeasonName);

        // Every division's round is one window of one day: two rounds, not one, and not four.
        Assert.Equal(2, first.Rounds.Count);
        Assert.All(first.Rounds, run => Assert.Equal(RoundClaim.Claimed, run.Claim));

        var second = await service.AdvanceTheWorldAsync();
        Assert.Equal(3, second.MatchDayNumber);
        Assert.Equal(CompetitionType.League, second.Wave);

        var third = await service.AdvanceTheWorldAsync();
        Assert.Equal(4, third.MatchDayNumber);
    }

    [Fact]
    public async Task A_day_with_a_cup_leg_in_it_is_two_advances_and_the_round_goes_first()
    {
        // The seventh day carries the sixth round of the divisions and the 32-avos of the cup.
        // They are six hours apart, so they are two windows and two advances — and the round is
        // first, because a cup leg taken by a side that has not played its league game is a leg
        // taken by a club that is not there yet.
        GivenASeasonWith(7, 8);
        GivenACupWindowOn(7, CompetitionRules.CupWindowNumber(1, 1));

        var service = CreateAdvanceService();

        var round = await service.AdvanceTheWorldAsync();
        Assert.Equal(7, round.MatchDayNumber);
        Assert.Equal(CompetitionType.League, round.Wave);
        Assert.Equal(2, round.Rounds.Count);

        var leg = await service.AdvanceTheWorldAsync();
        Assert.Equal(7, leg.MatchDayNumber);
        Assert.Equal(CompetitionType.Cup, leg.Wave);
        Assert.Single(leg.Rounds);

        var tomorrow = await service.AdvanceTheWorldAsync();
        Assert.Equal(8, tomorrow.MatchDayNumber);
        Assert.Equal(CompetitionType.League, tomorrow.Wave);
    }

    [Fact]
    public async Task The_call_that_plays_the_last_window_of_a_season_is_the_call_that_closes_it()
    {
        // Thirty-four days: the final's second leg is the last football, and the day after it is
        // the rest day. Playing that leg is what pays the season out and draws the next one —
        // whose first day has the Supercup of the two clubs that won the two competitions.
        var season = GivenASeasonWith();
        var nextSeasonId = Guid.NewGuid();

        _closer.Setup(closer => closer.IsFinishedAsync(season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _closer.Setup(closer => closer.CloseAsync(
                season.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeasonCloseResult(season.Id, nextSeasonId, 16, new List<Guid>()));

        var advance = await CreateAdvanceService().AdvanceTheWorldAsync();

        Assert.Equal(WorldAdvanceKind.SeasonClosed, advance.Kind);
        Assert.True(advance.SeasonClosed);
        Assert.Equal(nextSeasonId, advance.SeasonOpened);
        Assert.Equal(season.Id, advance.SeasonId);
    }

    [Fact]
    public async Task A_hand_reaches_back_for_a_window_that_was_closed_over_a_matchday_nobody_played()
    {
        // The other half of the repair, and without it the repair is a note in a log. The world
        // had a window, the window was written up as played, and one of its fixtures was a
        // matchday nobody finished — so the window owed a match. This filter used to read the
        // window's own row for that, which is a record of the window having been *closed* and
        // not of its fixtures having been played, and it skipped exactly the windows the
        // fixtures said were owed. Six fixtures of a second division sat there for a season:
        // owed to the world, and unreachable by a hand walking it.
        GivenASeasonWith(2, 3);
        var service = CreateAdvanceService();

        await service.AdvanceTheWorldAsync();
        await service.AdvanceTheWorldAsync();

        // The world has walked both days, and the window of the first day now says it was
        // played — which is the claim this walk used to believe. Both divisions drew a round
        // for that day, and one of them is the one about to owe a match.
        var firstWindow = _calendar.First(round => round.Number == 2);
        Assert.True(firstWindow.HasBeenExecuted);

        // A fixture of the first day goes back on the schedule, which is what the integrity
        // sweep does when it finds a window closed over a matchday nobody finished.
        _fixturesByRound[firstWindow.Id][0].Reopen();

        var back = await service.AdvanceTheWorldAsync();

        Assert.Equal(2, back.MatchDayNumber);
        // Day two, not day three and not nothing: the window the fixtures say is owed is the
        // window the world owes, whichever day it fell on. And only the one fixture is played
        // again — the other three of that day are decided, and a decided fixture is never
        // started.
        Assert.Equal(2, back.MatchDayNumber);
        Assert.Equal(CompetitionType.League, back.Wave);
        Assert.Contains(back.Rounds, run => run.RoundId == firstWindow.Id && run.Played == 1);
        Assert.Equal(FixtureStatus.Finished, _fixturesByRound[firstWindow.Id][0].Status);
    }

    [Fact]
    public async Task A_season_that_still_owes_a_fixture_is_not_closed_by_a_hand()
    {
        // A season closed over a hole pays its purses before its last matchday was played, and
        // the pyramid moves with them. The closer is asked; it says no; nothing is closed.
        var season = GivenASeasonWith();

        _closer.Setup(closer => closer.IsFinishedAsync(season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var advance = await CreateAdvanceService().AdvanceTheWorldAsync();

        Assert.Equal(WorldAdvanceKind.Nothing, advance.Kind);
        _closer.Verify(closer => closer.CloseAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_world_with_nothing_left_to_play_says_so_rather_than_inventing_a_window()
    {
        GivenASeasonWith();

        _closer.Setup(closer => closer.IsFinishedAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var advance = await CreateAdvanceService().AdvanceTheWorldAsync();

        Assert.Equal(WorldAdvanceKind.Nothing, advance.Kind);
        Assert.Empty(advance.Rounds);
        _player.Verify(
            player => player.PlayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private CompetitionExecutionService BuildWithStore(
        IRoundExecutionStore store,
        IOptions<WorldExecutionOptions> options) =>
        BuildWithStore(store, options, null);

    /// <summary>
    /// The claim without the row lock, for a test that has no PostgreSQL to lock a row in.
    ///
    /// It asks the window the same question the store asks, through the same methods, so what
    /// is under test here is the rule rather than the locking. The lock is the database's
    /// promise and the store is the only code that relies on it. It is given the window's
    /// fixtures for the same reason the store reads them: "has this window been played" is a
    /// question about the fixtures, and a fake that answered it from the window's own columns
    /// alone would be testing a rule the world does not have.
    /// </summary>
    private sealed class InMemoryRoundExecutionStore : IRoundExecutionStore
    {
        private const string Host = "scheduler-1";

        // The real store decides under a row lock, and the test that runs two services at the
        // same window is a test of that decision. A fake that decided it without one would be
        // passing or failing on the order two tasks happened to interleave in, which is a coin
        // in the suite rather than a test of anything.
        private readonly object _gate = new();

        private readonly IReadOnlyDictionary<Guid, Round> _windows;
        private readonly Func<Guid, IReadOnlyList<Fixture>> _fixturesOf;

        public InMemoryRoundExecutionStore(Round window, IReadOnlyList<Fixture> fixtures)
            : this(new Dictionary<Guid, Round> { [window.Id] = window }, _ => fixtures)
        {
            Window = window;
        }

        public InMemoryRoundExecutionStore(
            IReadOnlyDictionary<Guid, Round> windows,
            Func<Guid, IReadOnlyList<Fixture>> fixturesOf)
        {
            _windows = windows;
            _fixturesOf = fixturesOf;
        }

        /// <summary>The one window the single-window tests walk into.</summary>
        public Round? Window { get; }

        public int ClaimsTaken { get; private set; }

        public void Force(Action<Round> claim) => claim(Window!);

        public Task<RoundClaim> TryClaimAsync(
            Guid roundId, TimeSpan lease, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                var round = _windows[roundId];

                if (round.HasBeenExecuted)
                {
                    if (!_fixturesOf(roundId).Any(fixture => fixture.Status is not FixtureStatus.Finished))
                    {
                        return Task.FromResult(RoundClaim.AlreadyPlayed);
                    }

                    round.ReopenExecution();
                }

                if (round.ExecutionStatus is RoundExecutionStatus.Running
                    && !round.TryBeginExecution(now, lease, Host))
                {
                    return Task.FromResult(RoundClaim.HeldByAnotherProcess);
                }

                round.TryBeginExecution(now, lease, Host);
                ClaimsTaken++;
                return Task.FromResult(RoundClaim.Claimed);
            }
        }

        public Task<bool> TryCompleteAsync(
            Guid roundId, TimeSpan lease, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            var round = _windows[roundId];

            if (!round.CanBeCompletedBy(lease, now, Host))
            {
                return Task.FromResult(false);
            }

            round.CompleteExecution();
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(Guid roundId, CancellationToken cancellationToken = default)
        {
            _windows[roundId].ReleaseExecution();
            return Task.CompletedTask;
        }
    }
}
