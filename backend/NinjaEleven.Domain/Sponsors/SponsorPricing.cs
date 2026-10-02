namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// What a shirt deal is worth, and who is willing to put their name on it.
///
/// <para>
/// A sponsor pays for exposure, so the price of a shirt is priced the way a crowd is priced:
/// by the division the club plays in, by where it stands, by how it has been playing, by how
/// much of the season is still ahead of it and by who else is on the same ground. The whole
/// vocabulary lives here rather than in the service that draws offers, so the manager's price,
/// an NPC's price and the ceiling that keeps a shirt from being worth more than a title are
/// one set of numbers and not three.
/// </para>
/// </summary>
public static class SponsorPricing
{
    /// <summary>
    /// What a slot on a mid-table shirt costs, per match, for a regional sponsor with a
    /// half-full slate.
    ///
    /// <para>
    /// The ladder is not decoration: each base is a small fraction of what that division's
    /// title pays — well under a percent of it, and about a fifth of a mid-table gate — which
    /// is what keeps the four of them honest to each other. A shirt is worth something to a
    /// club that finishes nowhere, and a title is worth something no shirt could buy, so a
    /// purse doubled would not make a shirt twice as valuable as the decision it competes
    /// with. The number that matters is the order and the gap between the four, both of
    /// which a test holds.
    /// </para>
    /// </summary>
    public static decimal BaseFeeForTier(int tier) => tier switch
    {
        1 => 20_000m,
        2 => 13_000m,
        3 => 7_500m,
        4 => 4_000m,
        _ => 7_500m
    };

    /// <summary>What a local company pays for the same slot a regional one would.</summary>
    public const double LocalWeightFactor = 0.70;

    /// <summary>What a national company pays for the same slot a regional one would.</summary>
    public const double NationalWeightFactor = 1.35;

    /// <summary>
    /// How much a sponsor pays for a club when its slate is empty, and how much less when it
    /// is full.
    ///
    /// <para>
    /// It is a band of ±15% and no wider, on purpose. A sponsor signing its eighth club is
    /// taking a slot it barely needs, and one signing its first is filling a gap; both are
    /// real, and neither is worth a factor of two. The number is shown to the manager rather
    /// than folded into the fee, because a price whose cause is invisible is a price nobody
    /// can choose between.
    /// </para>
    /// </summary>
    public const double PortfolioCeiling = 1.15;

    public const double PortfolioFloor = 0.85;

    /// <summary>
    /// How far a first-division club in the middle of a full stadium is above the average one.
    /// The low end is the same club with nothing going for it, and the two are used together:
    /// a factor that could only move a price upwards would make every well-run club look
    /// broken.
    /// </summary>
    public const double AppealCeiling = 1.35;

    public const double AppealFloor = 0.75;

    /// <summary>How far a club's form moves a fee, over its last five league matches.</summary>
    public const double FormFloor = 0.85;

    public const double FormCeiling = 1.15;

    /// <summary>What still being in the cup is worth to a shirt.</summary>
    public const double InCupFactor = 1.15;

    /// <summary>What reaching a cup final is worth to a shirt.</summary>
    public const double CupFinalFactor = 1.25;

    /// <summary>
    /// How far the crowd moves a fee, against the average crowd of the club's own division.
    /// The ends are held flat rather than extrapolated, like the price of a seat: a sponsor
    /// does not pay six times the fee for a ground that is three times as full as the one it
    /// pays a quarter for.
    /// </summary>
    public const double CrowdSpread = 0.10;

    /// <summary>How many league matches a club's form is read over.</summary>
    public const int FormMatches = 5;

    /// <summary>
    /// The appeal a sponsor with no particular ambition asks for: none at all. A local shop
    /// that sponsors a fourth-division club is not being choosy, it is buying the local
    /// league's television audience.
    /// </summary>
    public const double DefaultMinAppeal = 0.0;

    /// <summary>What a regional company asks for before it will put its name on a club.</summary>
    public const double RegionalMinAppeal = 0.85;

    /// <summary>What a national company asks for: a club above average, in a top division.</summary>
    public const double NationalMinAppeal = 1.05;

