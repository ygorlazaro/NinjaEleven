using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Sponsors;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What a shirt is worth.
///
/// <para>
/// These are the numbers a manager budgets his season against, so the properties asserted here
/// are the ones that have to survive any later retune of a prize or a division: a base that
/// stays a fraction of the title it belongs to, a price that rises with everything a crowd is
/// made of, and a missing fact that reads as neutral rather than as a club in free fall.
/// </para>
/// </summary>
public class SponsorPricingTests
{
    private static Sponsor Company(int weight) => Sponsor.Create(
        "Empresa", "Comércio", "#0066CC", SponsorSize.ForWeight(weight));

    private static ClubSponsorFacts AverageFirstDivisionClub => new()
    {
        Position = 8,
        ClubsInDivision = 16,
        PointsPerGame = 1.5,
        AverageAttendance = 2_000,
        DivisionAverageAttendance = 2_000,
        SponsorClubs = 0
    };

    /// <summary>
    /// A shirt is a rounding error beside the title, in every division: a whole contract is
    /// under a percent of the purse that division pays out. If a shirt ever came to rival the
    /// championship, the two would stop being two different kinds of money — and the manager
    /// would be choosing between them for the wrong reason.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AShirtIsARoundingErrorBesideTheTitle(int tier)
    {
        var contract = SponsorPricing.BaseFeeFor(tier, 2) * SponsorRules.BaseOfferLength * 2;
        var share = contract / PrizeRules.PurseForTier(tier);

        Assert.InRange(share, 0m, 0.01m);
    }

    /// <summary>
    /// The four divisions stay a ladder, and each step down is a real drop rather than a
    /// drift — the gap is what tells a manager that moving down the pyramid costs him money
    /// he can see.
    /// </summary>
    [Fact]
    public void TheFourDivisionsAreALadder()
    {
        var bases = Enumerable.Range(1, 4)
            .Select(tier => SponsorPricing.BaseFeeFor(tier, 2))
            .ToList();

        for (var tier = 1; tier < bases.Count; tier++)
        {
            Assert.True(
                bases[tier] < bases[tier - 1],
                $"tier {tier + 1} at {bases[tier]} should be under tier {tier} at {bases[tier - 1]}");
        }
    }

    /// <summary>A club higher up pays more than the same club one place below it.</summary>
    [Fact]
    public void AClubHigherInTheTableIsWorthMore()
    {
        var top = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { Position = 1 });
        var bottom = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { Position = 16 });

        Assert.True(top > bottom, $"top {top} should beat bottom {bottom}");
    }

    [Fact]
    public void AClubInFormIsWorthMore()
    {
        var cold = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { PointsPerGame = 0.0 });
        var hot = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { PointsPerGame = 3.0 });

        Assert.True(hot > cold, $"hot {hot} should beat cold {cold}");
    }

    [Fact]
    public void AFullerGroundIsWorthMore()
    {
        var empty = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { AverageAttendance = 500 });
        var full = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { AverageAttendance = 8_000 });

        Assert.True(full > empty, $"full {full} should beat empty {empty}");
    }

    [Fact]
    public void AClubStillInTheCupIsWorthMore()
    {
        var knockedOut = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { StillInCup = false });
        var stillIn = SponsorPricing.FeeFor(1, Company(2), AverageFirstDivisionClub with { StillInCup = true });

        Assert.True(stillIn > knockedOut, $"in the cup {stillIn} should beat out of it {knockedOut}");
    }

    /// <summary>
    /// A crowd is not extrapolated, and neither is a position: a ground three times as full
    /// does not pay six times the fee, and the price of a seat is held flat at both ends for
    /// the same reason.
    /// </summary>
    [Fact]
    public void ACrowdIsNotExtrapolatedPastTheSpread()
    {
        var average = SponsorPricing.CrowdFactor(2_000, 2_000);
        var tenTimes = SponsorPricing.CrowdFactor(20_000, 2_000);

        Assert.Equal(1.0, average, 3);
        Assert.Equal(1.0 + SponsorPricing.CrowdSpread, tenTimes, 3);
    }

    /// <summary>
    /// A club that has not played a match is priced as the average club and not as a club in
    /// free fall. Zero here would be the one reading of "no form yet" that a sponsor could not
    /// act on, and would price a season's first day like its last.
    /// </summary>
    [Fact]
    public void AClubThatHasNotPlayedIsPricedLikeAnAverageOne()
    {
        var unplayed = new ClubSponsorFacts { ClubsInDivision = 16 };
        var dreadful = AverageFirstDivisionClub with
        {
            Position = 16,
            PointsPerGame = 0.0,
            AverageAttendance = 0,
            DivisionAverageAttendance = 2_000
        };

        var firstDay = SponsorPricing.FeeFor(1, Company(2), unplayed);
        var lastDay = SponsorPricing.FeeFor(1, Company(2), dreadful);

        Assert.True(firstDay > lastDay, $"an unplayed club {firstDay} should beat a dreadful one {lastDay}");
    }

    [Fact]
    public void ACompanyAlreadyOnItsEighthClubPaysLessForTheNinth()
    {
        Assert.True(
            SponsorPricing.PortfolioFactor(1, 8) > SponsorPricing.PortfolioFactor(8, 8));
    }

    [Fact]
    public void ANationalCompanyPaysMoreThanALocalOneForTheSameClub()
    {
        var local = SponsorPricing.FeeFor(1, Company(1), AverageFirstDivisionClub);
        var national = SponsorPricing.FeeFor(1, Company(3), AverageFirstDivisionClub);

        Assert.True(national > local, $"national {national} should beat local {local}");
    }

    /// <summary>
    /// A shirt deal is a negotiation between two companies, and neither of them quotes a club
    /// L$ 137,432.
    /// </summary>
    [Fact]
    public void AQuoteIsRoundedToTheNearestHundred()
    {
        var fee = SponsorPricing.FeeFor(2, Company(2), AverageFirstDivisionClub with { Position = 7 });

        Assert.Equal(0m, fee % 100m);
    }

    /// <summary>
    /// Every division can still be sold a shirt: a price of zero would be a company nobody
    /// would approach, and the bottom of the pyramid is where a sponsor's local audience is.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryDivisionHasAShirtWorthSelling(int tier)
    {
        var facts = AverageFirstDivisionClub with
        {
            Position = 12,
            PointsPerGame = 0.6,
            AverageAttendance = 300
        };

        Assert.True(SponsorPricing.FeeFor(tier, Company(1), facts) > 0m);
    }
}

