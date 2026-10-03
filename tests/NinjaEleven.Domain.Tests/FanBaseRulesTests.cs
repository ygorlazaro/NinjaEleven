using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A club's crowd is a number that moves, and the two things that go wrong with a number that
/// moves are the same two things: it moves too far, and it moves for the wrong reason.
///
/// <para>
/// These are the rules, measured rather than argued about. Every constant in
/// <see cref="FanBaseRules"/> is a balance decision, and a balance decision nobody checks is a
/// number that survives only as long as the thing it was balanced against does.
/// </para>
/// </summary>
public class FanBaseRulesTests
{
    /// <summary>
    /// A crowd a division's clubs are drawn from, and the four divisions have to be told apart
    /// by it. A ladder where the second division seeds the same crowd as the fourth is a ladder
    /// that is not a ladder, and the whole pyramid collapses into one number.
    /// </summary>
    [Fact]
    public void ASeededCrowdFollowsTheDivisionLadder()
    {
        var middles = Enumerable.Range(1, 4)
            .Select(tier => FanBaseRules.SeedFor(tier, 0.5))
            .ToList();

        Assert.Equal(middles.OrderByDescending(crowd => crowd).ToList(), middles);
        Assert.All(middles, crowd => Assert.InRange(
            crowd,
            FanBaseRules.SmallestFanBase,
            FanBaseRules.LargestFanBase));
    }

    /// <summary>
    /// The best club in a division is seeded at the top of it and the worst at the bottom, and
    /// neither leaves the division's own band. A top club of the fourth division that fills a
    /// first-division ground before it has won anything is a crowd that has stopped meaning
    /// anything.
    /// </summary>
    [Fact]
    public void AClubsSquadDecidesWhereItSitsOnItsOwnDivisionsLadder()
    {
        foreach (var tier in CompetitionRules.Tiers())
        {
            var (middle, floor, ceiling) = FanBaseRules.LadderFor(tier);

            var strongest = FanBaseRules.SeedFor(tier, 1.0);
            var weakest = FanBaseRules.SeedFor(tier, 0.0);

            Assert.True(strongest > weakest, $"Tier {tier} seeds its strongest club below its weakest.");

            // And they reach the two ends, rather than sitting somewhere inside the band. A seed
            // that covered half of its own ladder would leave the bottom third of every division
            // with a crowd no club in that division could ever be given, and the ladder would
            // have two ends it never actually reached.
            Assert.Equal(ceiling, strongest);
            Assert.Equal(floor, weakest);

            // The middle of the ladder is where the club nobody has said anything about is,
            // and it is the middle of the ladder rather than an arbitrary point in it.
            Assert.Equal(middle, FanBaseRules.SeedFor(tier, 0.5));
        }
    }

    /// <summary>
    /// The same club is not the same crowd in two different worlds.
    ///
    /// It is rounded to the nearest fifty so that a crowd is a number a club's page can say, and
    /// so that the seed does not depend on the order the clubs came out of the database in the
    /// last decimal place. A crowd of 31.247 is a number produced by arithmetic.
    /// </summary>
    [Fact]
    public void ASeededCrowdIsARoundNumberOfPeople()
    {
        foreach (var percentile in new[] { 0.0, 0.17, 0.33, 0.5, 0.66, 0.83, 1.0 })
        {
            var crowd = FanBaseRules.SeedFor(2, percentile);

            Assert.Equal(0, crowd % 50);
        }
    }

