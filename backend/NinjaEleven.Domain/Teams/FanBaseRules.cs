namespace NinjaEleven.Domain.Teams;

/// <summary>
/// What a season's football did to a club's crowd.
/// <para>
/// It is the whole of the season's football as the crowd remembers it, and nothing else: the
/// table the club finished in, the division it moved between, the titles it won and the trend it
/// arrived on. Everything the growth rule needs and nothing it does not, so a season's effect on
/// a crowd can be worked out — and tested — without a season.
/// </para>
/// </summary>
/// <param name="WonSomething">Whether the club won a championship, a cup or the Supercup.</param>
/// <param name="Promoted">Whether it ended the season a division above the one it started in.</param>
/// <param name="Relegated">Whether it ended the season a division below the one it started in.</param>
/// <param name="Position">Where it finished in its division, counted from one.</param>
/// <param name="ClubsInDivision">How many clubs were in that division.</param>
/// <param name="PointsPerGame">Its points a game across the season.</param>
/// <param name="Momentum">
/// How well it ended, as a number between minus one and one. A club that was last in October
/// and first in May arrives on the close with a positive trend, and that is a different club
/// from one that was first in October and last in May with the same 46 points.
/// </param>
public readonly record struct FanSeasonOutcome(
    bool WonSomething,
    bool Promoted,
    bool Relegated,
    int Position,
    int ClubsInDivision,
    double PointsPerGame,
    double Momentum);

/// <summary>
/// A club's crowd is a fact about the club, and the ladder it starts on and the rules it grows
/// by are both rules of the game rather than numbers a caller chose.
///
/// <para>
/// <b>The crowd is not derived from the ground.</b> That is the whole correction this file
/// exists for. A stand of five thousand seats says what a ground can hold; it says nothing at
/// all about how many people would come, and a crowd worked out as a share of the seats is a
/// crowd that can never exceed them. So the number lives here, on the club, and the ground is
/// only ever a ceiling applied to it at the kick-off — which is what makes a club too big for
/// its own stadium a state the game can be in rather than a contradiction.
/// </para>
///
/// <para>
/// <b>The ladder is wide on purpose.</b> A first-division club is seeded at tens of thousands
/// and its ground holds five thousand, so nearly every ground in the top division is a full
/// house with a demand that could not be served, and the unsold half of that demand is the
/// argument for building. A ladder seeded to fit the five thousand seats would make the first
/// work of expansion a response to nothing.
/// </para>
/// </summary>
public static class FanBaseRules
{
    /// <summary>
    /// The smallest a club's following is ever allowed to become.
    ///
    /// It is a floor and not zero because a relegation that emptied a club would leave a crowd
    /// that no season of football could refill, and the pyramid would be a place clubs go to
    /// disappear. A bottom club is small; it is never absent.
    /// </summary>
    public const int SmallestFanBase = 1_500;

    /// <summary>
    /// The largest a club's following is allowed to become.
    ///
    /// It is a ceiling because the growth rule already bounds a season, and a bound on a season
    /// alone still lets a club that keeps winning climb for ever. Nothing reads this number —
    /// a crowd past a hundred thousand makes no difference to any stadium the game can build —
    /// and it exists so that a number which has stopped meaning anything stops being printed.
    /// </summary>
    public const int LargestFanBase = 250_000;

    /// <summary>
    /// How far a season may move a crowd in either direction.
    ///
    /// A quarter either way, and it is the single most important number in the file. Without it
    /// a title is plus eight and a relegation is minus seven and a club on a long run compounds
    /// them into a hundred-fold in a decade; with it, the worst season a champion can have still
    /// leaves it recognisably the same club next September.
    /// </summary>
    public const double AnnualGrowthCap = 0.25;

    /// <summary>
    /// How much a season's good afternoons may add on top of the club's standing number.
    ///
    /// It is deliberately smaller than <see cref="AnnualGrowthCap"/>, because it is a different
    /// kind of thing: the annual cap bounds a year's account and this bounds a mood. A club that
    /// wins four derbies in September does not become a different club by December.
    /// </summary>
    public const double InSeasonGainCap = 0.08;

