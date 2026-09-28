using NinjaEleven.Domain.Enums;

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

    /// <summary>
    /// Windows of football per matchday, in the order they are played: the Supercup, the
    /// championship and the cup. A matchday that has no Supercup and no cup has one window in
    /// it, which is why this is the most a day can hold rather than what a day always holds.
    /// </summary>
    public const int WindowsPerMatchDay = 3;

    /// <summary>
    /// The window the championship is played in.
    ///
    /// The first window on a day where the Supercup is not, and the second on the day the
    /// Supercup is, which is why it is a window number and not an ordinal: a matchday's
    /// windows are numbered in the order they are played, and the Supercup takes window zero
    /// so that a championship window is window one whether or not anything played before it.
    /// </summary>
    public const int ChampionshipWindow = 1;

    public const int CupWindow = 2;

    /// <summary>
    /// The order a matchday's windows are played in, and the reason a matchday is not a set
    /// of games but a sequence of them.
    ///
    /// **The Supercup opens the day, the championship follows, and the cup closes it.** Every
    /// division plays its championship round in the same wave and at the same time, so a
    /// manager never sees one division a matchday ahead of another: a division that has played
    /// two more games than its neighbour is a pyramid where the tables are not comparable, and
    /// the whole point of a table is that it is comparable. The cup is last because it is a
    /// knockout among clubs that have all just played, and a cup leg played before the
    /// championship of the same day would be a leg taken by a side that had not yet run its
    /// legs that week.
    /// </summary>
    public static IReadOnlyList<CompetitionType> MatchdayWaves { get; } =
    [
        CompetitionType.SuperCup,
        CompetitionType.League,
        CompetitionType.Cup
    ];

    /// <summary>
    /// Which wave of the matchday a competition is played in: zero is first.
    /// </summary>
    public static int WaveOf(CompetitionType type) =>
        type switch
        {
            CompetitionType.SuperCup => 0,
            CompetitionType.League => 1,
            _ => 2
        };

    /// <summary>The window a competition is played in, on a matchday that has it.</summary>
    public static int WindowOf(CompetitionType type) => type switch
    {
        CompetitionType.SuperCup => SuperCupWindow,
        CompetitionType.League => ChampionshipWindow,
        _ => CupWindow
    };

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
    /// Which round of the bracket a cup window belongs to, told from the window's own number.
    ///
    /// The other half of <see cref="CupWindowNumber"/>, and it lives beside it for the reason
    /// the forward one does: a screen that has to say "quartas de final" is asking the same
    /// question the drawer answered, and two copies of that arithmetic are two numberings that
    /// can disagree about where the quarter-finals are.
    /// </summary>
    /// <param name="windowNumber">The cup window, counted from one across the whole cup.</param>
    public static int CupTieRoundOf(int windowNumber)
    {
        if (windowNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowNumber), windowNumber, "A cup window is counted from one.");
        }

        return ((windowNumber - 1) / CupTieLegs) + 1;
    }

    /// <summary>
    /// Which leg of the tie a cup window holds: one for the first, two for the second.
    /// </summary>
    /// <param name="windowNumber">The cup window, counted from one across the whole cup.</param>
    public static int CupLegOf(int windowNumber)
    {
        if (windowNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowNumber), windowNumber, "A cup window is counted from one.");
        }

        return ((windowNumber - 1) % CupTieLegs) + 1;
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
    /// The matchday the Supercup is played on, and the window it takes on it.
    ///
    /// It is the first matchday of a season and window zero of it, so the Supercup is the
    /// first football of the new season: the two clubs that won the two competitions of the
    /// one before play each other before anybody has played a league game. The cup's first
    /// tie-round is not until matchday five, so nothing else wants that day, and putting the
    /// Supercup there keeps the season exactly twenty-two matchdays long with the final last.
    /// A Supercup is one match between the two clubs that won the other two, and it belongs to
    /// the start of a season rather than to the end of the one before.
    /// </summary>
    public const int SuperCupMatchDay = 1;

    public const int SuperCupWindow = 0;

    /// <summary>
    /// Which band of a division's table a position falls into.
    ///
    /// The bands are the pyramid's, not a fixed four-and-four, because the three divisions do
    /// not have the same neighbours:
    ///
    /// - **The top division** promotes nobody — there is no division above it — so its first
    ///   place is the <see cref="TableZone.Champion"/> and its second to fourth are simply
    ///   safe. Four clubs are still sent down from its bottom.
    /// - **A middle division** sends its top four up and its bottom four down, and the middle is
    ///   the only place in the pyramid where a table is a race in both directions.
    /// - **The last division** has nothing below it, so its bottom four are not relegated into a
    ///   division that does not exist. They are the four clubs the cup leaves out: the bracket is
    ///   thirty-two of the pyramid's thirty-six, ranked by tier and then by position, and
    ///   <see cref="CupQualification"/> draws that line. The band says so, because a club that
    ///   finishes last in the country should be told what happened to it rather than be left to
    ///   work out that nothing is painted on its row.
    ///
    /// The zones come from the same numbers <see cref="DivisionMovement"/> moves clubs with, so
    /// the band on a row and the move at the end of the season cannot disagree.
    ///
    /// Cups and the Supercup carry no tier, so they get <see cref="TableZone.None"/>: there is no
    /// promotion to speak of in a knockout, and a table with no bands should not pretend it has
    /// any.
    /// </summary>
    public static TableZone ZoneFor(int? tier, int position, int count)
    {
        if (tier is null || count <= 0)
            return TableZone.None;

        // Tiers are counted from one and one is the top, so the first place of the first
        // division is the only position in the pyramid that is a title rather than a race.
        if (position == 1 && tier == Tiers()[0])
            return TableZone.Champion;

        if (tier > 1 && position <= PromotionSlots && position <= count)
            return TableZone.Promotion;

        if (position > count - RelegationSlots)
        {
            return tier < Tiers().Count ? TableZone.Relegation : TableZone.CupExclusion;
        }

        return TableZone.Safe;
    }

    /// <summary>
    /// What a tie-round is called, which is what a consolation prize is said as.
    /// </summary>
    /// <remarks>
    /// It is said in the game's own words rather than in numbers because a line in a club's
    /// book is read by a manager: "eliminado nas quartas de final" is something a person can
    /// picture and "eliminado na fase 3" is not. The numbering stays the rules' — the fifth
    /// round is the final, and the sixth does not exist.
    /// </remarks>
    public static string TieRoundName(int tieRound) => tieRound switch
    {
        1 => "16 avos de final",
        2 => "oitavas de final",
        3 => "quartas de final",
        4 => "semi-final",
        _ => "final"
    };

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
