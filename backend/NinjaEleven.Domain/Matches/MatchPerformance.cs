namespace NinjaEleven.Domain.Matches;

/// <summary>
/// What one man did in one match, counted as the engine resolves it.
///
/// <para>
/// It is a tally rather than a rating, and the difference is the whole point: the engine
/// cannot know what a match was worth to a player until the match is over, but it can know
/// exactly what happened to him. So the engine writes this down as it goes and
/// <see cref="MatchRating"/> reads it at the whistle, which is why a rating is a reading of a
/// tally rather than a running opinion the engine keeps revising.
/// </para>
///
/// <para>
/// It counts only what the match model does not already keep elsewhere. Goals, saves and
/// cards are already on the snapshot, and counting them here as well would be a second
/// answer to the same question sitting next to the first one.
/// </para>
///
/// <para>
/// It is written from the places in the engine where the two men involved in a thing are
/// both in scope, which is every place that matters. Nothing here needs a second participant
/// on the event: the man who lost a duel is recorded by the duel itself, not by a later
/// reading of who was mentioned in the narration.
/// </para>
/// </summary>
public class MatchPerformance
{
    /// <summary>Shots that found the target or would have, on a keeper or past one.</summary>
    public int ShotsOnTarget { get; private set; }

    /// <summary>Shots that did not. They are not worthless, so they cost rather than count.</summary>
    public int ShotsOffTarget { get; private set; }

    public int DuelsWon { get; private set; }
    public int DuelsLost { get; private set; }
    public int FoulsCommitted { get; private set; }
    public int CornersWon { get; private set; }
    public int Assists { get; private set; }

    /// <summary>
    /// Every moment the match put him in it: a duel either way, a shot, a foul, a corner,
    /// a pass that found a runner, a ball carried. This is what "involved in the match" means
    /// to a rating, and it is the only number the inactivity rule reads.
    /// </summary>
    public int Involvements { get; private set; }

    /// <summary>
    /// How far the duels he was in fell from what the engine had already said they would be,
    /// added up. Positive is above his own profile, negative below it.
    /// </summary>
    /// <remarks>
    /// Kept as three separate sums rather than one, and kept unweighted, for two reasons. A
    /// duel and a goal are different sizes of moment, so averaging them would let a striker
    /// with two shots outrank a midfielder with nine duels on the strength of two. And the
    /// weight each kind of moment carries is a question about the rating scale rather than a
    /// fact about the match, so it belongs in <see cref="MatchRules"/> and is applied where
    /// the reading happens — which is also the only place that can cap the total.
    /// </remarks>
    public double DuelDeviation { get; private set; }

    /// <summary>As <see cref="DuelDeviation"/>, for shots.</summary>
    public double ShotDeviation { get; private set; }

    /// <summary>As <see cref="DuelDeviation"/>, for penalties.</summary>
    public double PenaltyDeviation { get; private set; }

    public void RecordShot(bool onTarget, double chance)
    {
        if (onTarget)
        {
            ShotsOnTarget++;
        }
        else
        {
            ShotsOffTarget++;
        }

        Involvements++;
        ShotDeviation += Math.Clamp((onTarget ? 1.0 : 0.0) - chance, -1.0, 1.0);
    }

    public void RecordDuel(bool won, double chance)
    {
        if (won)
        {
            DuelsWon++;
        }
        else
        {
            DuelsLost++;
        }

        Involvements++;
        DuelDeviation += Math.Clamp((won ? 1.0 : 0.0) - chance, -1.0, 1.0);
    }

    /// <summary>
    /// A penalty, which is a moment like a shot and is graded the same way. The taker is the
    /// one being measured, not the keeper he beat: a keeper's work is read by his saves.
    /// </summary>
    public void RecordPenalty(bool scored, double chance)
    {
        Involvements++;
        PenaltyDeviation += Math.Clamp((scored ? 1.0 : 0.0) - chance, -1.0, 1.0);
    }

    /// <summary>
    /// The ball was in the game and he was in it, without anything being decided: a pass that
    /// found a runner, a ball carried into a corner. It counts towards being involved and
    /// towards nothing else, because carrying a ball well is not a thing this model measures.
    /// </summary>
    public void RecordInvolvement() => Involvements++;

    public void RecordFoul() => Involvements++;

    public void RecordAssist() => Assists++;

    public void RecordCorner()
    {
        Involvements++;
        CornersWon++;
    }
}