    /// <summary>
    /// The points a game that counts as having met expectations.
    ///
    /// Three points a game is perfection and one and a half is survival, so 1.65 is the middle
    /// of the two and is what a club is measured against before the table's own bottom is
    /// consulted. It is the only expectation in the game, and it is stated here rather than
    /// inferred from the table, because a rule nobody wrote down is a rule that changes the day
    /// somebody's table does.
    /// </summary>
    public const double ExpectedPointsPerGame = 1.65;

    /// <summary>What winning something is worth.</summary>
    public const double ChampionBonus = 0.08;

    /// <summary>What going up a division is worth.</summary>
    public const double PromotionBonus = 0.06;

    /// <summary>What finishing in the top half of the division is worth.</summary>
    public const double TopHalfBonus = 0.03;

    /// <summary>What a season far short of expectations costs.</summary>
    public const double DisappointmentPenalty = 0.04;

    /// <summary>What going down a division costs.</summary>
    public const double RelegationPenalty = 0.07;

    /// <summary>How many places from the top count as the top half of a division.</summary>
    public const int TopHalfPlaces = 4;

    /// <summary>
    /// How far a club's recent trend can move its season, either way.
    ///
    /// It is bounded by a quarter because that is how far a season can move a crowd at all: a
    /// trend that could double a season's account would make the account the smaller half of the
    /// rule, and two clubs finishing on the same forty-six points with one of them rising and the
    /// other sinking would not be having different seasons — they would be having different games.
    /// </summary>
    public const double MomentumSpread = 0.25;

    /// <summary>
    /// The ladder a club is seeded on: the middle of its division, the bottom and the top.
    ///
    /// <para>
    /// The bottom and the top are not decoration — they are the two ends <see cref="SeedFor"/>
    /// travels between, so a division's spread is stated once and every club on it is a point
    /// in that range. They are far enough apart for the best squad in a division to be a visibly
    /// different club from the worst, and close enough that a first-division club is never
    /// confused with a fourth-division one.
    /// </para>
    /// </summary>
    /// <param name="tier">The division, counted from one.</param>
    public static (int Middle, int Floor, int Ceiling) LadderFor(int tier) => tier switch
    {
        1 => (45_000, 30_000, 60_000),
        2 => (21_000, 12_000, 30_000),
        3 => (10_000, 5_000, 15_000),
        // A division below the bottom of the pyramid is a smaller league, and the ladder keeps
        // going down rather than stopping at a number that was only ever meant for four.
        _ => (4_250, 1_500, Math.Max(1_500, 7_000 - (tier - 4) * 1_500))
    };

    /// <summary>
    /// The crowd a club is given the day its world is drawn.
    /// <para>
    /// The whole of a division's range is used, from its floor for the weakest squad to its
    /// ceiling for the strongest. That is what makes the seed readable: the best club in the
    /// first division is the one whose ground is over-subscribed from the very first match,
    /// and the worst is the one whose ground is a third full before anything has happened.
    /// Anything narrower would seed a division as a single crowd and leave the ladder with no
    /// ends, which is a ladder that cannot be climbed.
    /// </para>
    /// </summary>
    /// <param name="tier">The division the club starts in, counted from one.</param>
    /// <param name="strengthPercentile">
    /// Where the club's squad sits among its own division's squads: zero for the weakest and
    /// one for the strongest. It is a percentile rather than a rating on purpose, so the ladder
    /// means the same thing in a division of sixteen and in a division of eight, and so no
    /// balance change to how a squad is rated can quietly move every crowd in the game.
    /// </param>
    public static int SeedFor(int tier, double strengthPercentile)
    {
        var (_, floor, ceiling) = LadderFor(tier);
        var percentile = Math.Clamp(strengthPercentile, 0.0, 1.0);

        var seeded = floor + ((ceiling - floor) * percentile);

        return Bounded((int)Math.Round(seeded / 50.0, MidpointRounding.AwayFromZero) * 50);
    }

