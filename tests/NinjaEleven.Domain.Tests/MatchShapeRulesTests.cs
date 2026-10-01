using NinjaEleven.Domain.Competitions;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A score is two numbers and a sports page is not, so the shape of a result is worked out
/// from the order the goals went in. These hold that a comeback is a comeback whichever way
/// round the two clubs are, that a club is only ever told about <i>its own</i> sequence, and
/// that a whitewash and a goalless draw are true whatever the order was.
/// </summary>
public class MatchShapeRulesTests
{
    private static readonly Guid Club = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static List<MatchGoal> For(Guid team, params int[] minutes) =>
        minutes.Select(minute => new MatchGoal(
            team, Guid.NewGuid(), "Fulano", minute, GoalKind.Goal)).ToList();

    private static List<MatchGoal> ForOther(params int[] minutes) => For(Other, minutes);

    /// <summary>
    /// Two sides' goals interleaved by the minute, which is the only order a match's goals
    /// are ever in. Concatenating one side and then the other is not a football match: it
    /// hands the rule a list in which every goal of one team came first, and the shape that
    /// comes out of it describes a game nobody played.
    /// </summary>
    private static List<MatchGoal> Together(
        (int Minute, bool Club)[] events) =>
        events
            .OrderBy(entry => entry.Minute)
            .Select(entry => new MatchGoal(
                entry.Club ? Club : Other,
                Guid.NewGuid(),
                entry.Club ? "Casa" : "Fora",
                entry.Minute,
                GoalKind.Goal))
            .ToList();

