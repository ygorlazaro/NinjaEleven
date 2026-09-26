namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Persisted match event. Narration is generated from these records, so events are
/// the source of truth and never free-text strings. <see cref="Payload"/> carries the
/// event specific detail as JSON and defaults to an empty object.
/// </summary>
public class MatchEvent
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public int Sequence { get; private set; }
    public int Minute { get; private set; }
    public MatchEventType Type { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? PlayerId { get; private set; }
    public Guid? SecondaryPlayerId { get; private set; }
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }
    public string Payload { get; private set; } = "{}";

    private MatchEvent() { }

    public static MatchEvent Create(
        Guid matchId,
        int sequence,
        int minute,
        MatchEventType type,
        Guid? teamId = null,
        Guid? playerId = null,
        Guid? secondaryPlayerId = null,
        int homeScore = 0,
        int awayScore = 0,
        string? payload = null)
    {
        return new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            Sequence = sequence,
            Minute = minute,
            Type = type,
            TeamId = teamId,
            PlayerId = playerId,
            SecondaryPlayerId = secondaryPlayerId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload
        };
    }

    public static MatchEvent FromEngineEvent(MatchEngineEvent engineEvent, Guid matchId)
    {
        return Create(
            matchId,
            engineEvent.Sequence,
            engineEvent.Minute,
            engineEvent.Type,
            engineEvent.TeamId,
            engineEvent.PlayerId,
            secondaryPlayerId: null,
            engineEvent.HomeScore,
            engineEvent.AwayScore,
            BuildPayload(engineEvent));
    }

    private static string BuildPayload(MatchEngineEvent engineEvent)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            description = engineEvent.Description,
            icon = engineEvent.Icon
        });
    }
}
