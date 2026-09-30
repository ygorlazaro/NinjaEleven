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
    public const int DivisionCount = 4;

    /// <summary>Clubs in every division.</summary>
    public const int ClubsPerDivision = 16;

    /// <summary>Clubs in the whole pyramid. Derived, because the two above are the rule.</summary>
    public const int TotalClubs = DivisionCount * ClubsPerDivision;

    /// <summary>
    /// Clubs sent down from a division. All divisions send their bottom four down (except tier 4).
    /// </summary>
    public const int RelegationSlots = 4;

    /// <summary>Clubs sent up from a division. All divisions send their top four up (except tier 1).</summary>
    public const int PromotionSlots = 4;

    /// <summary>Positions that are given a trophy in a division.</summary>
    public const int TrophyPositions = 3;

    /// <summary>Clubs in the cup. All 64 clubs participate.</summary>
    public const int CupSize = 64;

    /// <summary>
    /// Tie-rounds the cup is drawn over: the 32-avos, the 16-avos, the oitavas, the quartas, the
    /// semi-finals and the final. Sixty-four clubs halve six times, so the last of them is a
    /// final between two clubs and the round before it is a semi-final between four.
    /// </summary>
    public const int CupRounds = 6;

    /// <summary>
    /// Windows of football per matchday, in the order they are played: the championship and the
    /// cup. A matchday with no cup in it has one window in it, which is why this is the most a
    /// day can hold rather than what a day always holds.
    ///
    /// <para>
    /// It is two and not three because the two legs of a cup tie are a day apart: the first leg
    /// goes out on the cup's day and the second on the next one, so no day ever holds a cup tie
    /// twice. A day with a cup leg in it holds its division's round in the afternoon and that
    /// leg in the evening, and the ten days in between hold nothing but their round.
    /// </para>
    /// </summary>
    public const int WindowsPerMatchDay = 2;

    /// <summary>
    /// The window the championship is played in.
    ///
    /// It is the first window of the day, so a matchday's windows are numbered in the order
    /// they are played and the cup is window two after it.
    /// </summary>
    public const int ChampionshipWindow = 1;

    public const int CupWindow = 2;

    /// <summary>
    /// The order a matchday's windows are played in, and the reason a matchday is not a set
    /// of games but a sequence of them.
    ///
    /// **The championship opens the day and the cup closes it.** Every division plays its
    /// championship round in the same wave and at the same time, so a manager never sees one
    /// division a matchday ahead of another: a division that has played two more games than its
    /// neighbour is a pyramid where the tables are not comparable, and the whole point of a
    /// table is that it is comparable. The cup is last because it is a knockout among clubs that
    /// have all just played, and a cup leg played before the championship of the same day would
    /// be a leg taken by a side that has not yet run its legs that week.
    ///
    /// The Supercup is in the list because it is a window a matchday can have, and on the one
    /// day it is played it is the only football in it — day one, before anybody has played a
    /// league game.
    /// </summary>
    public static IReadOnlyList<CompetitionType> MatchdayWaves { get; } =
    [
        CompetitionType.SuperCup,
        CompetitionType.League,
        CompetitionType.Cup
    ];

    /// <summary>
    /// Which wave of the matchday a competition is played in: the first window is wave one.
    ///
    /// The Supercup takes the same wave as the championship rather than a wave of its own,
    /// because it is played on a day that has nothing else in it and the day's first window is
    /// therefore the same window whichever of the two is playing. A wave below one would be a
    /// window numbered before the first window, and a window is counted from one.
    /// </summary>
    public static int WaveOf(CompetitionType type) =>
        type switch
        {
            CompetitionType.Cup => 2,
            _ => 1
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
    /// round of 64 is windows 1 and 2, the 16-avos 3 and 4, and so on to the final in 11 and 12.
    /// Keeping the arithmetic here rather than in the drawer means a round drawn when a tie is
    /// decided lands in the same window the calendar would have given it, instead of in a second
    /// numbering that only the round-drawing code can read.
    /// </summary>
    /// <param name="tieRound">Which of the six tie-rounds this is, counted from one.</param>
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
    /// Round-robin legs every pair of clubs meets. A division of sixteen plays every other
    /// fifteen twice, which is what makes the division worth thirty rounds.
    /// </summary>
    public static int LeagueRoundsPerLeg => ClubsPerDivision - 1;

    /// <summary>Rounds of the championship, and therefore the spine of the calendar.</summary>
    public static int LeagueMatchDays => LeagueRoundsPerLeg * CupTieLegs;

    /// <summary>
    /// The day of the season the championship's first round is played on.
    ///
    /// <para>
    /// Day one belongs to the Supercup, and the championship opens on the morning after it: the
    /// two clubs that won the two competitions of the season before play each other at fifteen
    /// hundred on day one, and a division's first round is a day later. A calendar that put the
    /// first round on day one would have a club that had won the Supercup playing a league game
    /// nine hours later, which is a fixture nobody would be given.
    /// </para>
    /// </summary>
    public const int FirstChampionshipMatchDay = 2;

    /// <summary>
    /// The day of the season a round of the championship is played on.
    ///
    /// <para>
    /// One round a day, every day, from <see cref="FirstChampionshipMatchDay"/> to the day the
    /// thirtieth round falls on. A division of sixteen plays each of its fifteen opponents once
    /// over the first fifteen days and once with the ground the other way round over the next
    /// fifteen, and there is no day in the middle of that with nothing in it: the round that
    /// cannot be put off is the one that is played.
    /// </para>
    /// </summary>
    /// <param name="round">Which round of the division's own calendar, counted from one.</param>
    public static int ChampionshipMatchDayOf(int round)
    {
        if (round < 1 || round > LeagueMatchDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(round), round, $"The championship is {LeagueMatchDays} rounds long.");
        }

        return round + FirstChampionshipMatchDay - 1;
    }

    /// <summary>
    /// The days the cup's tie-rounds open on: the first leg of each of the six rounds.
    ///
    /// <para>
    /// They are fixed rather than worked out, because a calendar that spreads them evenly is not
    /// the calendar this game is played on: the 32-avos are on the seventh day, the 16-avos on the
    /// twelfth, the oitavas on the seventeenth, the quartas on the twenty-second, the
    /// semi-finals on the twenty-seventh and the final on the thirty-second — a cup round every
    /// five days, so a club is never asked to play a tie it has not had a week to prepare for and
    /// never waits nine days for the next one.
    /// </para>
    ///
    /// <para>
    /// Five of the six land on a day the championship is also playing — the seventh day carries
    /// the sixth round of the divisions and the 32-avos, for instance — and that is the point of
    /// the day having windows: a club plays its league game at fifteen hundred and its cup leg at
    /// twenty-one hundred, and the table it reads afterwards is the same table everybody else's is.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> CupMatchDays() =>
    [
        7, 12, 17, 22, 27, 32
    ];

    /// <summary>
    /// The two days a tie-round is played over: the first leg, then the second.
    ///
    /// <para>
    /// A tie is a day long: the first leg on the round's day and the return the next morning,
    /// both at nine in the evening, so a club that played a league game in the afternoon of the
    /// first leg has played the return the following day rather than a week later. That is what
    /// puts the final's second leg on the thirty-third day of the season and leaves the
    /// thirty-fourth free.
    /// </para>
    /// </summary>
    /// <param name="tieRound">Which of the six tie-rounds this is, counted from one.</param>
    /// <returns>The first-leg day and the second-leg day.</returns>
    public static (int FirstLeg, int SecondLeg) CupLegMatchDays(int tieRound)
    {
        if (tieRound < 1 || tieRound > CupRounds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tieRound), tieRound, $"The cup has {CupRounds} tie-rounds, counted from one.");
        }

        var first = CupMatchDays()[tieRound - 1];
        return (first, first + DaysBetweenCupLegs);
    }

    /// <summary>
    /// Days between the first leg of a cup tie and its return. One: the tie is over in two
    /// evenings, and the day after the first leg is a league day like any other.
    /// </summary>
    public const int DaysBetweenCupLegs = 1;

    /// <summary>
    /// The day of the season with no football in it, and the last day of a season.
    ///
    /// <para>
    /// The final's second leg is on the thirty-third day and the thirty-fourth is empty: a day
    /// for the market to do its work, for the squads to be turned over and for a manager to look
    /// at a season that is over. The next season's day one is the day after it, and it is the
    /// Supercup.
    /// </para>
    /// </summary>
    public static int SeasonRestDay => CupLegMatchDays(CupRounds).SecondLeg + 1;

    /// <summary>
    /// How many days a season has, counted from its first day to its rest day. It is the
    /// championship's spine plus the days the championship does not reach: day one for the
    /// Supercup, the last two for the final's two legs and the rest day after them.
    /// </summary>
    public static int SeasonMatchDays => SeasonRestDay;

    /// <summary>
    /// Days between matchdays. One: a round is played every day of the season, and a tie's two
    /// legs are two of those days rather than two of a week.
    /// </summary>
    public const int DaysBetweenMatchDays = 1;

    /// <summary>
    /// When the championship window of a matchday kicks off.
    ///
    /// A matchday is a date, and a date is not a moment: something has to say that the
    /// championship goes out in the afternoon and the cup in the evening, or "the day is due"
    /// is a question the world cannot answer. The hours are here rather than in
    /// Scheduler's cron for the reason every other number in this file is here — the calendar
    /// is a rule of the game, and a rule of the game that lives in the process that watches the
    /// clock is a rule that changes when that process is restarted.
    /// </summary>
    public static readonly TimeOnly ChampionshipKickOff = new(15, 0);

    /// <summary>When the cup window of a matchday kicks off, six hours after the championship.</summary>
    public static readonly TimeOnly CupKickOff = new(21, 0);

    /// <summary>
    /// When the Supercup kicks off. It is the first football of a season and the only window of
    /// the day it is played in, and it goes out at the championship's hour because that is the
    /// hour a club's Saturday of football starts at.
    /// </summary>
    public static readonly TimeOnly SuperCupKickOff = ChampionshipKickOff;

    /// <summary>
    /// The hour a competition's window of a matchday goes out, and the whole of what
    /// "which window is due" is made of.
    /// </summary>
    public static TimeOnly KickOffTimeOf(CompetitionType type) => type switch
    {
        CompetitionType.SuperCup => SuperCupKickOff,
        CompetitionType.Cup => CupKickOff,
        _ => ChampionshipKickOff
    };

    /// <summary>
    /// The matchday the Supercup is played on, and the window it takes on it.
    ///
    /// It is the first matchday of a season and the first window of it, so the Supercup is the
    /// first football of the new season: the two clubs that won the two competitions of the one
    /// before play each other before anybody has played a league game. It is alone on that day —
    /// the championship opens on <see cref="FirstChampionshipMatchDay"/> — so the window number
    /// it shares with the championship costs nothing and keeps a window counted from one
    /// counted from one. There is no Supercup in the first season of a world, because there is
    /// no season before it to have been won.
    /// </summary>
    public const int SuperCupMatchDay = 1;

    public const int SuperCupWindow = ChampionshipWindow;

    /// <summary>
    /// Which band of a division's table a position falls into.
    ///
    /// The bands are the pyramid's, not a fixed four-and-four, because the four divisions do
    /// not have the same neighbours:
    ///
    /// - **The top division** promotes nobody — there is no division above it — so its first
    ///   place is the <see cref="TableZone.Champion"/> and its second to fourth are simply
    ///   safe. Four clubs are still sent down from its bottom.
    /// - **A middle division** sends its top four up and its bottom four down, and the middle is
    ///   the only place in the pyramid where a table is a race in both directions.
    /// - **The last division** still sends its top four up, and has nothing below it, so it
    ///   relegates nobody and its bottom four are simply safe. There is no fifth division to
    ///   send them to, and a table that painted a band on them would be promising a fate the
    ///   pyramid does not have.
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

        if (tier < Tiers().Count && position > count - RelegationSlots)
            return TableZone.Relegation;

        return TableZone.Safe;
    }

    /// <summary>
    /// What a tie-round is called, which is what a consolation prize is said as.
    /// </summary>
    /// <remarks>
    /// It is said in the game's own words rather than in numbers because a line in a club's
    /// book is read by a manager: "eliminado nas quartas de final" is something a person can
    /// picture and "eliminado na fase 3" is not. The numbering stays the rules' — the sixth
    /// round is the final, and the seventh does not exist.
    /// </remarks>
    public static string TieRoundName(int tieRound) => tieRound switch
    {
        1 => "32 avos de final",
        2 => "16 avos de final",
        3 => "oitavas de final",
        4 => "quartas de final",
        5 => "semi-final",
        _ => "final"
    };

    /// <summary>Names of the divisions, top first.</summary>
    public static string DivisionName(int tier) => tier switch
    {
        1 => "1ª Divisão",
        2 => "2ª Divisão",
        3 => "3ª Divisão",
        4 => "4ª Divisão",
        _ => $"{tier}ª Divisão"
    };

    /// <summary>Tiers, top first.</summary>
    public static IReadOnlyList<int> Tiers() =>
        Enumerable.Range(1, DivisionCount).ToList();
}