    /// <summary>
    /// The same result is not counted twice.
    ///
    /// <para>
    /// The worry this settles is real but narrower than it looks: a championship and a promotion
    /// cannot both happen to the same club, because only a club that finished in the top four
    /// goes up and the first division's winner does not go up at all. What genuinely overlaps is
    /// a promotion with a top-four finish — which is the same four places — so the promotion is
    /// paid instead of the top half rather than paid alongside it.
    /// </para>
    /// </summary>
    [Fact]
    public void AClubThatGoesUpIsPaidThePromotionAndNotTheTopHalfAsWell()
    {
        // Two clubs that both finished in the top four and both met expectations: one went up,
        // the other stayed. The only thing that separates them is the movement, so the whole of
        // the difference is the promotion and nothing else.
        var promoted = new FanSeasonOutcome(
            WonSomething: false, Promoted: true, Relegated: false,
            Position: 2, ClubsInDivision: 16, PointsPerGame: 2.0, Momentum: 0.0);

        var stayedButPlaced = new FanSeasonOutcome(
            WonSomething: false, Promoted: false, Relegated: false,
            Position: 2, ClubsInDivision: 16, PointsPerGame: 2.0, Momentum: 0.0);

        Assert.True(FanBaseRules.PromotionBonus > FanBaseRules.TopHalfBonus);

        // The two clubs above differ only in the movement, and the difference is what the
        // promotion is worth over the top half — not the promotion on top of it, which is the
        // double count the `if` is there to prevent.
        Assert.Equal(
            FanBaseRules.PromotionBonus - FanBaseRules.TopHalfBonus,
            FanBaseRules.GrowthFor(promoted) - FanBaseRules.GrowthFor(stayedButPlaced),
            6);

        // And the two are not the same season: a club that went up on a poor run is still a club
        // that went up, and is still charged for the run.
        var promotedAndDisappointing = new FanSeasonOutcome(
            WonSomething: false, Promoted: true, Relegated: false,
            Position: 2, ClubsInDivision: 16, PointsPerGame: 0.4, Momentum: 0.0);

        Assert.True(FanBaseRules.GrowthFor(promotedAndDisappointing) < FanBaseRules.GrowthFor(promoted));
    }

    /// <summary>
    /// The division movement rules make the double-count impossible from the other side, and
    /// this reads them rather than trusting the arithmetic above.
    /// </summary>
    [Fact]
    public void AChampionOfTheTopDivisionIsNeverPromotedSoTheTwoCannotOverlap()
    {
        // The table arrives in the order the clubs finished in — the movement rule reads a
        // club's place from where it sits, the same way the season close hands it over.
        var table = Enumerable.Range(0, 16)
            .Select(_ => new StandingEntry { TeamId = Guid.NewGuid(), Played = 30 })
            .ToList();

        var movement = DivisionMovement.From(1, table);

        var champion = movement.Movements.Single(entry => entry.Position == 1);

        Assert.Equal(1, champion.ToTier);
        Assert.False(champion.IsPromoted);
    }

    /// <summary>
    /// Going down costs a crowd, and it costs it whichever end of the table the club was at.
    ///
    /// <para>
    /// The mistake this is here for is a single character. A penalty added instead of subtracted
    /// is not a crowd that grew too slowly: it is the pyramid's biggest fans celebrating every
    /// relegation, and it is invisible in a table of numbers because every cell is still a
    /// percentage and still inside the annual cap.
    /// </para>
    /// </summary>
    [Fact]
    public void AClubThatGoesDownIsChargedForItAndNotCongratulatedOnIt()
    {
        // A champion of the second division is relegated with no points at all — the exact
        // season where the title bonus and the movement have to be told apart.
        var demotedChampion = new FanSeasonOutcome(
            WonSomething: true, Promoted: false, Relegated: true,
            Position: 1, ClubsInDivision: 16, PointsPerGame: 0.0, Momentum: 0.0);

        var theSameSeasonWithoutTheRelegation = new FanSeasonOutcome(
            WonSomething: true, Promoted: false, Relegated: false,
            Position: 1, ClubsInDivision: 16, PointsPerGame: 0.0, Momentum: 0.0);

        // A comparison against a different club can only tell a charge from a reward if the
        // other club is worse; a comparison against the same season with one word changed can
        // tell it exactly.
        Assert.Equal(
            -FanBaseRules.RelegationPenalty,
            FanBaseRules.GrowthFor(demotedChampion) - FanBaseRules.GrowthFor(theSameSeasonWithoutTheRelegation),
            6);

        // And the title does not survive it: a season that ends in a relegation with no points
        // is a bad season whatever it won in April.
        Assert.True(FanBaseRules.GrowthFor(demotedChampion) < 0);
    }

