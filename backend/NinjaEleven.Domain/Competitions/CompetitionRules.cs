namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// The shape of the world, in one place.
///
/// Every one of these numbers is a rule of the game rather than a setting of a screen, and
/// they are gathered here for the same reason the match engine's numbers live in
/// <c>MatchRules</c>: a constant buried in the middle of a service is a constant nobody can
/// find when the pyramid has to change. The first division is tier 1 and the last is the
/// highest tier number, so adding a division at the bottom is a new row and not a new rule.
/// </summary>
public static class CompetitionRules
{
    /// <summary>How many divisions the pyramid has. Tier 1 is the top.</summary>
    public const int DivisionCount = 3;

    /// <summary>Clubs in every division.</summary>
    public const int ClubsPerDivision = 12;

    /// <summary>Clubs in the whole pyramid. Derived, because the two above are the rule.</summary>
    public const int TotalClubs = DivisionCount * ClubsPerDivision;

    /// <summary>
    /// Clubs sent down from a division. Tier 1 sends its bottom four down; the lower
    /// divisions send the same four down, which is what keeps the pyramid the same size.
    /// </summary>
    public const int RelegationSlots = 4;

    /// <summary>Clubs sent up from tier 2 into tier 1.</summary>
    public const int PromotionSlots = 4;

    /// <summary>Positions that are given a trophy in a division.</summary>
    public const int TrophyPositions = 3;

    /// <summary>Clubs in the cup.</summary>
    public const int CupSize = 32;

    /// <summary>
    /// How many matchdays the cup's tie-rounds are spread over before the last one. It is
    /// the gaps between them, not the count: the fifth round is the final and the final is
    /// the last match of the season, so the last gap is the one that has to land on the
    /// final matchday.
    /// </summary>
    public const int CupRounds = 5;

    /// <summary>Windows of football per matchday, in the order they are played.</summary>
    public const int WindowsPerMatchDay = 2;

    /// <summary>
    /// The first window of a matchday is the championship and the second is the cup. It is a
    /// number rather than a competition type because a Supercup matchday has only one window
    /// and it is the first one, and because a future competition would take the third.
    /// </summary>
    public const int ChampionshipWindow = 1;

    public const int CupWindow = 2;

    /// <summary>Legs in a cup tie, and the reason a cup tie needs two matchdays.</summary>
    public const int CupTieLegs = 2;