    [Fact]
    public void AWinHavingBeenLevelThroughoutIsAnOrdinaryOne()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 2, 1, [.. For(Club, 10), .. ForOther(20)]);

        Assert.Equal(MatchShape.Ordinary, shape);
    }

    [Fact]
    public void WinningAfterHavingBeenBehindIsAcomeback()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 2, 1, [.. ForOther(15), .. For(Club, 30, 70)]);

        Assert.Equal(MatchShape.Comeback, shape);
    }

    /// <summary>
    /// Two goals down is a different thing to have overcome from one, and a report that called
    /// both "virada" would have spent a word that ought to mean something.
    /// </summary>
    [Fact]
    public void WinningFromTwoDownIsAGreatComeback()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 3, 2, [.. ForOther(5, 25), .. For(Club, 40, 60, 85)]);

        Assert.Equal(MatchShape.GreatComeback, shape);
    }

    [Fact]
    public void LevellingFromBehindIsARescueAndNotATurnaround()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 1, 1, [.. ForOther(15, 50), .. For(Club, 80)]);

        Assert.Equal(MatchShape.Rescued, shape);
        Assert.True(MatchShapeRules.IsATurnaround(shape));
    }

    [Fact]
    public void LeadingAndThenLosingIsACollapse()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 1, 2, [.. For(Club, 20), .. ForOther(55, 80)]);

        Assert.Equal(MatchShape.Collapse, shape);
    }

    [Fact]
    public void LeadingByTwoAndLosingIsARout()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 1, 3, [.. For(Club, 10, 20), .. ForOther(60, 75, 90)]);

        Assert.Equal(MatchShape.Rout, shape);
    }

    /// <summary>
    /// The one that matters most: a manager does not care that the opposition came from two
    /// down either. Told from the other bench, that same match reads as an ordinary win for
    /// the home club and an ordinary defeat for the visitors, and never as either club's
    /// collapse — the shape is asked for <b>from a club's point of view</b> and the same
    /// sequence must answer differently for each side.
    /// </summary>
    [Fact]
    public void TheShapeIsTheClubsOwnAndNotTheMatchs()
    {
        var goals = Together(
        [
            (10, false), (20, false), (35, true), (45, true)
        ]);

        // From the home bench the club led 2-0 and lost it: a squandered lead. From the away
        // bench it was 2-0 down and rescued a point. The same four goals, two entirely
        // different evenings, and neither report is allowed to be about the other club.
        var home = MatchShapeRules.ShapeOf(Other, 2, 2, goals);
        var away = MatchShapeRules.ShapeOf(Club, 2, 2, goals);

        Assert.Equal(MatchShape.Squandered, home);
        Assert.Equal(MatchShape.Rescued, away);
    }

    [Fact]
    public void LosingWithoutEverHavingLedIsAnOrdinaryDefeat()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 0, 2, [.. ForOther(15, 60)]);

        Assert.Equal(MatchShape.Ordinary, shape);
    }

    /// <summary>
    /// A four-nil is a whitewash whether the fourth goal came in the fifth minute or the
    /// eightieth, so the shape is settled before the sequence is read at all.
    /// </summary>
    [Fact]
    public void AWhitewashIsAWhitewashWhicheverWayTheGoalsWentIn()
    {
        var early = MatchShapeRules.ShapeOf(Club, 4, 0, For(Club, 2, 5, 8, 11));
        var late = MatchShapeRules.ShapeOf(Club, 4, 0, For(Club, 55, 70, 80, 90));

        Assert.Equal(MatchShape.Whitewash, early);
        Assert.Equal(MatchShape.Whitewash, late);
    }

    [Fact]
    public void AGoallessDrawIsAGoallessDrawAndNotABalancedOne()
    {
        var shape = MatchShapeRules.ShapeOf(Club, 0, 0, []);

        Assert.Equal(MatchShape.Goalless, shape);
    }

    [Fact]
    public void ADrawWithGoalsInItIsBalanced()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 1, 1, [.. For(Club, 30), .. ForOther(60)]);

        Assert.Equal(MatchShape.BalancedDraw, shape);
    }

    /// <summary>
    /// Four goals in a draw is an evening worth a different word from two, and a manager
    /// reading "empate por 2 x 2" and "empate por 2 x 2" for two different matches learns
    /// nothing about either.
    /// </summary>
    [Fact]
    public void ADrawWithFourGoalsInItIsAGoalFest()
    {
        // Level or one goal ahead throughout, in the order the goals actually happened:
        // nobody ever had two in the bag, so this is the open game it looks like rather than
        // a lead that was thrown away.
        var shape = MatchShapeRules.ShapeOf(Club, 2, 2, Together(
        [
            (10, true), (20, false), (30, true), (40, false)
        ]));

        Assert.Equal(MatchShape.GoalFest, shape);
    }

    /// <summary>
    /// The goals are read in the order they happened, so a caller that handed them over
    /// unsorted would be handed a different match. This is the arithmetic of the running
    /// score, and it only holds if the order is the order.
    /// </summary>
    [Fact]
    public void TheOrderTheGoalsArrivedInIsWhatDecidesTheShape()
    {
        var behindFirst = new List<MatchGoal>
        {
            new(Other, Guid.NewGuid(), "Casa", 5, GoalKind.Goal),
            new(Club, Guid.NewGuid(), "Fora", 30, GoalKind.Goal),
            new(Club, Guid.NewGuid(), "Fora", 60, GoalKind.Goal)
        };

        var aheadFirst = new List<MatchGoal>
        {
            new(Club, Guid.NewGuid(), "Fora", 5, GoalKind.Goal),
            new(Club, Guid.NewGuid(), "Fora", 30, GoalKind.Goal),
            new(Other, Guid.NewGuid(), "Casa", 60, GoalKind.Goal)
        };

        // Both are 2 x 1 wins by the club, and they are not the same evening. A goal conceded
        // at five minutes and a goal conceded at sixty are the same scoreline and opposite
        // football, which is the whole reason the shape is read off the sequence.
        Assert.Equal(MatchShape.Comeback, MatchShapeRules.ShapeOf(Club, 2, 1, behindFirst));
        Assert.Equal(MatchShape.Ordinary, MatchShapeRules.ShapeOf(Club, 2, 1, aheadFirst));
    }

    /// <summary>
    /// Two up and level at the end is a collapse that stopped short, and calling it a goal
    /// fest would tell the manager his team had a good evening.
    /// </summary>
    [Fact]
    public void LevellingAfterLeadingByTwoIsASquanderedLeadAndNotAGoalFest()
    {
        var shape = MatchShapeRules.ShapeOf(Club, 2, 2, Together(
        [
            (10, true), (20, true), (55, false), (88, false)
        ]));

        Assert.Equal(MatchShape.Squandered, shape);
    }

    /// <summary>One goal up is not a lead worth the word, so a 1 x 1 stays a balanced draw.</summary>
    [Fact]
    public void LevellingAfterLeadingByOneIsStillJustADraw()
    {
        var shape = MatchShapeRules.ShapeOf(
            Club, 1, 1, [.. For(Club, 10), .. ForOther(60)]);

        Assert.Equal(MatchShape.BalancedDraw, shape);
    }

    [Fact]
    public void AnOrdinaryResultIsTheOnlyOneThatDoesNotLead()
    {
        Assert.False(MatchShapeRules.IsHeadline(MatchShape.Ordinary));
        Assert.True(MatchShapeRules.IsHeadline(MatchShape.Comeback));
        Assert.True(MatchShapeRules.IsHeadline(MatchShape.Goalless));
    }
}
