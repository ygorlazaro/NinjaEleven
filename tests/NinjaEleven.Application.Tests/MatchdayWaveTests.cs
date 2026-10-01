using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A matchday is a sequence of windows, not a set of games: the championship of the day is
/// played out in every division at once, and only then does the cup go out.
///
/// These tests are about **when** the second window opens, because a window that opens early
/// is the whole error, and it is invisible in the result: a cup leg played by a side that has
/// not finished its own match of the day is a leg taken by a team that is not there yet, and
/// every number afterwards looks perfectly reasonable.
/// </summary>
public class MatchdayWaveTests
{
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _matchDayId = Guid.NewGuid();

    /// <summary>The championship edition of the day, and the cup's — one window each.</summary>
    private readonly Competition _leagueCompetition = Competition.Create("Campeonato", CompetitionType.League);
    private readonly Competition _cupCompetition = Competition.Create("Copa", CompetitionType.Cup);

    private CompetitionSeason _league = null!;
    private CompetitionSeason _cup = null!;

    private Round _leagueRound = null!;
    private Round _cupRound = null!;

    /// <summary>One division's fixture. Three of them, one per division, in the first window.</summary>
    private readonly List<Fixture> _championship = new();

    /// <summary>The cup's fixtures, in the second window.</summary>
    private readonly List<Fixture> _cupFixtures = new();

    /// <summary>Every fixture the service asked to be started, in the order it asked.</summary>
    private readonly List<Guid> _started = new();

    public MatchdayWaveTests()
        : this(withCupWindow: true)
    {
    }

