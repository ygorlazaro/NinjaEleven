using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Tells whoever is following a match what just happened in it.
///
/// <para>
/// The seam speaks the Application layer's own models and never a transport's DTOs, so the
/// loop that moves a match's clock does not know whether it is talking to a socket or to
/// nobody. The mapping to DTOs belongs to the implementation on the other side: the API's
/// sends them over SignalR, and <see cref="SilentMatchBroadcaster"/> throws them away.
/// </para>
///
/// <para>
/// Every process that can start a match runs the loop that publishes it. That is the whole of
/// why this seam exists in this layer: the world is moved by more than one process, and a
/// match opened by one of them is watched on another — so "who told the client" cannot be a
/// question with an answer that only one process knows.
/// </para>
/// </summary>
public interface IMatchBroadcaster
{
    /// <summary>
    /// True when there is no transport behind this seam: a process that moves the world with
    /// nobody attached to it. The loop uses it to skip the pass that republishes another
    /// process's football, which is work only a process holding connections can use.
    /// </summary>
    bool IsSilent { get; }

    Task PublishStateAsync(Guid matchId, MatchStateView state, CancellationToken cancellationToken = default);

    Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEngineEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same beats, read back out of the log rather than taken from the engine. A match
    /// another process is playing is republished from its own event rows, so a client cannot
    /// tell which process produced the football it is watching.
    /// </summary>
    Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken = default);

    Task PublishResultAsync(Guid matchId, MatchResultView result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the score of one match to everyone following the round. This is what
    /// lets a client watch its own match in full and the rest of the matchday on a
    /// scoreboard, all from the same connection.
    /// </summary>
    Task PublishScoreAsync(Guid roundId, MatchScoreRow score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the beats of one match to everyone following the round. The score says
    /// how many goals a match has; this says who scored them and what else happened, which
    /// is the only part of a match somebody who is not managing it can actually follow.
    /// </summary>
    Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEngineEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>The matchday's scoreboard, fed by another process's log rather than by an engine.</summary>
    Task PublishMatchdayEventsAsync(
        Guid roundId,
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken = default);
}