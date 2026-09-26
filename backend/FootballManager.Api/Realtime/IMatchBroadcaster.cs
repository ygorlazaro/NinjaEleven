using FootballManager.Api.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace FootballManager.Api.Realtime;

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
        _hubContext.Clients.Group(MatchHub.GroupFor(matchId))
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

        return _hubContext.Clients.Group(MatchHub.GroupFor(matchId))
            .SendAsync(MatchHub.EventMethod, events, cancellationToken);
    }

    public Task PublishResultAsync(Guid matchId, MatchResultDto result, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.GroupFor(matchId))
            .SendAsync(MatchHub.ResultMethod, result, cancellationToken);
}