    /// <summary>
    /// The window a cup tie's leg is played in, counted from one across the whole cup.
    ///
    /// It is a flat count rather than a matchday number because a cup window is a window: the
    /// round of 16 is windows 1 and 2, the quarter-finals 3 and 4, and so on to the final in 9
    /// and 10. Keeping the arithmetic here rather than in the drawer means a round drawn when
    /// a tie is decided lands in the same window the calendar would have given it, instead of
    /// in a second numbering that only the round-drawing code can read.
    /// </summary>
    /// <param name="tieRound">Which of the five tie-rounds this is, counted from one.</param>
    /// <param name="leg">Which leg: one for the first, two for the second.</param>
    public static int CupWindowNumber(int tieRound, int leg)
    {
        if (tieRound < 1 || tieRound > CupRounds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tieRound), tieRound, $"The cup has {CupRounds} tie-rounds, counted from one.");
        }

        if (leg < 1 || leg > CupTieLegs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leg), leg, $"A tie is played over {CupTieLegs} legs.");
        }

        return ((tieRound - 1) * CupTieLegs) + leg;
    }

    /// <summary>
    /// Round-robin legs every pair of clubs meets. A division of twelve plays every other
    /// eleven twice, which is what makes the division worth twenty-two matchdays.
    /// </summary>
    public static int LeagueRoundsPerLeg => ClubsPerDivision - 1;

    /// <summary>Matchdays of the championship, and therefore the spine of the calendar.</summary>
    public static int LeagueMatchDays => LeagueRoundsPerLeg * CupTieLegs;

    /// <summary>
    /// The first matchday of each cup tie-round, except the last: the final is forced onto
    /// the last matchday of the season, and the four rounds before it are spread over the
    /// matchdays in between so a club plays a cup tie roughly every four days rather than
    /// four rounds in a row.
    /// </summary>
    public static IReadOnlyList<int> CupMatchDays()
    {
        // The first four rounds share the matchdays before the last one, and the last round
        // is the final. Four rounds over `LeagueMatchDays - 1` matchdays means one round
        // every `step` matchdays, rounded so the days stay strictly increasing.
        var available = Math.Max(CupRounds - 1, 1);
        var days = new List<int>(CupRounds);

        for (var round = 1; round < CupRounds; round++)
        {
            var day = (int)Math.Round((double)round * (LeagueMatchDays - 1) / available);
            days.Add(Math.Clamp(day, 1, LeagueMatchDays - 1));
        }

        days.Add(LeagueMatchDays);
        return days;
    }

    /// <summary>
    /// The two matchdays a tie-round is played over: the first leg, then the second.
    ///
    /// <see cref="CupMatchDays"/> gives the matchday a tie-round is *finished* on, which is
    /// why the final's is the last matchday of the season and why the earlier ones are held
    /// back from it. The first leg is the matchday before. A tie is a week long, which is what
    /// a tie is: two games with a week in between, not two games in one window.
    /// </summary>
    /// <param name="tieRound">Which of the five tie-rounds this is, counted from one.</param>
    /// <returns>The first-leg matchday and the second-leg matchday.</returns>
    public static (int FirstLeg, int SecondLeg) CupLegMatchDays(int tieRound)
    {
        if (tieRound < 1 || tieRound > CupRounds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tieRound), tieRound, $"The cup has {CupRounds} tie-rounds, counted from one.");
        }

        var second = CupMatchDays()[tieRound - 1];
        return (Math.Max(second - 1, 1), second);
    }

    /// <summary>
    /// Days between matchdays. A matchday is a weekend, and a tie's two legs are a week apart
    /// because they are played on two of them.
    /// </summary>
    public const int DaysBetweenMatchDays = 7;

    /// <summary>
    /// The matchday the Supercup is played on.
    ///
    /// It is the first, and it is in the second window. The cup's first tie-round is not until
    /// matchday five, so nothing else wants that window on the first matchday, and putting the
    /// Supercup there keeps the season exactly twenty-two matchdays long with the final last.
    /// A Supercup is one match between the two clubs that won the other two, and it belongs to
    /// the start of a season rather than to the end of the one before.
    /// </summary>
    public const int SuperCupMatchDay = 1;

    public const int SuperCupWindow = CupWindow;

    /// <summary>
    /// Which band of a divisions table a position falls into: the top four is the promotion
    /// race, the bottom four the relegation battle, and the rest are safe. The split is read off
    /// the position alone, because the pyramid keeps every division the same size, so the same
    /// four-and-four rule holds for all of them — a manager ought to be able to read the bands
    /// from one place whether his club is going up, fighting to stay up, or already safe.
    ///
    /// Cups and the Supercup carry no tier, so they get <see cref="TableZone.None"/>: there is
    /// no promotion to speak of in a knockout, and a table with no bands should not pretend it has
    /// any.
    /// </summary>
    public static TableZone ZoneFor(int? tier, int position, int count)
    {
        if (tier is null || count <= 0)
            return TableZone.None;

        if (position <= PromotionSlots && position <= count)
            return TableZone.Promotion;

        if (position > count - RelegationSlots)
            return TableZone.Relegation;

        return TableZone.Safe;
    }

    /// <summary>Names of the divisions, top first.</summary>
    public static string DivisionName(int tier) => tier switch
    {
        1 => "1ª Divisão",
        2 => "2ª Divisão",
        3 => "3ª Divisão",
        _ => $"{tier}ª Divisão"
    };

    /// <summary>Tiers, top first.</summary>
    public static IReadOnlyList<int> Tiers() =>
        Enumerable.Range(1, DivisionCount).ToList();
}