    private MatchdayWaveTests(bool withCupWindow)
    {
        // The edition is not what says which competition a round is a round of: the round
        // belongs to an edition and the edition belongs to a competition, and the wave a round
        // is in is read all the way up that chain.
        _league = CompetitionSeason.Create(_leagueCompetition.Id, _seasonId, Guid.NewGuid());
        _cup = CompetitionSeason.Create(_cupCompetition.Id, _seasonId);

        _leagueRound = Round.Create(_league.Id, 1);
        _cupRound = Round.Create(_cup.Id, 1);
        _leagueRound.ScheduleOn(_matchDayId);
        _cupRound.ScheduleOn(_matchDayId);

        for (var division = 0; division < 3; division++)
        {
            _championship.Add(Fixture.Create(_leagueRound.Id, Guid.NewGuid(), Guid.NewGuid()));
        }

        if (withCupWindow)
        {
            for (var tie = 0; tie < 16; tie++)
            {
                _cupFixtures.Add(Fixture.Create(_cupRound.Id, Guid.NewGuid(), Guid.NewGuid()));
            }
        }

        _matchDays.Setup(repo => repo.GetAsync(_matchDayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MatchDay.Create(_seasonId, 4, new DateOnly(2026, 2, 1)));

        _competitions.Setup(repo => repo.GetSeasonByIdAsync(_league.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_league);
        _competitions.Setup(repo => repo.GetSeasonByIdAsync(_cup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cup);
        _competitions.Setup(repo => repo.GetAsync(_leagueCompetition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_leagueCompetition);
        _competitions.Setup(repo => repo.GetAsync(_cupCompetition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cupCompetition);

        var roundsOfTheDay = withCupWindow
            ? new List<Round> { _leagueRound, _cupRound }
            : new List<Round> { _leagueRound };

        _rounds.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new[] { _leagueRound, _cupRound });
        _rounds.Setup(repo => repo.ListByMatchDayAsync(_matchDayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roundsOfTheDay);
        _rounds.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                new[] { _leagueRound, _cupRound }.FirstOrDefault(round => round.Id == id));

        var all = () => _championship.Concat(_cupFixtures).ToList();

        _fixtures.Setup(repo => repo.ListByRoundIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                all().Where(fixture => ids.Contains(fixture.RoundId)).ToList());
        _fixtures.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => all().FirstOrDefault(fixture => fixture.Id == id));

        _matches.Setup(repo => repo.GetByFixtureAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid fixtureId, CancellationToken _) =>
            {
                var fixture = all().FirstOrDefault(candidate => candidate.Id == fixtureId);

                return fixture is not null && fixture.Status != FixtureStatus.Scheduled
                    ? Match.Create(fixture.Id, fixture.HomeTeamId, fixture.AwayTeamId)
                    : null;
            });
    }

    private ScorerPrizeService CreateScorerPrizes() => new(
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
            NullLogger<FinanceService>.Instance),
        InboxTestFactory.Create(_teams),
        NullLogger<ScorerPrizeService>.Instance);

    private TransferService CreateTransfers() => new(
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
        NullLogger<TransferService>.Instance);

    private MatchdayService CreateService()
    {
        var service = new MatchdayService(
            _matchDays.Object,
            _rounds.Object,
            _fixtures.Object,
            _competitions.Object,
            _matches.Object,
            CreateScorerPrizes(),
            CreateTransfers(),
            _unitOfWork.Object,
            NullLogger<MatchdayService>.Instance);

        service.UseStarter((fixtureId, _) =>
        {
            _started.Add(fixtureId);
            _cupFixtures.SingleOrDefault(fixture => fixture.Id == fixtureId)?.MarkInProgress();
            return Task.FromResult<Guid?>(Guid.NewGuid());
        });

        return service;
    }

    [Fact]
    public async Task The_cup_window_opens_only_after_the_last_championship_match_of_the_day_is_over()
    {
        var manager = _championship[0];
        var second = _championship[1];
        var third = _championship[2];
        var cup = _cupFixtures[0];

        var service = CreateService();

        // Every division has kicked off, which is what a matchday looks like twenty minutes
        // in: eighteen games running, and the manager's own about to finish.
        Play(manager);
        Play(second);
        Play(third);

        Finish(manager);
        await service.AdvanceAsync(manager.Id);

        // One of eighteen is over. The cup has not started and the day's open window is still
        // the championship.
        Assert.DoesNotContain(cup.Id, _started);
        Assert.Equal(CompetitionType.League, (await service.GetProgressAsync(_matchDayId)).OpenWave);

        // Two of three. The third is still being played, and a cup leg taken now is a leg
        // taken by a side whose league match has not finished.
        Finish(second);
        await service.AdvanceAsync(second.Id);

        Assert.DoesNotContain(cup.Id, _started);
        Assert.Equal(CompetitionType.League, (await service.GetProgressAsync(_matchDayId)).OpenWave);

        // Now the championship of the day is over, and the cup goes out — all of it at once.
        Finish(third);
        await service.AdvanceAsync(third.Id);

        Assert.Equal(
            _cupFixtures.Select(fixture => fixture.Id).OrderBy(id => id),
            _started.OrderBy(id => id));
        Assert.Equal(CompetitionType.Cup, (await service.GetProgressAsync(_matchDayId)).OpenWave);
    }

    [Fact]
    public async Task A_match_finishing_in_the_middle_of_its_own_window_does_not_start_that_window_again()
    {
        // The double start this guards: the world walks a window a fixture at a time, and every
        // match that finishes asks the day whether there is anything left to kick off. The
        // fixtures the walk has not reached yet are still on the schedule, so the answer used
        // to be yes — and each one was started here, abandoned by the walk when it arrived,
        // and started again, which is a matchday that leaves four abandoned matches behind
        // every fixture it played. A window that has begun belongs to whoever began it.
        var first = _championship[0];
        var second = _championship[1];
        var third = _championship[2];

        var service = CreateService();

        // The window has begun: the manager's kick-off started the day, and the other two are
        // on the pitch. One of them finishes, which is the moment the day used to go round
        // starting whatever it found on the schedule.
        Play(first);
        Play(second);
        Play(third);

        Finish(first);
        await service.AdvanceAsync(first.Id);

        Assert.Equal(CompetitionType.League, (await service.GetProgressAsync(_matchDayId)).OpenWave);
        Assert.DoesNotContain(_started, id => _championship.Any(fixture => fixture.Id == id));

        // And the cup is not opened by it either: the window it belongs to is still running.
        Assert.DoesNotContain(_cupFixtures[0].Id, _started);

        // The last match of the window is what opens the next one, which is the whole of what
        // this method is for.
        Finish(second);
        Finish(third);
        await service.AdvanceAsync(third.Id);

        Assert.Equal(CompetitionType.Cup, (await service.GetProgressAsync(_matchDayId)).OpenWave);
    }

    [Fact]
    public async Task A_manager_cannot_start_a_cup_match_while_the_championship_is_still_running()
    {
        var manager = _championship[0];
        var cup = _cupFixtures[0];

        Play(manager);

        var service = CreateService();

        // The championship is the open window, so the manager may start his own match, and the
        // cup is refused. The refusal has to be a refusal: a client that ignored it would play
        // a cup leg in the middle of a league matchday.
        await service.EnsureTheWaveIsOpenAsync(manager.Id);
        await Assert.ThrowsAsync<DomainValidationException>(
            () => service.EnsureTheWaveIsOpenAsync(cup.Id));
    }

    [Fact]
    public async Task A_day_with_no_cup_in_it_is_finished_when_its_championship_is()
    {
        // A championship matchday. It has no second window — the bracket is not drawn yet, so
        // the cup round is not there — and it is over when its own window is.
        _rounds.Setup(repo => repo.ListByMatchDayAsync(_matchDayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { _leagueRound });

        var service = new MatchdayService(
            _matchDays.Object,
            _rounds.Object,
            _fixtures.Object,
            _competitions.Object,
            _matches.Object,
            CreateScorerPrizes(),
            CreateTransfers(),
            _unitOfWork.Object,
            NullLogger<MatchdayService>.Instance);

        // The last of the eighteen finishes, and the day is over with nothing behind it.
        foreach (var fixture in _championship)
        {
            Play(fixture);
            Finish(fixture);
        }

        var progress = await service.AdvanceAsync(_championship[2].Id);

        Assert.True(progress.Complete);
        Assert.Null(progress.OpenWave);
    }

    [Fact]
    public async Task AMatchday_whose_championship_was_never_played_does_not_open_its_cup_window()
    {
        var cup = _cupFixtures[0];
        var service = CreateService();

        // Nothing has kicked off. The day is the same either way, and this is the state a
        // restart leaves behind: a day whose cup window has fixtures and whose championship
        // has not been started is a day that waits, not a day that plays its cup.
        var progress = await service.GetProgressAsync(_matchDayId);

        Assert.Equal(CompetitionType.League, progress.OpenWave);
        Assert.False(progress.Complete);
        Assert.DoesNotContain(cup.Id, _started);
    }

    [Fact]
    public async Task A_window_is_closed_when_its_last_match_is_over()
    {
        var service = CreateService();

        foreach (var fixture in _championship)
        {
            Play(fixture);
            Finish(fixture);
            await service.AdvanceAsync(fixture.Id);
        }

        // The championship of the day is over and it says so. A round that never closes is a
        // season that plays twenty-two matchdays and reads as though every one of them were
        // still running.
        Assert.True(_leagueRound.IsCompleted);
        _rounds.Verify(repo => repo.Update(_leagueRound), Times.Once);
    }

    [Fact]
    public async Task The_last_match_of_the_day_closes_its_own_window()
    {
        // A day with a cup window in it is not a day whose last match completes it, so this
        // one is a championship on its own: the last match of the day is also the last match
        // of the last window, which is the case a check for "is the day over" gets wrong.
        _rounds.Setup(repo => repo.ListByMatchDayAsync(_matchDayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { _leagueRound });

        var service = CreateService();

        foreach (var fixture in _championship)
        {
            Play(fixture);
        }

        // Two of three divisions are over, and the last match of the day is in the third. A
        // check that stopped as soon as the day was complete would stop before closing the
        // window whose last match it was answering, and the day would end with one of its
        // three windows still open.
        Finish(_championship[0]);
        await service.AdvanceAsync(_championship[0].Id);

        Finish(_championship[1]);
        await service.AdvanceAsync(_championship[1].Id);

        Assert.False(_leagueRound.IsCompleted);

        Finish(_championship[2]);
        var progress = await service.AdvanceAsync(_championship[2].Id);

        Assert.True(_leagueRound.IsCompleted);
        Assert.True(progress.Complete);
    }

    [Fact]
    public async Task A_window_still_being_played_is_not_closed()
    {
        var service = CreateService();

        foreach (var fixture in _championship)
        {
            Play(fixture);
            Finish(fixture);
            await service.AdvanceAsync(fixture.Id);
        }

        // The cup went out with the championship and nothing in it has finished, so its window
        // is open — a closed one would say the tie was decided before it was played.
        Assert.False(_cupRound.IsCompleted);
    }

    [Fact]
    public async Task A_window_played_while_the_process_was_down_is_closed_on_the_restart()
    {
        // Every fixture of the championship was played and the window was never closed,
        // because the match that finished it finished it without the process running.
        foreach (var fixture in _championship)
        {
            Finish(fixture);
        }

        var service = CreateService();

        await service.CloseTheWindowsThatWereLeftOpenAsync();

        Assert.True(_leagueRound.IsCompleted);
    }

    [Fact]
    public async Task The_restart_leaves_a_window_that_is_still_being_played_open()
    {
        Play(_championship[0]);
        Finish(_championship[1]);
        Finish(_championship[2]);

        var service = CreateService();

        await service.CloseTheWindowsThatWereLeftOpenAsync();

        // Two of three played is not a window that was played, and closing it would say the
        // day is over while a match of it is still on the pitch.
        Assert.False(_leagueRound.IsCompleted);
    }

    private static void Play(Fixture fixture) => fixture.MarkInProgress();

    private static void Finish(Fixture fixture) => fixture.MarkFinished();
}
