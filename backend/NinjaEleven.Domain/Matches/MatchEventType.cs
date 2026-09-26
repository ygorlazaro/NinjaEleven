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
    MatchFinished
}