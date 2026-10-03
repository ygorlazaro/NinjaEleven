using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A club builds its ground over rounds, pays for it once, and gets the seats once.
///
/// <para>
/// The two failures worth writing a test for are both about the same seam. The world closes its
/// windows more than once, so the settlement runs again — and a settlement that is not
/// idempotent grows a stand per Scheduler run. And the charge is written in the same unit of
/// work as the project, because a club holding a building site it never paid for is a club
/// whose balance and whose ground are two different stories.
/// </para>
/// </summary>
public class StadiumServiceTests
{
    private readonly Mock<IStadiumConstructionRepository> _constructions = new(MockBehavior.Loose);
    private readonly Mock<IStadiumRepository> _stadiums = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    private readonly Guid _teamId = Guid.NewGuid();

    /// <summary>The season the world is in, which is where a ground is paid for out of.</summary>
    private readonly Season _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private Stadium _ground = null!;
    private StadiumConstruction? _open;
    private readonly List<FinanceMovement> _book = [];

    [Fact]
    public async Task StartingAProjectChargesTheClubAndWritesTheLine()
    {
        GivenAClubWithAGroundOf(5_000);

        var project = StadiumRules.ProjectOf(10_000)!.Value;
        GivenTheBookIsEmpty();

        var started = await AService().StartExpansionAsync(_teamId, 15_000, playedThroughRound: 8);

        Assert.Equal(StadiumWorkKind.Started, started.Kind);
        Assert.Equal(10_000, started.Project!.Value.Seats);
        Assert.Equal(15_000, started.Seats);

        // The line is Infrastructure, it is a charge for exactly the project's price, and it
        // points at the project so a club's statement can be read back against the stand it
        // bought rather than against a line of "Infrastructure" with nothing on it.
        var line = Assert.Single(_book);
        Assert.Equal(FinanceMovementKind.Infrastructure, line.Kind);
        Assert.Equal(-project.Cost, line.Amount);
        Assert.StartsWith("stadium-works:", line.Reference);

        // And the ground has not grown: the seats arrive when the rounds have been played.
        Assert.Equal(5_000, _ground.Capacity);
    }

    [Fact]
    public async Task AClubAlreadyBuildingIsNotChargedTwice()
    {
        GivenAClubWithAGroundOf(5_000);
        _open = AProjectOn(5_000, 15_000, startedAfterRound: 8);

        var started = await AService().StartExpansionAsync(_teamId, 15_000, playedThroughRound: 8);

        Assert.Equal(StadiumWorkKind.NothingToBuild, started.Kind);
        Assert.Empty(_book);

        // The answer still tells the manager what he is waiting for: the ten-thousand stand he
        // already bought, the day it lands, and nothing new to pay for.
        Assert.Equal(10_000, started.Project!.Value.Seats);
        Assert.Equal(15_000, started.Seats);
        Assert.Equal(12, started.FinishesAfterRound);
    }

    [Fact]
    public async Task AGroundAsBigAsThisGameBuildsIsNotGivenAProjectToStart()
    {
        GivenAClubWithAGroundOf(StadiumRules.LargestCapacity);

        var started = await AService().StartExpansionAsync(_teamId, 100_000, playedThroughRound: 8);

        Assert.Equal(StadiumWorkKind.NothingToBuild, started.Kind);
        Assert.Null(started.Project);
        Assert.Empty(_book);
    }