/// <summary>
/// What size of company a sponsor is, and where that size comes from.
/// </summary>
public class SponsorSizeTests
{
    /// <summary>
    /// A catalog of thirty companies that are all the same size is a catalog with no pyramid
    /// in it: every one of them paying the same for the same shirt, so a club's place, form
    /// and crowd would decide the price and the company would never decide whether it wanted
    /// the club at all.
    /// </summary>
    [Fact]
    public void AWholeCatalogIsNotOneSize()
    {
        var sizes = Enumerable.Range(0, 30)
            .Select(index => SponsorRules.SizeFor($"Empresa {index}"))
            .Select(size => size.Weight)
            .Distinct()
            .ToList();

        Assert.Equal(3, sizes.Count);
    }

    /// <summary>
    /// A company's size is its name, not a counter: a world seeded today and a world migrated
    /// onto the columns have to arrive at the same book, or a sponsor is a different company
    /// depending on how the database was built.
    /// </summary>
    [Fact]
    public void TheSizeOfANameIsTheSameEveryTime()
    {
        Assert.Equal(SponsorRules.SizeFor("Banco do Vale"), SponsorRules.SizeFor("Banco do Vale"));
    }

    /// <summary>
    /// The four numbers travel together, and they are the numbers the rest of the world prices
    /// a shirt by — a company seeded as national with a local club's appetite could not be
    /// reasoned about by anything that reads a sponsor.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EverySizeIsACompanyTheWorldCanReasonAbout(int weight)
    {
        var size = SponsorSize.ForWeight(weight);

        Assert.True(size.MaxClubs >= 1);
        Assert.InRange(size.MaxTier, 1, 4);
        Assert.True(
            Math.Abs(size.MinAppeal - SponsorPricing.MinAppealForWeight(weight)) < 0.0001);
    }

    [Fact]
    public void ABiggerCompanyPaysMoreKeepsMoreShirtsAndWorksHigherUp()
    {
        var local = SponsorSize.Local;
        var regional = SponsorSize.Regional;
        var national = SponsorSize.National;

        Assert.True(national.MaxClubs > regional.MaxClubs);
        Assert.True(regional.MaxClubs > local.MaxClubs);
        Assert.True(national.MinAppeal > regional.MinAppeal);
        Assert.True(regional.MinAppeal > local.MinAppeal);
        Assert.True(local.MaxTier > national.MaxTier);
    }
}
