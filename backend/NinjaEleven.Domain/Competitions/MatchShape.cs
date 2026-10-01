namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// How a match reads, told apart from the score alone.
///
/// <para>
/// A score is two numbers and a newspaper lead is not. The same 3 x 1 is a comeback, a
/// collapse, a rout or a comfortable afternoon depending on the order the goals went in and
/// on which side was behind when they did, and a manager reading "venceu por 3 a 1" has been
/// told none of the four things he wants to know. This is where the difference is worked out,
/// in the domain, from the sequence of goals and the final score — because the sequence is a
/// fact about the match and not a matter of taste.
/// </para>
///
/// <para>
/// <b>Comeback and collapse are the same question asked twice.</b> A club that was behind and
/// finished ahead came back; a club that was ahead and finished behind collapsed; a club that
/// was behind twice — once by two and once by one — and finished level rescued a point rather
/// than a win, and saying "recuperou" about it would be dressing a draw up as a turnaround.
/// </para>
/// </summary>
public enum MatchShape
{
    /// <summary>An ordinary result: the side that was ahead finished ahead, or neither ever was.</summary>
    Ordinary,

    /// <summary>The club was behind and won. A turnaround.</summary>
    Comeback,

    /// <summary>The club was behind twice or more by the end and won anyway.</summary>
    GreatComeback,

    /// <summary>The club was behind and drew. A point rescued rather than a result won.</summary>
    Rescued,

    /// <summary>The club was ahead and lost. A game given away.</summary>
    Collapse,

    /// <summary>The club was ahead by two or more and lost. A game thrown away.</summary>
    Rout,

    /// <summary>
    /// The club was two or more ahead and drew. A lead thrown away rather than a result won.
    /// </summary>
    ///
    /// <para>
    /// It is a shape of its own and not a <see cref="GoalFest"/> because the difference is
    /// the two goals that were already in the bag when they came out. Reading a 2 x 2 that
    /// was 2 x 0 as "noite de gols" tells a manager his team had a good evening, and the
    /// evening is the one thing about it he most needs to be told straight: two goals thrown
    /// away is a different week from a 2 x 2 that was always going to be a 2 x 2.
    /// </para>
    Squandered,

    /// <summary>A whitewash, by three goals or more.</summary>
    Whitewash,

    /// <summary>A 0 x 0. The one result every manager reads the same way.</summary>
    Goalless,

    /// <summary>A draw with goals in it, level at the end.</summary>
    BalancedDraw,

    /// <summary>A draw with a lot of goals in it.</summary>
GoalFest
}

/// <summary>
/// What one goal was, in the words a report uses for it.
///
/// <para>
/// A penalty is not a goal that happened to be easy: it is a foul, a taker and a decision,
/// and a manager reading a list that said only "Fulano, 28" would not know that the goal
/// came from eleven metres. An own goal is a defender's mistake and is never the scorer's
/// credit, which is the one distinction the rest of the game is careful about.
/// </para>
/// </summary>
public enum GoalKind
{
    Goal,
    Penalty,
    Rebound,
    OwnGoal
}

/// <summary>
/// One goal of a match, in the order it happened.
///
/// <para>
/// It is a fact about a match rather than a sentence about it: the minute and the man are
/// read off the event, and the report decides what to say about them. That split is the same
/// one the narration follows — a match is narrated once, when it happens, and everything that
/// reads it afterwards reads the same words.
/// </para>
/// </summary>
public readonly record struct MatchGoal(
    Guid TeamId,
    Guid PlayerId,
    string PlayerName,
    int Minute,
    GoalKind Kind);

/// <summary>
/// How a finished match reads, worked out from its goals.
///
/// <para>
/// It is a static class of pure arithmetic over two lists, and it is in the domain rather than
/// in the report because a comeback is a fact about a match. The desk decides the words; the
/// rule decides what happened.
/// </para>
/// </summary>
public static class MatchShapeRules
{
    /// <summary>A margin of this many goals or more is a whitewash.</summary>
    public const int WhitewashMargin = 3;