    /// <summary>
    /// How many clubs a sponsor of each size keeps on its shirt at once.
    /// </summary>
    public static int MaxClubsForWeight(int weight) => weight switch
    {
        1 => 2,
        2 => 4,
        3 => 8,
        _ => 4
    };

    /// <summary>The fee a sponsor of a given size pays for the same slot.</summary>
    public static double WeightFactor(int weight) => weight switch
    {
        1 => LocalWeightFactor,
        3 => NationalWeightFactor,
        _ => 1.0
    };

    /// <summary>The best division a sponsor of a given size will approach.</summary>
    public static int MaxTierForWeight(int weight) => weight switch
    {
        1 => 4,
        2 => 3,
        _ => 2
    };

    /// <summary>What a sponsor of a given size asks of a club before it will approach it.</summary>
    public static double MinAppealForWeight(int weight) => weight switch
    {
        1 => DefaultMinAppeal,
        3 => NationalMinAppeal,
        _ => RegionalMinAppeal
    };

    /// <summary>
    /// What the sponsor pays for a slot on a club that is going exactly as its division's
    /// average club goes.
    /// </summary>
    public static decimal BaseFeeFor(int tier, int sponsorWeight) =>
        Math.Round(BaseFeeForTier(tier) * (decimal)WeightFactor(sponsorWeight), 2);

    /// <summary>
    /// What a sponsor pays for a club it has already put <paramref name="clubs"/> of its
    /// <paramref name="maxClubs"/> shirts on.
    /// </summary>
    public static double PortfolioFactor(int clubs, int maxClubs)
    {
        if (maxClubs < 1)
        {
            return 1.0;
        }

        var full = Math.Clamp(clubs / (double)maxClubs, 0.0, 1.0);

        return PortfolioCeiling - (PortfolioCeiling - PortfolioFloor) * full;
    }

    /// <summary>
    /// How full a ground is worth to a sponsor, against the average crowd of the division.
    /// </summary>
    public static double CrowdFactor(double clubAttendance, double divisionAverage)
    {
        if (divisionAverage <= 0)
        {
            return 1.0;
        }

        var ratio = clubAttendance / divisionAverage;

        return Math.Clamp(ratio, 1.0 - CrowdSpread, 1.0 + CrowdSpread);
    }

    /// <summary>
    /// What a run of results is worth to a shirt, from the points a club has taken per match.
    /// Three points a game is the ceiling and none is the floor, and it is read as a rate
    /// because five wins out of five and two wins out of five are the same number of games of
    /// evidence.
    /// </summary>
    public static double FormFactor(double pointsPerGame)
    {
        var clamped = Math.Clamp(pointsPerGame, 0.0, 3.0);

        return FormFloor + (FormCeiling - FormFloor) * (clamped / 3.0);
    }

    /// <summary>
    /// How attractive a club is to a company looking for its name in front of people, as a
    /// multiple of the average club doing average things.
    ///
    /// <para>
    /// The four factors are the ones a crowd is made of, and they are the ones the gate is
    /// already priced by — so a sponsor is paying a share of what the crowd already says the
    /// club is worth, rather than a second opinion about it. The product is clamped at both
    /// ends: a club that is winning everything in the bottom division is not the most valuable
    /// club in the country, it is the most valuable club in the fourth division, and a price
    /// that could not tell those apart would be a price that sold the same shirt twice.
    /// </para>
    ///
    /// <para>
    /// A fact that is missing reads as neutral and not as zero. A club that has not played a
    /// match has no position, no form and no crowd of its own, and a club whose facts are all
    /// zero is a club with a terrible season — which is a different club, and a fee that could
    /// not tell them apart would price a new season's first day like its last.
    /// </para>
    /// </summary>
    public static double Appeal(ClubSponsorFacts facts)
    {
        var position = facts.Position ?? facts.ClubsInDivision;
        var form = facts.PointsPerGame ?? NeutralPointsPerGame;
        var crowd = facts.AverageAttendance ?? facts.DivisionAverageAttendance;

        var product =
            PositionFactor(position, facts.ClubsInDivision)
            * FormFactor(form)
            * CupFactor(facts.StillInCup, facts.ReachedCupFinal)
            * CrowdFactor(crowd, facts.DivisionAverageAttendance);

        return Math.Clamp(product, LowestPossibleAppeal, HighestPossibleAppeal);
    }

