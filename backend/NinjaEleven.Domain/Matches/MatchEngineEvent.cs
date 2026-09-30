namespace NinjaEleven.Domain.Matches;

/// <summary>
/// A single event produced by the match engine. Events are typed — the engine
/// never emits raw strings. The `Sequence` field lets clients detect lost events
/// after a SignalR reconnect.
/// </summary>
public class MatchEngineEvent
{
    public int Sequence { get; }
    public int Minute { get; }
    public MatchEventType Type { get; }
    public Guid? TeamId { get; }
    public Guid? PlayerId { get; }
    public int HomeScore { get; }
    public int AwayScore { get; }
    public string Description { get; } = string.Empty;
    public string Icon { get; } = string.Empty;

    /// <summary>
    /// Whether this goal came from the spot. The goal is the event a scoreline counts, so
    /// the scoreline is where the question of how it was scored has to be answered: a
    /// penalty and an open-play goal are the same goal until somebody says which, and the
    /// only way a screen can mark one without reading the wording is for the engine to
    /// have said so when it emitted the goal.
    /// </summary>
    public bool FromPenalty { get; }

    /// <summary>
    /// What the player on this event is called, when the engine had him to hand.
    ///
    /// The scorerline names a scorer from the goal itself rather than looking him up in the
    /// squad: the club he played for is not necessarily the club he is in now, and a goal
    /// whose author cannot be found is a goal with a blank beside it.
    /// </summary>
    public string? PlayerName { get; }

    public bool IsGoal =>
        Type == MatchEventType.GoalScored || Type == MatchEventType.OwnGoalScored;

    private MatchEngineEvent() { }

    public MatchEngineEvent(
        int sequence,
        int minute,
        MatchEventType type,
        Guid? teamId,
        Guid? playerId,
        int homeScore,
        int awayScore,
        string description,
        string icon,
        bool fromPenalty = false,
        string? playerName = null)
    {
        Sequence = sequence;
        Minute = minute;
        Type = type;
        TeamId = teamId;
        PlayerId = playerId;
        HomeScore = homeScore;
        AwayScore = awayScore;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        FromPenalty = fromPenalty;
        PlayerName = playerName;
    }
}