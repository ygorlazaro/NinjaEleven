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
/// <param name="HomeSupporters">
/// How many people follow the home club. This is the number the crowd is worked out from, and
/// it has no default on purpose: a club with no following is not a club whose crowd happens to
/// be zero this week, and a context that could omit it would let a caller measure a ground
/// without ever saying who is coming to it.
/// </param>
/// <param name="HomeSquadStars">The home squad's strength, 0..5, everybody in the squad.</param>
/// <param name="AwaySquadStars">The away squad's strength, 0..5, everybody in the squad.</param>
/// <param name="DivisionAverageStars">The average squad strength of the division, for the opponent factor.</param>
/// <param name="Matchday">Which matchday of the season this is, counted from one.</param>
/// <param name="TotalMatchdays">How many matchdays the season has.</param>
/// <param name="Importance">How much the match matters.</param>
/// <param name="WorksUnderway">
/// Whether the ground is in the middle of a construction project. It is a fact about the
/// ground rather than about the football, which is why it belongs here and not in any of the
/// factors below: it does not change how much a club wants to fill the stand, only how much of
/// the stand is open to be filled.
/// </param>
public readonly record struct AttendanceContext(
    int Tier,
    int HomePosition,
    int ClubsInDivision,
    int HomeSupporters,
    double HomeSquadStars,
    double AwaySquadStars,
    double DivisionAverageStars,
    int Matchday,
    int TotalMatchdays,
    MatchImportance Importance,
    bool WorksUnderway = false);

/// <summary>
/// How many people are in the stand.
///
/// <para>
/// <b>A crowd is a fact about the club and the ground is only a ceiling.</b> It used to be the
/// other way round — a share of the seats, which meant a club could never draw more people than
/// it had seats, a ground of five thousand could never be described as too small, and every
/// argument for building one was an argument nobody could make. Now the demand is the club's
/// own following, worked out of who they are playing, where they stand, what it means and what
/// a seat costs; the ground only says how many of them can be let in. The rest is what a club
/// is being told about its own ground, and it is the whole point of the model.
/// </para>
///
/// <para>
/// What is left out is the division. It used to be a factor here, and it is not any more,
/// because a division's clubs do not all draw the same crowd and the ladder in
/// <see cref="FanBaseRules"/> already says which division draws more. A tier factor multiplied
/// on top of a crowd that already carries the tier was counting it twice, and it was counting
/// it twice in the one direction that makes a small ground look busy.
/// </para>
///
/// <para>
/// The last factor is the only random one and it is deliberately narrow. A crowd that swings by
/// half is not a crowd, it is a coin, and a gate that depends on a coin is a gate nobody can
/// manage.
/// </para>
/// </summary>
public static class AttendanceCalculator
{
    /// <summary>
    /// The share of a club's following that comes to one match.
    ///
    /// <para>
    /// It replaces a constant that used to mean the opposite thing — the share of a full ground
    /// a first-tier match drew — and the two numbers are not comparable. A following is
    /// everybody who would come: the season-ticket holder, the family, the boy who comes with
    /// his father, the club that has not won anything in nine years and would still turn up on
    /// a Saturday. Turning up to one match is a fraction of that, and the fraction is what this
    /// is.
    /// </para>
    ///
    /// <para>
    /// It is a small number because a club of forty-five thousand people is not a club whose
    /// stadium holds forty-five thousand. That gap is the argument for building: a first
    /// division club at this share sells out a five-thousand seat ground several times a
    /// season, and the part of its following it could not let in is what the ten-thousand stand
    /// is for.
    /// </para>
    /// </summary>
    public const double Followthrough = 0.13;

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

    /// <summary>
    /// What a ground being worked on is worth to a crowd.
    ///
    /// <para>
    /// It multiplies the demand rather than the capacity, and the two are not the same thing.
    /// A ground of five thousand with a quarter of it shut is still a ground of five thousand:
    /// the club cannot sell the seats it does not have, and a demand figure that knew about the
    /// closure would be a demand for a smaller stadium than the one the club owns. So the
    /// closure takes a quarter of the crowd off and leaves the capacity where it is — which is
    /// also why a full house during a rebuild is 3.750 people and not 6.250.
    /// </para>
    /// </summary>
    public static double WorksFactor(bool worksUnderway) =>
        worksUnderway ? StadiumRules.WorksAttendanceFactor : 1.0;

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
    /// How much of a club's following wants to come, before the ground is asked whether it has
    /// anywhere to put them.
    ///
    /// <para>
    /// This is the number a club's finances actually turn on when the ground is big enough,
    /// and it is worth separating from the attendance for that reason alone: a ground of five
    /// thousand hides every crowd above five thousand, so a club whose following outgrew its
    /// ground would see a flat gate and a growing income, and nothing on the club's page would
    /// say which of the two had happened.
    /// </para>
    /// </summary>
    /// <param name="context">Everything about the match that moves a crowd.</param>
    /// <param name="ticketPrice">What a seat costs at this ground.</param>
    /// <param name="randomFactor">The narrow band of noise, 0.90 to 1.10.</param>
    public static int Demand(AttendanceContext context, decimal ticketPrice, double randomFactor)
    {
        if (context.HomeSupporters <= 0) return 0;

        var interest =
            Followthrough
            * PositionFactor(context.HomePosition, context.ClubsInDivision)
            * OpponentFactor(context.AwaySquadStars, context.DivisionAverageStars)
            * SeasonProgress(context.Matchday, context.TotalMatchdays)
            * ImportanceFactor(context.Importance)
            * TicketPriceRules.DemandFactor(ticketPrice)
            * WorksFactor(context.WorksUnderway)
            * Math.Clamp(randomFactor, RandomFloor, RandomCeiling);

        return Math.Max(0, (int)Math.Round(context.HomeSupporters * interest));
    }

    /// <summary>
    /// How many people turn up, which is how many of them the ground had room for.
    ///
    /// <para>
    /// The ground is the last word and it is the only thing that says no. A demand above the
    /// capacity is not a crowd that shrank to fit — it is the state a club's ground is in, and
    /// it is the only state in which an expansion is worth arguing for.
    /// </para>
    /// </summary>
    /// <param name="stadium">The ground the match is played in.</param>
    /// <param name="context">Everything about the match that moves a crowd.</param>
    /// <param name="randomFactor">The narrow band of noise, 0.90 to 1.10.</param>
    public static int Calculate(Stadium stadium, AttendanceContext context, double randomFactor)
    {
        ArgumentNullException.ThrowIfNull(stadium);

        var demand = Demand(context, stadium.TicketPrice, randomFactor);

        // A ground holds what it holds, and a ground of no seats at all holds nobody however
        // many people would come to the match.
        return Math.Min(demand, stadium.Capacity);
    }
}
