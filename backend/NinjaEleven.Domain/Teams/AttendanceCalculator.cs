namespace NinjaEleven.Domain.Teams;

/// <summary>
/// How much a match matters, as far as a crowd is concerned.
///
/// It is an enum and not a number because the number is a rule of the game: a match decided
/// on a knife edge fills a ground that an ordinary one does not, and the size of that gap is
/// the same everywhere. A manager will later be able to make a match important by what it
/// means — a title race, a relegation fight, a derby — and those will be more values here
/// rather than new multipliers invented at the call site.
/// </summary>
public enum MatchImportance
{
    /// <summary>A match that decides nothing but the two points and the three.</summary>
    Normal = 0,

    /// <summary>A match that matters: a rival, a European place, a manager on a run.</summary>
    Relevant = 1,

    /// <summary>A match that decides something: the title, a place in the cup, survival.</summary>
    VeryRelevant = 2,

    /// <summary>A match whose result decides another match: a decider, a final.</summary>
    Decisive = 3
}

/// <summary>Everything about a match that changes how many people turn up, gathered to be passed in one.</summary>
/// <param name="Tier">The tier of the home club's division. 1 is the top.</param>
/// <param name="HomePosition">Where the home club stands in its table, counted from one.</param>
/// <param name="ClubsInDivision">How many clubs are in the division.</param>
/// <param name="HomeSquadStars">The home squad's strength, 0..5, everybody in the squad.</param>
/// <param name="AwaySquadStars">The away squad's strength, 0..5, everybody in the squad.</param>
/// <param name="DivisionAverageStars">The average squad strength of the division, for the opponent factor.</param>
/// <param name="Matchday">Which matchday of the season this is, counted from one.</param>
/// <param name="TotalMatchdays">How many matchdays the season has.</param>
/// <param name="Importance">How much the match matters.</param>
public readonly record struct AttendanceContext(
    int Tier,
    int HomePosition,
    int ClubsInDivision,
    double HomeSquadStars,
    double AwaySquadStars,
    double DivisionAverageStars,
    int Matchday,
    int TotalMatchdays,
    MatchImportance Importance);

/// <summary>
/// How many people are in the stand.
///
/// A crowd is not a random number. It is a ground of a given size, in a given division, for
/// a club sitting in a given place in its table, against an opponent of a given quality, in
/// a given week of a given season, for a match of a given importance, at a given price — and
/// then a small amount of noise, because the people deciding whether to go are people. Each
/// of those is a factor, the factors multiply, and the result can never be more people than
/// the ground holds.
///
/// The last factor is the only random one and it is deliberately narrow. A crowd that
/// swings by half is not a crowd, it is a coin, and a gate that depends on a coin is a gate
/// nobody can manage.
/// </summary>
public static class AttendanceCalculator
{
    /// <summary>
    /// The share of a full ground a first-tier match draws, before anything else is taken into
    /// account. Everything else is a reason to be fuller or emptier than this.
    ///
    /// It is 0.45 and not 0.40 because of what the other factors can add up to. The strongest
    /// possible case — the best club, the best draw, the last day, a decider, and a seat at the
    /// cheapest price the curve knows — multiplies out to about 1.16, so only a ground in a
    /// nearly full stadium and a nearly sold-out price reaches a full house. At 0.40 the very
    /// best case came to 0.93 and no ground in the game could ever be sold out, which would
    /// make the capacity the only ceiling in the formula a line nothing ever reached.
    /// </summary>
    public const double BaseDemand = 0.45;

    /// <summary>How much fuller the ground is for the best-placed club than the worst-placed.</summary>
    public const double PositionSpread = 0.30;

    /// <summary>The bottom of the position spread: a club last in the table draws this much of the top club's crowd.</summary>
    public const double PositionFloor = 0.88;

    /// <summary>How far the opponent factor is allowed to move a crowd, in either direction.</summary>
    public const double OpponentSpread = 0.15;

    /// <summary>
    /// How much of the season has to pass before a crowd is at its biggest. A ground is
    /// emptier in August than it is in May, and the difference is a straight line: interest
    /// starts ten percent below the average and ends thirty percent above it.
    /// </summary>
    public const double SeasonProgressFloor = 0.90;