    [Fact]
    public async Task AProjectIsOnlyFinishedOnceTheRoundsItTakesHaveBeenPlayed()
    {
        GivenAClubWithAGroundOf(5_000);

        // Approved after round eight, so the ten-thousand stand lands after round twelve.
        var project = StadiumRules.ProjectOf(10_000)!.Value;
        var construction = StadiumConstruction.Create(
            _teamId, _ground.Id, _season.Id, project, startedAfterRound: 8, DateTimeOffset.UnixEpoch);
        _constructions.Setup(repository => repository.ListUnfinishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([construction]);

        var early = await AService().SettleFinishedWorksAsync(playedThroughRound: 11);

        Assert.Empty(early);
        Assert.Equal(5_000, _ground.Capacity);

        var onTime = await AService().SettleFinishedWorksAsync(playedThroughRound: 12);

        var finished = Assert.Single(onTime);
        Assert.Equal(10_000, finished.SeatsAdded);
        Assert.Equal(15_000, finished.Capacity);
        Assert.Equal(15_000, _ground.Capacity);
    }

    /// <summary>
    /// The world closes a window twice and the settlement runs again. A ground that grew again
    /// would be a club whose stand is a function of how many times the Scheduler woke up.
    /// </summary>
    [Fact]
    public async Task SettlingTheSameWindowTwiceAddsTheSeatsOnce()
    {
        GivenAClubWithAGroundOf(5_000);

        var construction = StadiumConstruction.Create(
            _teamId, _ground.Id, _season.Id,
            StadiumRules.ProjectOf(5_000)!.Value,
            startedAfterRound: 0,
            DateTimeOffset.UnixEpoch);

        _constructions.Setup(repository => repository.ListUnfinishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([construction]);

        var first = await AService().SettleFinishedWorksAsync(playedThroughRound: 2);
        var second = await AService().SettleFinishedWorksAsync(playedThroughRound: 2);

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Equal(10_000, _ground.Capacity);
    }

    [Fact]
    public async Task ASeasonOfTheWorldIsWhereTheGroundIsPaidFor()
    {
        GivenAClubWithAGroundOf(5_000);
        GivenTheBookIsEmpty();

        await AService().StartExpansionAsync(_teamId, 7_000, playedThroughRound: 0);

        var line = Assert.Single(_book);
        Assert.Equal(_season.Id, line.SeasonId);
    }

    private void GivenAClubWithAGroundOf(int capacity)
    {
        _ground = Stadium.Create(_teamId, "Vila Nova");
        _ground.SetCapacity(capacity);

        var team = Team.Create("Vila Nova", "VN", "#101820", "#38d39f", 60);
        team.SetStadium(_ground);

        _teams.Setup(repository => repository.GetAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        _stadiums.Setup(repository => repository.ListForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_ground]);

        _constructions.Setup(repository => repository.FindOpenForStadiumAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _open);

        _constructions.Setup(repository => repository.ListUnfinishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _open is null ? [] : [_open]);

        _seasons.Setup(repository => repository.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);

        _finance.Setup(repository => repository.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CancellationToken _) =>
                _book.LastOrDefault(movement => movement.TeamId == _teamId));

        _finance.Setup(repository => repository.AddAsync(
                It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Callback((FinanceMovement movement, CancellationToken _) => _book.Add(movement))
            .Returns(Task.CompletedTask);
    }

    private void GivenTheBookIsEmpty() => _book.Clear();

    private static StadiumConstruction AProjectOn(int seats, int wantedSeats, int startedAfterRound) =>
        StadiumConstruction.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            StadiumRules.ProjectToReach(seats, wantedSeats)!.Value,
            startedAfterRound,
            DateTimeOffset.UnixEpoch);

    private StadiumService AService() => new(
        _constructions.Object,
        _stadiums.Object,
        _teams.Object,
        _seasons.Object,
        new FinanceService(
            _finance.Object,
            _teams.Object,
            Mock.Of<IPlayerRepository>(MockBehavior.Loose),
            Mock.Of<IFixtureRepository>(MockBehavior.Loose),
            Mock.Of<IRoundRepository>(MockBehavior.Loose),
            Mock.Of<IMatchDayRepository>(MockBehavior.Loose),
            _seasons.Object,
            InboxTestFactory.Create(_teams),
            _unitOfWork.Object,
            NullLogger<FinanceService>.Instance),
        _unitOfWork.Object,
        new FixedClock(DateTimeOffset.UnixEpoch.AddDays(30)));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}