    /// <summary>
    /// The bottom of a division is punished and the middle is not, and the dividing line is the
    /// same four places the top half is rewarded over.
    /// </summary>
    [Fact]
    public void TheBottomOfADivisionIsTheOneThatIsPunished()
    {
        var twelfth = new FanSeasonOutcome(
            WonSomething: false, Promoted: false, Relegated: false,
            Position: 12, ClubsInDivision: 16, PointsPerGame: 1.0, Momentum: 0.0);

        var thirteenth = twelfth with { Position = 13 };

        Assert.True(
            FanBaseRules.GrowthFor(thirteenth) < FanBaseRules.GrowthFor(twelfth),
            "The thirteenth club was not punished while the twelfth was.");

        Assert.Equal(
            -FanBaseRules.DisappointmentPenalty,
            FanBaseRules.GrowthFor(thirteenth) - FanBaseRules.GrowthFor(twelfth),
            6);
    }

    /// <summary>
    /// Six clubs have a top four and a bottom four, and the middle two are in both.
    ///
    /// <para>
    /// That is not a rule in trouble; it is a division too small for the rule to fit. The
    /// honest answer is that a club inside both halves is paid and charged, and the assertion
    /// below is that both halves reach it: the third of six is the second of six less the
    /// disappointment charge, and the fourth of six plus the top-half bonus.
    /// </para>
    /// </summary>
    [Fact]
    public void AClubInsideBothHalvesOfASmallDivisionIsPaidAndCharged()
    {
        var season = (int position) => new FanSeasonOutcome(
            WonSomething: false, Promoted: false, Relegated: false,
            Position: position, ClubsInDivision: 6, PointsPerGame: 1.0, Momentum: 0.0);

        var second = season(2);   // top half only
        var third = season(3);    // both halves
        var fifth = season(5);    // bottom half only

        Assert.Equal(
            -FanBaseRules.DisappointmentPenalty,
            FanBaseRules.GrowthFor(third) - FanBaseRules.GrowthFor(second),
            6);

        Assert.Equal(
            FanBaseRules.TopHalfBonus,
            FanBaseRules.GrowthFor(third) - FanBaseRules.GrowthFor(fifth),
            6);
    }

    /// <summary>
    /// A season moves a crowd by at most a quarter, whichever way it went.
    ///
    /// <para>
    /// This is the single most important number in the rules. Without it the bonuses compound:
    /// a title is plus eight and a relegation is minus seven and a club on a long run reaches a
    /// hundredfold in a decade. With it, the worst season a champion can have still leaves it
    /// recognisably the same club next September.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1, 45_000, 2.9, 0, 0.55)]      // champion on a perfect run
    [InlineData(1, 45_000, 0.0, 15, 0.2)]      // last but one, cold, terrible season
    [InlineData(4, 1_600, 0.0, 16, 0.0)]        // bottom of the pyramid, nothing
    [InlineData(4, 250_000, 2.9, 1, 0.3)]      // a very large club, everything at once
    public void ASeasonCannotMoveACrowdByMoreThanAQuarter(int tier, int crowd, double ppg, int position, double momentum)
    {
        var outcome = new FanSeasonOutcome(
            WonSomething: position == 1,
            Promoted: false,
            Relegated: position == 16 && tier > 1,
            Position: position,
            ClubsInDivision: 16,
            PointsPerGame: ppg,
            Momentum: momentum);

        var after = FanBaseRules.AfterAGrowth(crowd, FanBaseRules.GrowthFor(outcome));

        Assert.InRange(after, (int)(crowd * (1 - FanBaseRules.AnnualGrowthCap)), (int)(crowd * (1 + FanBaseRules.AnnualGrowthCap)) + 1);
    }

    /// <summary>
    /// A crowd never empties and never becomes a number nothing reads.
    ///
    /// A relegation that emptied a club would leave a crowd no season of football could refill,
    /// and the pyramid would be a place clubs go to disappear. A bottom club is small; it is
    /// never absent, and it is never a national institution either.
    /// </summary>
    [Theory]
    [InlineData(1_500)]
    [InlineData(2_000)]
    [InlineData(45_000)]
    public void ACrowdIsAlwaysInsideItsBounds(int crowd)
    {
        Assert.InRange(
            FanBaseRules.AfterAGrowth(crowd, -10.0),
            FanBaseRules.SmallestFanBase,
            FanBaseRules.LargestFanBase);

        Assert.InRange(
            FanBaseRules.AfterAGrowth(crowd, 10.0),
            FanBaseRules.SmallestFanBase,
            FanBaseRules.LargestFanBase);
    }