    /// <summary>A lead of this many goals is a game that was lost, not one that was drawn.</summary>
    public const int CollapseMargin = 2;

    /// <summary>A draw with at least this many goals in it is worth calling.</summary>
    public const int GoalFestGoals = 4;


    /// <summary>
    /// What the match was, from the club's point of view.
    ///
    /// <para>
    /// The club's own goals are what decide a comeback: a manager does not care that the
    /// opposition came from two down either, and a report that told him his team "recuperou"
    /// because the other side recovered would be a sentence about the wrong club. The
    /// whitewash and the goalless draw are checked first because they are true whatever the
    /// sequence was — a 4 x 0 is a whitewash whether the fourth goal came in the fifth minute
    /// or the eightieth.
    /// </para>
    /// </summary>
    public static MatchShape ShapeOf(
        Guid clubId,
        int clubGoals,
        int opponentGoals,
        IReadOnlyList<MatchGoal> goals)
    {
        var margin = clubGoals - opponentGoals;
        var absolute = Math.Abs(margin);

        if (clubGoals == 0 && opponentGoals == 0)
        {
            return MatchShape.Goalless;
        }

        if (absolute >= WhitewashMargin)
        {
            return MatchShape.Whitewash;
        }

        // The two things a comeback and a collapse are made of, read off the running score of
        // the club's own goals and the ones against him.
        var wasAhead = false;
        var wasBehind = 0;
        var running = 0;

        foreach (var goal in goals)
        {
            running += goal.TeamId == clubId ? 1 : -1;

            if (running > 0)
            {
                wasAhead = true;
            }

            if (running < 0)
            {
                wasBehind = Math.Max(wasBehind, -running);
            }
        }

        if (margin > 0)
        {
            return wasBehind == 0
                ? MatchShape.Ordinary
                : wasBehind >= CollapseMargin
                    ? MatchShape.GreatComeback
                    : MatchShape.Comeback;
        }

        if (margin < 0)
        {
            if (wasAhead == false)
            {
                return MatchShape.Ordinary;
            }

            return HowFarAheadBy(goals, clubId) >= CollapseMargin
                ? MatchShape.Rout
                : MatchShape.Collapse;
        }

        if (wasBehind > 0)
        {
            return MatchShape.Rescued;
        }

        // A draw is asked about the lead before it is asked about the goals in it: a club two
        // up that ends level has not had a goal-fest, it has had a collapse that stopped
        // short, and the number of goals is the least interesting thing about it.
        if (HowFarAheadBy(goals, clubId) >= CollapseMargin)
        {
            return MatchShape.Squandered;
        }

        return clubGoals + opponentGoals >= GoalFestGoals
            ? MatchShape.GoalFest
            : MatchShape.BalancedDraw;
    }

    /// <summary>
    /// How far ahead the club ever was, in goals, before the match finished.
    ///
    /// It is a second pass over the same list rather than a number carried out of the first:
    /// the first pass only had to know *whether* the club was ever in front, and a report
    /// asking "by how much" needs the biggest of them, not the last.
    /// </summary>
    private static int HowFarAheadBy(IReadOnlyList<MatchGoal> goals, Guid clubId)
    {
        var running = 0;
        var best = 0;

        foreach (var goal in goals)
        {
            running += goal.TeamId == clubId ? 1 : -1;
            best = Math.Max(best, running);
        }

        return best;
    }

    /// <summary>Whether a shape is one a manager wants in the subject line rather than in the body.</summary>
    public static bool IsHeadline(MatchShape shape) =>
        shape is MatchShape.Ordinary ? false : true;

    /// <summary>Whether a shape is a turnaround — the club got the result it was not winning.</summary>
    public static bool IsATurnaround(MatchShape shape) =>
        shape is MatchShape.Comeback or MatchShape.GreatComeback or MatchShape.Rescued;
}
