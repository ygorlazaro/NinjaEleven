using Microsoft.AspNetCore.SignalR;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Api.Realtime;

/// <summary>
/// The API's side of the broadcaster seam: what the match loop produced, in the DTOs the
/// clients read, sent to the groups following that match and that matchday.
///
/// <para>
/// The mapping lives here and not in the loop, and that is the whole point of the seam
/// speaking models: the loop that moves a match's clock is the same code in the API and in
/// the Scheduler, and only one of those two processes has a socket to send anything down.
/// A process with no clients registers <see cref="SilentMatchBroadcaster"/> and runs the
/// same loop, so a match it opens is played to the final whistle either way.
/// </para>
/// </summary>
public sealed class SignalRMatchBroadcaster : IMatchBroadcaster
{
    private readonly IHubContext<MatchHub> _hubContext;

    public SignalRMatchBroadcaster(IHubContext<MatchHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>There are clients here, so the loop may read another process's football to send it on.</summary>
    public bool IsSilent => false;

    public Task PublishStateAsync(
        Guid matchId,
        MatchStateView state,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(MatchHub.StateMethod, state.ToDto(), cancellationToken);

    public Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEngineEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(
                MatchHub.EventMethod,
                new MatchStreamDto { MatchId = matchId, Events = events.ToDtos() },
                cancellationToken);
    }

    /// <summary>
    /// The same wire shape as the events the engine has just produced. A manager who opens a
    /// match at minute sixty is sent the state and then the events of a match another process
    /// is playing, and those arrive as themselves rather than as a different kind of message
    /// that happens to mean the same thing.
    /// </summary>
    public Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(
                MatchHub.EventMethod,
                new MatchStreamDto { MatchId = matchId, Events = events.Select(logged => logged.ToDto().ToEngineDto()).ToList() },
                cancellationToken);
    }

    public Task PublishResultAsync(
        Guid matchId,
        MatchResultView result,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.MatchGroupFor(matchId))
            .SendAsync(MatchHub.ResultMethod, result.ToDto(), cancellationToken);

    public Task PublishScoreAsync(
        Guid roundId,
        MatchScoreRow score,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MatchHub.RoundGroupFor(roundId))
            .SendAsync(MatchHub.ScoreMethod, score.ToDto(), cancellationToken);

    public Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEngineEvent> events,
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
            Events = events.ToDtos()
        };

        return _hubContext.Clients.Group(MatchHub.RoundGroupFor(roundId))
            .SendAsync(MatchHub.MatchdayEventMethod, payload, cancellationToken);
    }

    /// <summary>
    /// Another process's football, on the matchday's scoreboard. A client follows one match in
    /// full and the rest of the day here, so the beats a foreign match produced reach it as
    /// well as the goals.
    /// </summary>
    public Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
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
            Events = events.Select(logged => logged.ToDto().ToEngineDto()).ToList()
        };

        return _hubContext.Clients.Group(MatchHub.RoundGroupFor(roundId))
            .SendAsync(MatchHub.MatchdayEventMethod, payload, cancellationToken);
    }
}