    /// <summary>
    /// A club never loses more than a quarter of its following in one season, however bad the
    /// season was and however well it had been going before it.
    /// </summary>
    [Fact]
    public void AClubOnARunThatIsThenRelegatedLosesNoMoreThanAQuarter()
    {
        var onARun = new FanSeasonOutcome(
            WonSomething: false, Promoted: false, Relegated: false,
            Position: 3, ClubsInDivision: 16, PointsPerGame: 2.2, Momentum: 1.0);

        var relegatedAfterwards = new FanSeasonOutcome(
            WonSomething: false, Promoted: false, Relegated: true,
            Position: 16, ClubsInDivision: 16, PointsPerGame: 0.6, Momentum: -1.0);

        var after = FanBaseRules.AfterAGrowth(50_000, FanBaseRules.GrowthFor(relegatedAfterwards));

        Assert.True(
            after >= (int)(50_000 * (1 - FanBaseRules.AnnualGrowthCap)),
            $"A relegated club fell from 50.000 to {after:N0}, which is more than a quarter.");
    }

    /// <summary>
    /// A season's best afternoon is worth something and a season's account is worth something
    /// else, and the two are not the same number.
    ///
    /// <para>
    /// The peak is capped lower than the year on purpose: a mood is not an account. A club that
    /// wins four derbies in September does not become a different club by December, and a peak
    /// that fed the following season's opening number would make one afternoon worth a year of
    /// growth.
    /// </para>
    /// </summary>
    [Fact]
    public void ASasonsPeakIsCappedLowerThanItsAccount()
    {
        var onARun = FanBaseRules.PeakFor(20_000, 1.0);

        Assert.Equal(
            (int)Math.Round(20_000 * (1 + FanBaseRules.InSeasonGainCap)),
            onARun);

        Assert.True(
            FanBaseRules.InSeasonGainCap < FanBaseRules.AnnualGrowthCap,
            "A mood that could be as big as a year would make the year meaningless.");

        // Nothing about the season can push a peak past the cap either.
        Assert.Equal(onARun, FanBaseRules.PeakFor(20_000, 50.0));
    }

    /// <summary>
    /// A row is opened with the number the season starts on and closes with the number it ends
    /// on, so a club's page can print a change without dividing two rows it happened to load.
    /// </summary>
    [Fact]
    public void ACrowdRowCarriesBothEndsOfItsSeason()
    {
        var now = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var fanBase = TeamFanBase.Create(Guid.NewGuid(), Guid.NewGuid(), 20_000, now);

        Assert.Equal(20_000, fanBase.Supporters);
        Assert.Equal(20_000, fanBase.OpeningSupporters);
        Assert.Equal(20_000, fanBase.PeakSupporters);
        Assert.False(fanBase.Grew);
        Assert.Equal(0, fanBase.Change);

        fanBase.RaiseThePeakTo(21_500);
        fanBase.CloseTheSeasonWith(21_000, 23_000, now.AddDays(30));

        Assert.Equal(21_000, fanBase.Supporters);
        Assert.Equal(20_000, fanBase.OpeningSupporters);
        Assert.Equal(23_000, fanBase.PeakSupporters);
        Assert.True(fanBase.Grew);
        Assert.Equal(1_000, fanBase.Change);
    }

    /// <summary>
    /// A peak only ever goes up, and closing below it pulls the peak down with the closing
    /// number rather than leaving a season whose best moment was better than its last.
    /// </summary>
    [Fact]
    public void ASeasonsPeakIsTheBestMomentItHad()
    {
        var now = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var fanBase = TeamFanBase.Create(Guid.NewGuid(), Guid.NewGuid(), 20_000, now);

        fanBase.RaiseThePeakTo(23_000);
        fanBase.RaiseThePeakTo(21_000);

        Assert.Equal(23_000, fanBase.PeakSupporters);

        // A relegation closes the season below its own peak. The peak is the best moment anybody
        // had, so it is kept — but a club cannot finish a season below the floor.
        fanBase.CloseTheSeasonWith(10, 10, now.AddDays(30));

        Assert.Equal(FanBaseRules.SmallestFanBase, fanBase.Supporters);
        Assert.Equal(FanBaseRules.SmallestFanBase, fanBase.PeakSupporters);
    }
}