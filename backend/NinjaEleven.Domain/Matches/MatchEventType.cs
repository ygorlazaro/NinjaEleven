namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Types of events the match engine can emit. Strings are never used to
/// represent events — the engine always produces a typed event.
/// </summary>
public enum MatchEventType
{
    /// <summary>
    /// The eleven a club is putting on the pitch, said before the whistle rather than
    /// after it. A manager picks the men, and a match that starts without saying who is
    /// playing hides the one decision he made.
    /// </summary>
    LineupAnnounced,

    KickOff,

    /// <summary>
    /// The ball being played: a dribble held, a pass completed, a duel won. Most of a
    /// match is this, and a feed that only ever says what happened to the ball cannot say
    /// what happened in the game.
    /// </summary>
    BuildUp,

    Shot,
    Save,
    GoalScored,
    OwnGoalScored,
    Corner,
    Foul,
    YellowCardShown,
    RedCardShown,
    PlayerInjured,

    /// <summary>
    /// The referee announcing the added time, at the end of the half it belongs to.
    /// </summary>
    StoppageTimeAdded,

    /// <summary>
    /// An outfield player had to take the gloves because his club lost its goalkeeper.
    /// </summary>
    KeeperPromoted,

    SubstitutionMade,
    PenaltyAwarded,
    PenaltyTaken,
    PenaltySaved,
    HalfTimeReached,
    SecondHalfStarted,

    /// <summary>
    /// The ninety minutes are over and the tie is not decided, so the match is going to the
    /// spot. It is said before the first kick rather than after the last one, so a manager
    /// arriving at a tied match finds out why the clock has stopped.
    /// </summary>
    FullTimeReached,

    /// <summary>
    /// A penalty shootout has begun. The screen draws the two rows of five from here.
    /// </summary>
    PenaltyShootoutStarted,

    /// <summary>
    /// One kick of a shootout, taken by the player the event carries. Whether it went in is
    /// in the narration, because the feed says "GOL" or "perdeu" the way it says everything
    /// else, and a screen that has to read a word to know the result is a screen that has to
    /// be told the words.
    /// </summary>
    PenaltyShootoutKick,

    MatchFinished
}