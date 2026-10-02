using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// The broadcaster of a process nobody is connected to.
///
/// <para>
/// It is not a stub: it is what a process with no sockets behind it says, and it answers
/// <see cref="IsSilent"/> with true so the loop skips the pass that reads another process's
/// football out of the database only to have nowhere to put it. The Scheduler is that
/// process — it moves the world at five o'clock with no client attached — and it still has
/// to run the loop, because a match it opens is a match somebody is watching from another
/// process, and a match nobody drives is a match that never finishes.
/// </para>
/// </summary>
public sealed class SilentMatchBroadcaster : IMatchBroadcaster
{
    public bool IsSilent => true;

    public Task PublishStateAsync(
        Guid matchId,
        MatchStateView state,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEngineEvent> events,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishResultAsync(
        Guid matchId,
        MatchResultView result,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishScoreAsync(
        Guid roundId,
        MatchScoreRow score,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEngineEvent> events,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}