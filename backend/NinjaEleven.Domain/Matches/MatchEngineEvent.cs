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
        string icon)
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
    }
}