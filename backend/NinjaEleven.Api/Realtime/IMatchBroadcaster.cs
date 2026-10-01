using NinjaEleven.Api.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace NinjaEleven.Api.Realtime;

/// <summary>
/// Publishes match DTOs to the clients following a match. The Application layer never
/// knows this exists: the match loop and the hub live in the API and both talk to the
/// clients through this seam, so the domain and the services stay transport free.
/// </summary>
public interface IMatchBroadcaster
{
    Task PublishStateAsync(Guid matchId, MatchStateDto state, CancellationToken cancellationToken = default);

    Task PublishEventsAsync(Guid matchId, IReadOnlyList<MatchEngineEventDto> events, CancellationToken cancellationToken = default);

    Task PublishResultAsync(Guid matchId, MatchResultDto result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the score of one match to everyone following the round. This is what
    /// lets a client watch its own match in full and the rest of the matchday on a
    /// scoreboard, all from the same connection.
    /// </summary>
    Task PublishScoreAsync(Guid roundId, MatchScoreDto score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the beats of one match to everyone following the round. The score says
    /// how many goals a match has; this says who scored them and what else happened, which
    /// is the only part of a match somebody who is not managing it can actually follow.
    /// </summary>
    Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEngineEventDto> events,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class SignalRMatchBroadcaster : IMatchBroadcaster
{
    private readonly IHubContext<MatchHub> _hubContext;

    public SignalRMatchBroadcaster(IHubContext<MatchHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishStateAsync(Guid matchId, MatchStateDto state, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(MatchHub.StateMethod, state, cancellationToken);

    public Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEngineEventDto> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(
                MatchHub.EventMethod,
                new MatchStreamDto { MatchId = matchId, Events = events },
                cancellationToken);
    }

    public Task PublishResultAsync(Guid matchId, MatchResultDto result, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(MatchHub.ResultMethod, result, cancellationToken);

    public Task PublishScoreAsync(Guid roundId, MatchScoreDto score, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.RoundGroupFor(roundId))
            .SendAsync(MatchHub.ScoreMethod, score, cancellationToken);

    public Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEngineEventDto> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Task.CompletedTask;
        }

        var payload = new MatchdayEventDto
        {
            RoundId = roundId,
            MatchId = matchId,
            Events = events
        };

        return _hubContext.Clients.Group(MatchHub.RoundGroupFor(roundId))
            .SendAsync(MatchHub.MatchdayEventMethod, payload, cancellationToken);
    }
}
