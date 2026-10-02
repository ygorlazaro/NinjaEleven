namespace NinjaEleven.Domain.Matches;

/// <summary>
/// How a man is described on a card, which is a reading of a number and not a judgement of
/// its own.
/// </summary>
/// <remarks>
/// The bands live in <see cref="MatchRating"/> and are named here rather than left as a
/// number a screen compares against thresholds of its own, because a client that decides
/// where "good" starts is a client that will decide it differently from the next one.
/// </remarks>
public enum MatchRatingBand
{
    /// <summary>He was not on the pitch long enough to have earned one.</summary>
    Unrated = 0,

    /// <summary>
    /// Below the ordinary mark and not on it, so red is reserved for men the number is
    /// actually against rather than for men who did nothing worth writing down.
    /// </summary>
    Red,

    /// <summary>An ordinary evening, which is what most of a squad's cards read.</summary>
    Yellow,

    /// <summary>A good one.</summary>
    Green,

    /// <summary>The ceiling itself. There is one way to wear it.</summary>
    Diamond
}
