using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What the clubs nobody is running do with their grounds.
///
/// <para>
/// Every number here is a club that exists in a four-division world — a ground of a size the
/// pyramid actually uses, a following the ladder actually hands out, and a wage bill the world
/// actually charges. The claims being made are about which of those clubs builds, and the
/// ordering between them is the whole of it.
/// </para>
/// </summary>
public class NpcStadiumPolicyTests
{
    private const decimal Wages = 165_000m;
    private const decimal Comfortable = 14_000_000m;
    private const decimal Broke = 250_000m;

    private static NpcStadiumFacts Club(
        int capacity,
        int demand,
        bool measured = true,
        bool growing = false,
        decimal balance = Comfortable) =>
        new(Guid.NewGuid(), capacity, demand, measured, growing, balance, Wages);

    /// <summary>
    /// The country looks at its grounds every five rounds, and on no other day.
    /// </summary>
    /// <remarks>
    /// A stand is built once and a crowd is measured over a season. A club assessed on one
    /// matchday would build on a wet Saturday, and every club in the country would start a
    /// building site on the same matchday and the country's gate money would arrive in one lump.
    /// </remarks>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(10, true)]
    [InlineData(15, true)]
    [InlineData(30, true)]
    public void TheCountryLooksAtItsGroundsEveryFiveRounds(int round, bool buildingDay) =>
        Assert.Equal(buildingDay, NpcStadiumPolicy.IsBuildingDay(round));

    /// <summary>
    /// A world that has played no round has measured no crowd, so round zero is not a building
    /// day even though it is a multiple of five.
    /// </summary>
    [Fact]
    public void ARoundThatHasNotBeenPlayedIsNotABuildingDay() =>
        Assert.False(NpcStadiumPolicy.IsBuildingDay(0));

    /// <summary>
    /// A club whose ground holds everybody who comes does not build.
    /// </summary>
    [Fact]
    public void AClubWithARoomyGroundLeavesItAlone()
    {
        var decision = NpcStadiumPolicy.Decide(Club(5_000, 2_000));

        Assert.False(decision.Builds);
        Assert.Equal(NpcStadiumVerdict.TheGroundFits, decision.Verdict);

        // And nothing to build carries nothing to pay for: a refusal that quotes a four-million
        // stand on it is a bill waiting to be misread.
        Assert.Null(decision.Project);
        Assert.Equal(0m, decision.Cost);
    }

    /// <summary>
    /// A club turning people away builds, and builds to the size its crowd wants rather than
    /// the biggest thing the catalogue has.
    /// </summary>
    /// <remarks>
    /// The last half is the rule that matters. Asked what it could build next, every ground in
    /// the game answers "ten thousand seats", because that is the largest stand that fits — and
    /// a club whose crowd wants eleven thousand would be handed twenty thousand, eight thousand
    /// of them empty and every one of them paid for at the gate that was supposed to pay for
    /// the stand.
    /// </remarks>
    [Fact]
    public void AClubBeingTurnedAwayBuildsToTheSizeItsCrowdWants()
    {
        var decision = NpcStadiumPolicy.Decide(Club(5_000, 11_700));

        Assert.True(decision.Builds);
        Assert.NotNull(decision.Project);

        // A project's Seats is the stand, not the ground: five thousand of new seats on a ground
        // of five thousand is a ground of ten thousand. And that ground holds the crowd, which
        // is the whole claim — it is not ten thousand of new seats.
        Assert.Equal(10_000, 5_000 + decision.Project!.Value.Seats);
        Assert.True(5_000 + decision.Project.Value.Seats <= 11_700);
    }

    /// <summary>
    /// A club whose crowd has outgrown the biggest single stand still gets a step, not a leap.
    /// </summary>
    /// <remarks>
    /// Nothing in the catalogue reaches thirty thousand from ten without passing it, and a rule
    /// that answered null would leave a club of thirty thousand supporters with a ground of ten
    /// thousand for ever. So the smallest stand is the answer when the largest would overshoot,
    /// and a ground doubles over a season rather than in one decision.
    /// </remarks>
    [Fact]
    public void AGroundGrowsByTheSmallestStepWhenNoProjectReachesTheCrowd()
    {
        // A thousand more people than the ground holds, and not one project in the catalogue
        // reaches that without passing it: the smallest is two thousand and the crowd is short
        // by a thousand. The ground still has to move, so it moves by the smallest step.
        var decision = NpcStadiumPolicy.Decide(Club(10_000, 11_000));

        Assert.True(decision.Builds);
        Assert.Equal(
            StadiumRules.Projects.Min(project => project.Seats),
            decision.Project!.Value.Seats);
    }

    /// <summary>
    /// A club whose ground is already the biggest this game builds does not build.
    /// </summary>
    [Fact]
    public void AGroundThatCannotGrowSaysThereIsNothingToBuild()
    {
        var decision = NpcStadiumPolicy.Decide(Club(StadiumRules.LargestCapacity, 250_000));

        Assert.False(decision.Builds);
        Assert.Equal(NpcStadiumVerdict.NothingLeftToBuild, decision.Verdict);
        Assert.Null(decision.Project);
    }