    public const double SeasonProgressSpread = 0.30;

    /// <summary>The band a random crowd moves in. Narrow on purpose.</summary>
    public const double RandomFloor = 0.90;

    public const double RandomCeiling = 1.10;

    /// <summary>How full the ground is, as a share of its capacity, for a match in the top division.</summary>
    public static double DivisionFactor(int tier) => tier switch
    {
        1 => 1.00,
        2 => 0.75,
        3 => 0.55,
        // A division below the bottom of the pyramid is a smaller league, and the curve keeps
        // going down rather than stopping at a number that was only ever meant for three.
        _ => Math.Max(0.30, 1.00 - (tier - 1) * 0.225)
    };

    /// <summary>What a match of this importance is worth to a crowd.</summary>
    public static double ImportanceFactor(MatchImportance importance) => importance switch
    {
        MatchImportance.Normal => 1.00,
        MatchImportance.Relevant => 1.10,
        MatchImportance.VeryRelevant => 1.20,
        MatchImportance.Decisive => 1.30,
        _ => 1.00
    };

    /// <summary>
    /// How full the ground is for a club in a given place in its table.
    ///
    /// The place is used as a fraction of the division rather than as one of twelve fixed
    /// steps, so a division of eight clubs or of twenty gives the same spread between the
    /// best-placed and the worst-placed club.
    /// </summary>
    public static double PositionFactor(int position, int clubsInDivision)
    {
        if (position < 1) position = 1;
        if (clubsInDivision < 1) clubsInDivision = 1;

        if (clubsInDivision == 1) return PositionFloor + PositionSpread;

        var relative = (position - 1.0) / (clubsInDivision - 1.0);
        var clamped = Math.Clamp(relative, 0.0, 1.0);

        return PositionFloor + PositionSpread * (1.0 - clamped);
    }

    /// <summary>
    /// How much an opponent of a given quality is worth, against the average club of the
    /// division. A very weak opponent keeps people away and a very strong one brings them
    /// in, and the band is capped so neither end can take a crowd to nothing or to a
    /// capacity that was never going to be reached.
    /// </summary>
    public static double OpponentFactor(double opponentSquadStars, double divisionAverageStars)
    {
        if (divisionAverageStars <= 0) return 1.0;

        var ratio = opponentSquadStars / divisionAverageStars;
        return Math.Clamp(ratio, 1.0 - OpponentSpread, 1.0 + OpponentSpread);
    }

    /// <summary>How far the season has run, as a share of the matchdays played so far.</summary>
    public static double SeasonProgress(int matchday, int totalMatchdays)
    {
        if (totalMatchdays < 1) return SeasonProgressFloor;

        var progress = Math.Clamp((double)matchday / totalMatchdays, 0.0, 1.0);
        return SeasonProgressFloor + SeasonProgressSpread * progress;
    }

    /// <summary>
    /// The crowd, as a number of people.
    /// </summary>
    /// <param name="stadium">The ground the match is played in.</param>
    /// <param name="context">Everything about the match that moves a crowd.</param>
    /// <param name="randomFactor">The narrow band of noise, 0.90 to 1.10.</param>
    public static int Calculate(Stadium stadium, AttendanceContext context, double randomFactor)
    {
        ArgumentNullException.ThrowIfNull(stadium);

        if (stadium.Capacity == 0) return 0;

        var demand =
            BaseDemand
            * DivisionFactor(context.Tier)
            * PositionFactor(context.HomePosition, context.ClubsInDivision)
            * OpponentFactor(context.AwaySquadStars, context.DivisionAverageStars)
            * SeasonProgress(context.Matchday, context.TotalMatchdays)
            * ImportanceFactor(context.Importance)
            * TicketPriceRules.DemandFactor(stadium.TicketPrice)
            * Math.Clamp(randomFactor, RandomFloor, RandomCeiling);

        var attendance = (int)Math.Round(stadium.Capacity * demand);

        // A ground holds what it holds. Every factor above pushes towards a fuller ground and
        // the capacity is the only thing that is allowed to say no.
        return Math.Clamp(attendance, 0, stadium.Capacity);
    }
}
