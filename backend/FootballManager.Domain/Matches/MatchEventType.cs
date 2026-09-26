namespace FootballManager.Domain.Matches;

/// <summary>
/// Types of events the match engine can emit. Strings are never used to
/// represent events — the engine always produces a typed event.
/// </summary>
public enum MatchEventType
{
    KickOff,
    Shot,
    Save,
    GoalScored,
    OwnGoalScored,
    Corner,
    Foul,
    YellowCardShown,
    RedCardShown,
    PlayerInjured,
    SubstitutionMade,
    PenaltyAwarded,
    PenaltyTaken,
    PenaltySaved,
    HalfTimeReached,
    SecondHalfStarted,
    MatchFinished
}