    /// <summary>
    /// The points a game that neither raise nor lower a fee: a little under a win every
    /// match, which is what a club that has not played yet is treated as.
    /// </summary>
    public const double NeutralPointsPerGame = 1.5;

    private static double LowestPossibleAppeal =>
        AppealFloor * FormFloor * 1.0 * (1.0 - CrowdSpread);

    private static double HighestPossibleAppeal =>
        AppealCeiling * CupFinalFactor * (1.0 + CrowdSpread);

    /// <summary>
    /// How full the ground is for a club in a given place in its table, as a fraction of the
    /// division rather than as a fixed list of steps, so a division of eight clubs and one of
    /// sixteen give the same spread between the best-placed and the worst-placed club.
    /// </summary>
    public static double PositionFactor(int position, int clubsInDivision)
    {
        if (position < 1) position = 1;
        if (clubsInDivision < 1) clubsInDivision = 1;

        if (clubsInDivision == 1)
        {
            return AppealCeiling;
        }

        var relative = (position - 1.0) / (clubsInDivision - 1.0);
        var clamped = Math.Clamp(relative, 0.0, 1.0);

        return AppealFloor + (AppealCeiling - AppealFloor) * (1.0 - clamped);
    }

    /// <summary>
    /// What being in the cup is worth to a shirt. A club still in a competition is a club
    /// whose name is on a screen for another round, and one that has reached a final is on it
    /// one more time than anybody expected.
    /// </summary>
    public static double CupFactor(bool stillInCup, bool reachedFinal)
    {
        if (reachedFinal)
        {
            return CupFinalFactor;
        }

        return stillInCup ? InCupFactor : 1.0;
    }

    /// <summary>
    /// The whole price of a shirt, per match.
    /// </summary>
    /// <remarks>
    /// The base carries the division and the size of the company; everything after it is the
    /// club asking for more or for less than that base says, which is the only part of the
    /// number that a season can change.
    /// </remarks>
    public static decimal FeeFor(int tier, Sponsor sponsor, ClubSponsorFacts facts)
    {
        var appeal = Appeal(facts);

        var fee = BaseFeeFor(tier, sponsor.Weight) * (decimal)appeal * (decimal)PortfolioFactor(facts.SponsorClubs, sponsor.MaxClubs);

        // Rounded to the nearest hundred limos: a shirt deal is a negotiation between two
        // companies, and neither of them quotes a club L$ 137,432.
        return Math.Round(fee / 100m) * 100m;
    }
}

/// <summary>
/// Everything about a club that a sponsor's price depends on, read once.
///
/// <para>
/// It is a record rather than fourteen arguments because these facts are read by a service
/// that has to answer for sixty-four clubs at a time, and a signature that long is a signature
/// nobody fills in correctly. A missing fact is null and not a zero: a club that has not
/// played a match has no form and has no crowd, and both of those are different from a club
/// whose form and crowd are bad.
/// </para>
/// </summary>
public record ClubSponsorFacts
{
    /// <summary>Where the club stands in its table. Null before the first matchday.</summary>
    public int? Position { get; init; }

    /// <summary>How many clubs share the division, which is what a position is a share of.</summary>
    public int ClubsInDivision { get; init; } = 1;

    /// <summary>Points a game over the club's last five league matches.</summary>
    public double? PointsPerGame { get; init; }

    /// <summary>Whether the club is still in the cup it entered.</summary>
    public bool StillInCup { get; init; }

    /// <summary>Whether the club has reached the last match of the cup.</summary>
    public bool ReachedCupFinal { get; init; }

    /// <summary>The club's own average crowd over its recent home matches.</summary>
    public double? AverageAttendance { get; init; }

    /// <summary>The average crowd of the club's division, which is what its own is measured against.</summary>
    public double DivisionAverageAttendance { get; init; }

    /// <summary>How many clubs this sponsor already has on its shirt.</summary>
    public int SponsorClubs { get; init; }
}