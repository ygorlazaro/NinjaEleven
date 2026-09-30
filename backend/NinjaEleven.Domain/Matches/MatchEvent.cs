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

    /// <summary>
    /// What the player on this event is called, filled in when the events are read rather
    /// than stored, because a name is not a fact about the event — it is a fact about the
    /// man, and it is read from him.
    ///
    /// <para>
    /// A scorer has to be nameable by the scoreline without the scoreline going looking for
    /// him: a finished match is answered with the squad as it is <i>now</i>, and a striker
    /// who has since left the club is not in it, so a scoreline that read names off the team
    /// sheet printed an empty name beside the goal he scored.
    /// </para>
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PlayerName { get; set; }

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
            icon = engineEvent.Icon,
            // Written into the payload rather than left on the engine event, because the
            // engine's copy dies with the session and this row is what a manager's screen
            // reads after a reload. A scoreline that marked penalties while he was watching
            // and lost them on refresh would be a scoreline that was wrong twice.
            fromPenalty = engineEvent.FromPenalty
        });
    }
}
