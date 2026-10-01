using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The four things a contract is for: what it costs, how long it runs, whether it can be
/// signed again, and whether it is over.
///
/// <para>
/// They live together because they are the same object. A wage that is worked out fresh on the
/// day of the payday is not a wage, a contract whose length is a running tally rather than a
/// promise is not a contract, and a renewal that quietly left the seasons alone would be a verb
/// lying about what it did. Each test below holds one of those seams shut.
/// </para>
/// </summary>
public class ContractWageTests
{
    private static readonly DateOnly Signed = new(2026, 1, 1);

    [Fact]
    public void AContractIsSignedOnAWageAndKeepsIt()
    {
        // The wage is agreed once and read thereafter. It is the difference between a club that
        // knows what a squad costs and a club that works it out again on every payday, from a
        // man who may be a different player by then.
        var contract = TeamMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Signed, wage: 480_000m);

        Assert.Equal(480_000m, contract.Wage);
    }

    [Fact]
    public void AContractWithNoWageIsAContractThatHasNotBeenPaid()
    {
        // Zero rather than an invented number. The seeder prices the contracts a world already
        // had, and a contract it has not reached yet is a contract that costs nothing rather
        // than one carrying a wage nobody agreed to.
        var contract = TeamMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Signed);

        Assert.Equal(0m, contract.Wage);
    }

    [Fact]
    public void AWageIsNeverADebt()
    {
        // A club paying a player to go away is a different game. The number on the contract is
        // what the club pays out, and a negative one would turn a wage bill into an income and
        // quietly fix every club's finances.
        Assert.Throws<ArgumentOutOfRangeException>(() => AgreeAWageOf(-1m));
    }

    [Fact]
    public void ARenewalAgreesTheWageAsWellAsTheSeasons()
    {
        // Both, and in this order: a renewal that moved the clock and left the wage would be
        // the best deal in the game, and one that moved the wage and left the clock would be a
        // man who had been re-signed without being promised anything.
        var contract = AContractRunningFor(2, out var teamId);

        contract.Renew(3, currentSeasonNumber: 2, wage: 900_000m, on: new DateOnly(2026, 7, 1));

        Assert.Equal(900_000m, contract.Wage);
        Assert.Equal(3, contract.ContractSeasons);
        Assert.Equal(2, contract.StartSeasonNumber);
        Assert.Null(contract.EndDate);
    }

    [Fact]
    public void ARenewalIsCountedFromTodayAndNotAddedToWhatIsLeft()
    {
        // A renewal is a decision about the future rather than an extension of a tally. A man
        // with one season left who is renewed for two has two seasons afterwards, and not
        // three — which is the whole difference between a promise and an accumulator.
        var contract = AContractRunningFor(2, out _);

        contract.Renew(2, currentSeasonNumber: 2, wage: 100m, on: new DateOnly(2026, 7, 1));

        Assert.Equal(2, contract.SeasonsLeft(2));
        Assert.Equal(1, contract.SeasonsLeft(3));
    }

    [Fact]
    public void ARenewalIsBoundedAtBothEnds()
    {
        // One season is a month-to-month with notice; six is a club tying itself to a player it
        // has not seen play for four years. The two bounds are what stop both.
        var contract = AContractRunningFor(2, out _);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => contract.Renew(0, 2, 100m, new DateOnly(2026, 7, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => contract.Renew(ContractRules.MostRenewableSeasons + 1, 2, 100m, new DateOnly(2026, 7, 1)));

        // The ends themselves are allowed, or the bounds would be decoration.
        contract.Renew(ContractRules.FewestRenewableSeasons, 2, 100m, new DateOnly(2026, 7, 1));
        contract.Renew(ContractRules.MostRenewableSeasons, 2, 100m, new DateOnly(2026, 7, 1));

        Assert.Equal(ContractRules.MostRenewableSeasons, contract.ContractSeasons);
    }

    [Fact]
    public void ARenewalIsNotSignedBeforeTheWorldExisted()
    {
        // Season zero is not a season a contract can be signed in. A renewal that accepted it
        // would give a man seasons that no calendar ever reaches, and he would never run out.
        var contract = AContractRunningFor(2, out _);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => contract.Renew(2, currentSeasonNumber: 0, wage: 100m, on: Signed));
    }

    [Fact]
    public void AContractRunsOutAtTheEndOfTheSeasonItWasSignedFor()
    {
        // Signed in season one for two seasons, he is a free man at the end of season two — not
        // at the end of season three. The boundary the club stands on is the season that has just
        // finished, so a contract of one season is spent the moment that season is done.
        var contract = AContractRunningFor(2, out _);

        Assert.False(contract.HasRunOut(1));
        Assert.True(contract.HasRunOut(2));
        Assert.True(contract.HasRunOut(3));
    }

    [Fact]
    public void AContractInItsLastSeasonIsTheOneAnotherClubMaySign()
    {
        // The difference between a free man and a man somebody else owns is a price, and it is
        // the only thing that stops a squad being sold out from under itself in the summer.
        var contract = AContractRunningFor(3, out _);

        Assert.False(contract.IsInHisLastSeason(1));
        Assert.False(contract.IsInHisLastSeason(2));
        Assert.True(contract.IsInHisLastSeason(3));
    }

    [Fact]
    public void ARenewalRunsOutOnItsOwnNewSeasonsAndNotTheOldOnes()
    {
        // The trap this guards: renewing a man whose contract has already run out and reading
        // the seasons from where he was rather than from when he was signed. A renewal from the
        // old start would leave a man who had just been handed three seasons with nothing to
        // play for, which reads as a contract that never expires.
        var contract = AContractRunningFor(1, out _);

        Assert.True(contract.HasRunOut(4));

        contract.Renew(2, currentSeasonNumber: 4, wage: 100m, on: new DateOnly(2030, 1, 1));

        Assert.Equal(2, contract.SeasonsLeft(4));
        Assert.False(contract.HasRunOut(4));
        Assert.True(contract.HasRunOut(6));
    }

    [Fact]
    public void AContractIsNeverLeftWithNegativeSeasons()
    {
        // Zero, and not a negative number. A contract whose seasons are all spent is a free
        // man, and a question it cannot answer is not a worse answer than a wrong one.
        var contract = AContractRunningFor(1, out _);

        Assert.Equal(0, contract.SeasonsLeft(9));
    }

    // --- Helpers ------------------------------------------------------------------

    /// <summary>
    /// A live contract signed in season one, which is how every world starts: the seeder signs
    /// its men in the first season there is.
    /// </summary>
    private static TeamMembership AContractRunningFor(int seasons, out Guid teamId)
    {
        teamId = Guid.NewGuid();

        return TeamMembership.Create(
            Guid.NewGuid(), teamId, Signed, seasons, startSeasonNumber: 1);
    }

    private static void AgreeAWageOf(decimal wage) =>
        TeamMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Signed, wage: wage);
}