    /// <summary>
    /// How much a season moved a crowd, as a fraction of what the club brought into it.
    ///
    /// <para>
    /// Bonuses and charges are added to one another and nothing is floored afterwards. A season
    /// that is good and bad at the same time — a cup won and a relegation, a title and fifteen
    /// defeats — is the ordinary case in a division's lower half, and a rule that guaranteed a
    /// minimum would quietly refund the charge: a relegated champion with no points would come
    /// out ahead of a mid-table club that merely drew its matches, which is the same season
    /// read backwards.
    /// </para>
    /// </summary>
    /// <param name="outcome">What the season's football did.</param>
    public static double GrowthFor(FanSeasonOutcome outcome)
    {
        var growth = 0.0;

        if (outcome.WonSomething) growth += ChampionBonus;

        // A club that goes up has, by definition, finished in the top four of its division —
        // the promotion slots are the top four — so it is paid the better of the two and not
        // both. Charging one season's football twice is what an `if` is for.
        if (outcome.Promoted) growth += PromotionBonus;
        else if (IsTopHalf(outcome)) growth += TopHalfBonus;

        if (outcome.Relegated) growth -= RelegationPenalty;
        else if (IsDisappointing(outcome)) growth -= DisappointmentPenalty;

        // And the expectations are subtracted, not chosen from: a relegated champion of the
        // second division was six points a game all season and still went down, and the two facts
        // are both true.
        growth -= DisappointmentPenalty * DeficitOf(outcome);

        // The trend is a multiplier on the account rather than a term in it, so a club on a run
        // that finished fifteenth is still a club on a run — and a relegation is not softened
        // into insignificance by having been preceded by a good October.
        return growth * (1.0 + MomentumSpread * Math.Clamp(outcome.Momentum, -1.0, 1.0));
    }

    /// <summary>
    /// The crowd a club has after one season of that growth.
    ///
    /// <para>
    /// It is the annual cap that makes the rule safe rather than the growth itself: the cap is
    /// applied here, once, to whatever the outcome worked out to, so no combination of bonuses
    /// and penalties can add its way past a quarter in a season. The floor and the ceiling are
    /// applied to the result rather than to the growth, because they are absolute numbers about
    /// a club and not percentages about a year.
    /// </para>
    /// </summary>
    public static int AfterAGrowth(int supporters, double growth)
    {
        var bounded = Math.Clamp(growth, -AnnualGrowthCap, AnnualGrowthCap);

        return Bounded((int)Math.Round(supporters * (1.0 + bounded), MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// The peak a season's good afternoons can be worth, over what the club started it with.
    /// </summary>
    /// <param name="openingSupporters">The club's crowd when the season opened.</param>
    /// <param name="form">How well the club is going, as a fraction: zero for nothing and one for everything.</param>
    public static int PeakFor(int openingSupporters, double form) =>
        Bounded((int)Math.Round(
            openingSupporters * (1.0 + InSeasonGainCap * Math.Clamp(form, 0.0, 1.0)),
            MidpointRounding.AwayFromZero));

    /// <summary>
    /// How far short of its expectations a season finished, as a fraction: zero for meeting them
    /// and one for collecting nothing at all.
    /// </summary>
    private static double DeficitOf(FanSeasonOutcome outcome)
    {
        if (outcome.PointsPerGame >= ExpectedPointsPerGame) return 0.0;

        return Math.Clamp((ExpectedPointsPerGame - outcome.PointsPerGame) / ExpectedPointsPerGame, 0.0, 1.0);
    }

    private static bool IsTopHalf(FanSeasonOutcome outcome) =>
        outcome.Position >= 1 && outcome.Position <= Math.Min(TopHalfPlaces, Math.Max(outcome.ClubsInDivision, 1));

    private static bool IsDisappointing(FanSeasonOutcome outcome)
    {
        if (outcome.ClubsInDivision < 1) return false;

        // The same four places as the top half, counted from the other end, so a rule that pays
        // the top four and punishes the bottom four is one rule rather than two that have to be
        // kept in step — and a division of fewer clubs than the bonus still punishes its own
        // bottom rather than punishing nobody.
        return outcome.Position >= Math.Max(1, outcome.ClubsInDivision - TopHalfPlaces + 1);
    }

    /// <summary>
    /// Puts a number of people inside the two bounds, and rounds it to the nearest fifty.
    ///
    /// <para>
    /// The rounding is to fifty rather than to one because a crowd of 31.247 is a number
    /// produced by arithmetic and a crowd of 31.250 is a number a club's page can say. It is
    /// also what stops the seed from depending on the order the clubs came out of the database
    /// in the last decimal place.
    /// </para>
    /// </summary>
    private static int Bounded(int supporters) =>
        Math.Clamp(supporters, SmallestFanBase, LargestFanBase);
}