    /// <summary>
    /// A club the world has never measured is not a club with no pressure.
    /// </summary>
    /// <remarks>
    /// This is the refusal that would otherwise be invisible and ruinous. A match played before
    /// the world kept what a crowd wanted has no demand recorded, and a policy that read that
    /// as zero would read it as a full and comfortable ground — and would decline, correctly on
    /// the number it had and wrongly in the world, to build for a club whose stand had not been
    /// measured at all.
    /// </remarks>
    [Fact]
    public void AClubNobodyHasMeasuredIsNotBuiltForAndIsNotLeftAloneEither()
    {
        var decision = NpcStadiumPolicy.Decide(Club(5_000, 0, measured: false));

        Assert.False(decision.Builds);
        Assert.Equal(NpcStadiumVerdict.NotYetMeasured, decision.Verdict);
    }

    /// <summary>
    /// The only reason to build before being turned away is a crowd that is still growing.
    /// </summary>
    [Fact]
    public void AGrowingClubBuildsBeforeTheCrowdTurnsAway()
    {
        // Nine tenths full and rising: next season this ground is short of seats.
        var growing = NpcStadiumPolicy.Decide(
            Club(5_000, 4_500, growing: true));

        Assert.True(growing.Builds);
        Assert.Equal(NpcStadiumVerdict.Build, growing.Verdict);

        // The same ground, the same crowd, a following that is not growing: there is nothing
        // coming and so there is nothing to build for.
        var staticClub = NpcStadiumPolicy.Decide(
            Club(5_000, 4_500, growing: false));

        Assert.False(staticClub.Builds);
    }

    /// <summary>
    /// A club with room to grow does not build ahead of the crowd either.
    /// </summary>
    /// <remarks>
    /// Half a ground is not a pressure and a growing club at half a ground is a club whose
    /// following has years to run. Building at that point is a club spending a season's gate on
    /// seats it does not need yet.
    /// </remarks>
    [Fact]
    public void AGrowingClubWithRoomToSpareWaits()
    {
        var decision = NpcStadiumPolicy.Decide(
            Club(5_000, (int)(5_000 * (NpcStadiumPolicy.AnticipationFill - 0.2)), growing: true));

        Assert.False(decision.Builds);
        Assert.Equal(NpcStadiumVerdict.TheGroundFits, decision.Verdict);
    }

    /// <summary>
    /// A club that cannot pay its squad after building does not build.
    /// </summary>
    /// <remarks>
    /// The reserve is the wage bill rather than a share of the price, because a stand is a cost
    /// a club can defer and a squad is a cost it cannot. A club that signs off its wages to buy
    /// seats has not improved its ground — it has moved the failure to where it happens earlier
    /// and cannot be refused.
    /// </remarks>
    [Fact]
    public void AClubThatCannotStillPayItsSquadDoesNotBuild()
    {
        var poor = NpcStadiumPolicy.Decide(Club(5_000, 11_700, balance: Broke));
        Assert.False(poor.Builds);
        Assert.Equal(NpcStadiumVerdict.CannotAfford, poor.Verdict);

        // The refusal still names what it could not have, because a manager reading this club's
        // book is owed the price of the stand that did not happen.
        Assert.NotNull(poor.Project);
        Assert.True(poor.Cost > 0m);

        var rich = NpcStadiumPolicy.Decide(Club(5_000, 11_700));
        Assert.True(rich.Builds);
    }

    /// <summary>
    /// The reserve is the whole wage bill, not a share of the stand's price.
    /// </summary>
    [Fact]
    public void TheReserveIsTheSeasonWages()
    {
        var facts = new NpcStadiumFacts(Guid.NewGuid(), 5_000, 11_700, true, false, 2_500_000m, Wages);

        // Two and a quarter million is spent and the club still holds the whole wage bill.
        Assert.True(NpcStadiumPolicy.CanAfford(facts, 2_250_000m));

        // Two and a half is spent and it no longer does.
        Assert.False(NpcStadiumPolicy.CanAfford(facts, 2_500_000m));
    }

    /// <summary>
    /// Both questions are asked, and either one can stop it.
    /// </summary>
    /// <remarks>
    /// The world is full of clubs in one half of this pair and not the other. A club with a full
    /// bank and a comfortable ground has nowhere to put its money, and a club being turned away
    /// with two hundred thousand limos has somewhere to put it and nothing to put it with. A
    /// policy that only asked one of the two questions would build for the second group and
    /// leave the first group to hoard for ever.
    /// </remarks>
    [Fact]
    public void NeedAndMoneyAreAskedSeparatelyAndEitherOneStopsIt()
    {
        // Wants a stand and can pay for it.
        Assert.True(NpcStadiumPolicy.Decide(Club(5_000, 11_700, balance: Comfortable)).Builds);

        // Can pay and has no reason to.
        Assert.False(NpcStadiumPolicy.Decide(Club(5_000, 1_000, balance: Comfortable)).Builds);

        // Wants one badly and cannot.
        Assert.False(NpcStadiumPolicy.Decide(Club(5_000, 11_700, balance: Broke)).Builds);

        // Neither.
        Assert.False(NpcStadiumPolicy.Decide(Club(5_000, 1_000, balance: Broke)).Builds);
    }
}