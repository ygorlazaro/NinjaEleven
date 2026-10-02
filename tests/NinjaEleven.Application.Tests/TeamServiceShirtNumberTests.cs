using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What a manager may put on a man's back, and who may ask.
///
/// <para>
/// Three rules carry the whole feature. The number belongs to the contract rather than to the
/// man, so it is read from the club's own live contracts and never from a player's record —
/// a shirt is what he wears <i>here</i>, and a number that lived on the person would follow
/// him between clubs. A number is unique inside a dressing room and not across the world:
/// two rivals' number nines are two different ninths, and a uniqueness that reached past the
/// club would leave clubs unable to field the numbers they have always fielded. And a club
/// somebody is not running has no dressing room this service may open.
/// </para>
/// </summary>
public class TeamServiceShirtNumberTests
{
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    private readonly Guid _teamId = Guid.NewGuid();

    private readonly List<TeamMembership> _contracts = [];
    private readonly List<TeamMembership> _updated = [];

    public TeamServiceShirtNumberTests()
    {
        var club = Team.Create("Ninja Eleven", "NIN", "#101820", "#38d39f", 70);
        club.MarkAsManagerClub();

        _teams.Setup(repository => repository.GetAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(club);

        _teams.Setup(repository => repository.GetLiveContractsAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken _) => Task.FromResult<IReadOnlyList<TeamMembership>>(_contracts));

        _teams.Setup(repository => repository.UpdateMembership(It.IsAny<TeamMembership>()))
            .Callback((TeamMembership membership) => _updated.Add(membership));
    }

    private TeamService Service() => new(
        _teams.Object,
        Mock.Of<IPlayerRepository>(),
        Mock.Of<ISeasonRepository>(),
        Mock.Of<IMatchRepository>(),
        Mock.Of<IClubEventRepository>(),
        _unitOfWork.Object);

    private TeamMembership Given(string name, int? shirtNumber)
    {
        var contract = TeamMembership.Create(
            Guid.NewGuid(),
            _teamId,
            new DateOnly(2026, 1, 1),
            shirtNumber: shirtNumber);

        _contracts.Add(contract);
        return contract;
    }

    [Fact]
    public async Task AManagerPutsOneOfHisOwnMenInAShirt()
    {
        var striker = Given("João da Silva", 9);

        var number = await Service().UpdateShirtNumberAsync(_teamId, striker.PlayerId, 10);

        Assert.Equal(10, number);
        Assert.Equal(10, striker.ShirtNumber);
        Assert.Contains(striker, _updated);
        _unitOfWork.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AManWithNoNumberIsGivenOne()
    {
        var free = Given("Sem camisa", null);

        Assert.Null(free.ShirtNumber);
        Assert.False(free.HasShirtNumber);

        await Service().UpdateShirtNumberAsync(_teamId, free.PlayerId, 23);

        Assert.Equal(23, free.ShirtNumber);
        Assert.True(free.HasShirtNumber);
    }

    /// <summary>
    /// Both ends of the range are refused, and refused as themselves. "The number is taken"
    /// and "that is not a number" are two different answers to two different mistakes, and a
    /// manager who changed 9 to 100 and then 9 to 10 needs to be told which of the two he did.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task ANumberOutsideOneToNinetyNineIsRefused(int asked)
    {
        var striker = Given("João da Silva", 9);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().UpdateShirtNumberAsync(_teamId, striker.PlayerId, asked));

        Assert.Equal(ShirtNumberRules.OutOfRangeCode, error.Code);
        Assert.Equal(9, striker.ShirtNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public async Task BothEndsOfTheRangeAreNumbersAPlayerMayWear(int asked)
    {
        var striker = Given("João da Silva", 9);

        Assert.Equal(asked, await Service().UpdateShirtNumberAsync(_teamId, striker.PlayerId, asked));
    }

    [Fact]
    public async Task ANumberSomebodyElseIsWearingIsRefused()
    {
        Given("Zé da Silva", 10);
        var striker = Given("João da Silva", 9);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().UpdateShirtNumberAsync(_teamId, striker.PlayerId, 10));

        Assert.Equal(ShirtNumberRules.AlreadyTakenCode, error.Code);
        Assert.Equal(9, striker.ShirtNumber);
    }

    /// <summary>
    /// A man keeping the number he already wears is not a change and does not collide with
    /// himself. The clash is asked about every other man, and a check that forgot that would
    /// refuse the only renumbering a manager makes most often: none at all.
    /// </summary>
    [Fact]
    public async Task AManKeepingHisOwnNumberIsNotARefusal()
    {
        Given("Zé da Silva", 10);
        var striker = Given("João da Silva", 9);

        Assert.Equal(9, await Service().UpdateShirtNumberAsync(_teamId, striker.PlayerId, 9));
    }

    [Fact]
    public async Task AManTheClubDoesNotHoldCannotBeRenumbered()
    {
        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().UpdateShirtNumberAsync(_teamId, Guid.NewGuid(), 10));

        Assert.Equal("PlayerNotContracted", error.Code);
    }

    /// <summary>
    /// The number is unique inside a dressing room and nowhere else. Two rivals both fielding
    /// a number ten is football, and a uniqueness that reached past the club would quietly
    /// make it impossible to hand out the numbers clubs have always handed out.
    /// </summary>
    [Fact]
    public async Task TheNumberIsUniqueInsideTheClubAndNotAcrossTheWorld()
    {
        var striker = Given("João da Silva", 9);

        // The tenth of another club is not in this club's live contracts, and that is the
        // whole reason the clash is asked about the contracts of one club.
        var number = await Service().UpdateShirtNumberAsync(_teamId, striker.PlayerId, 10);

        Assert.Equal(10, number);
    }

    /// <summary>
    /// Only the club somebody is running has a dressing room this may open. A squad screen
    /// belongs to another club as often as to the manager's own, and a service that renumbered
    /// a rival would be a control the backend has already said no to.
    /// </summary>
    [Fact]
    public async Task AClubTheManagerDoesNotRunCannotRenumberAMan()
    {
        var rival = Team.Create("Bairro Unido", "BAI", "#222222", "#ffffff", 65);
        var rivalId = Guid.NewGuid();

        _teams.Setup(repository => repository.GetAsync(rivalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rival);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().UpdateShirtNumberAsync(rivalId, Guid.NewGuid(), 10));

        Assert.Equal("NotTheManagerClub", error.Code);
    }

    [Fact]
    public async Task AClubNobodyHasIsNotFound()
    {
        var missing = Guid.NewGuid();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Service().UpdateShirtNumberAsync(missing, Guid.NewGuid(), 10));
    }
}
