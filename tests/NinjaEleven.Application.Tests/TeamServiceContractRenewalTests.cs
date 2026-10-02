using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// Signing a man again: what a renewal is, what it costs, and who may ask for one.
///
/// <para>
/// A renewal is two decisions in one — how long, and at what wage — and the rule worth holding
/// is that both come from the man as he is <i>now</i>. The clock is read against the season
/// being played rather than added to the seasons remaining, and the wage is worked out from the
/// player on the day of the signing rather than carried over from the contract he is leaving.
/// A renewal that inherited either of those would be a renewal of the past.
/// </para>
///
/// <para>
/// The wage is not passed in by the caller. The contract does not know what a player is worth,
/// and a service that let a screen name the figure would be letting the screen set the salary.
/// </para>
/// </summary>
public class TeamServiceContractRenewalTests
{
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    private readonly Guid _teamId = Guid.NewGuid();

    private readonly List<TeamMembership> _contracts = [];
    private readonly List<TeamMembership> _updated = [];

    private Season _season = null!;
    private Player _player = null!;

    public TeamServiceContractRenewalTests()
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

        _season = ASeason(number: 3);
        _seasons.Setup(repository => repository.GetAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);
        _seasons.Setup(repository => repository.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);

        _player = Player.Create(
            "João da Silva", 27, Position.ATT,
            speed: 74, accuracy: 79, dribbling: 71, heading: 66, strength: 63,
            goalkeeperPower: 0, reflexes: 0, stamina: 70, potential: 88);
        _players.Setup(repository => repository.GetAsync(_player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_player);
    }

    [Fact]
    public async Task ARenewalSignsTheManAgainForAsManySeasonsAsWereAsked()
    {
        var contract = GivenAContractRunningFor(2);

        var renewal = await Service().RenewContractAsync(_teamId, _player.Id, 3, _season.Id);

        Assert.Equal(3, renewal.Seasons);
        Assert.Equal(3, contract.ContractSeasons);
        Assert.Equal(_season.Number, contract.StartSeasonNumber);
        Assert.Equal(3, renewal.SeasonsLeft);
    }

    [Fact]
    public async Task ARenewalIsCountedFromTheSeasonBeingPlayedAndNotFromWhatIsLeft()
    {
        // The difference between a promise and an accumulator. A man with one season left who
        // is renewed for three has three seasons afterwards, not four — and reading the old
        // seasons out of the contract is how a renewal ends up extending a deal instead of
        // replacing it.
        var contract = GivenAContractRunningFor(2);

        await Service().RenewContractAsync(_teamId, _player.Id, 3, _season.Id);

        Assert.Equal(3, contract.SeasonsLeft(_season.Number));
        Assert.Equal(2, contract.SeasonsLeft(_season.Number + 1));
    }

    [Fact]
    public async Task ARenewalPaysTheWageTheManIsWorthToday()
    {
        // Worked out here rather than passed in, and worked out from the man as he is on the
        // day of the signing: the wage is the only thing a renewal moves besides the clock, and
        // a renewal that carried the old figure forward would be a club deciding a player's
        // value never moves.
        GivenAContractRunningFor(2, wage: 1m);
        var state = ASeasonStateForTheMan();

        var renewal = await Service().RenewContractAsync(_teamId, _player.Id, 2, _season.Id);

        Assert.Equal(PlayerValuation.SeasonWage(_player, state), renewal.Wage);
    }

    [Fact]
    public async Task AContractIsPricedByTheManAndNotByTheClubThatHoldsIt()
    {
        // The same man under two clubs is two contracts and two wages, and the second club's
        // renewal reads him rather than the row it happens to have picked up. A wage that
        // depended on the holder would make a man's salary a fact about his employer's books.
        var cheap = GivenAContractRunningFor(2, wage: 1m);

        var renewal = await Service().RenewContractAsync(_teamId, _player.Id, 2, _season.Id);

        Assert.NotEqual(1m, renewal.Wage);
        Assert.Equal(cheap.Wage, renewal.Wage);
    }

    [Fact]
    public async Task ARenewalIsBoundedAtBothEnds()
    {
        GivenAContractRunningFor(2);

        // A month-to-month with notice and a six-year promise are both refused, and the
        // refusal names the range rather than saying no: a manager who asked for seven needs
        // to be told what seven is instead of.
        var tooFew = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().RenewContractAsync(_teamId, _player.Id, 0, _season.Id));
        var tooMany = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().RenewContractAsync(_teamId, _player.Id, 6, _season.Id));

        Assert.Equal("InvalidContractLength", tooFew.Code);
        Assert.Equal("InvalidContractLength", tooMany.Code);
        Assert.Contains("1", tooMany.Message);
        Assert.Contains("5", tooMany.Message);
    }

    [Fact]
    public async Task APlayerWithNoContractHereIsNotSomebodyToRenew()
    {
        // The other club's player is not a renewal, it is a transfer, and the two cost
        // different amounts of money. He has a season and a state — he plays, just not here —
        // so what is missing is the contract and the refusal says so.
        ASeasonStateForTheMan();

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().RenewContractAsync(_teamId, _player.Id, 2, _season.Id));

        Assert.Equal("PlayerNotContracted", error.Code);
        Assert.Empty(_updated);
    }

    [Fact]
    public async Task AWorldWithNoSeasonCannotSignAnybody()
    {
        // A contract's clock is the calendar, so there is no season to count from and the
        // renewal is refused rather than counted from nothing.
        GivenAContractRunningFor(2);
        _seasons.Setup(repository => repository.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Season?)null);
        _seasons.Setup(repository => repository.GetAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Season?)null);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => Service().RenewContractAsync(_teamId, _player.Id, 2));

        Assert.Equal("SeasonRequired", error.Code);
    }

    [Fact]
    public async Task AClubSomebodyIsNotRunningHasNoContractToRenew()
    {
        // The same door the shirt number goes through: a rule in a controller is a rule the
        // other callers walk past, and there are three callers of a manager's club.
        var elsewhere = Guid.NewGuid();
        var other = Team.Create("Outro Clube", "OUT", "#101820", "#38d39f", 70);
        _teams.Setup(repository => repository.GetAsync(elsewhere, It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);

        await Assert.ThrowsAnyAsync<Exception>(
            () => Service().RenewContractAsync(elsewhere, _player.Id, 2, _season.Id));

        Assert.Empty(_updated);
    }

    // --- Helpers ------------------------------------------------------------------

    private TeamService Service() => new(
        _teams.Object,
        _players.Object,
        _seasons.Object,
        Mock.Of<IMatchRepository>(),
        Mock.Of<IClubEventRepository>(),
        _unitOfWork.Object);

    private TeamMembership GivenAContractRunningFor(int seasons, decimal wage = 0m)
    {
        var contract = TeamMembership.Create(
            _player.Id, _teamId, new DateOnly(2026, 1, 1), seasons,
            startSeasonNumber: 1, wage: wage);

        _contracts.Add(contract);

        var state = ASeasonStateForTheMan();
        _players.Setup(repository => repository.GetSeasonStateForUpdateAsync(
                _player.Id, _season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        return contract;
    }

    private PlayerSeasonState ASeasonStateForTheMan()
    {
        var state = PlayerSeasonState.Create(_player.Id, _season.Id, _teamId, energy: 90);

        _players.Setup(repository => repository.GetSeasonStateForUpdateAsync(
                _player.Id, _season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        return state;
    }

    private static Season ASeason(int number)
    {
        var season = Season.Create(number, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();

        return season;
    }
}