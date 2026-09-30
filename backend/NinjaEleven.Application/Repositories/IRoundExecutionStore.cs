using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// What happened when a process asked for a window of football.
/// </summary>
public enum RoundClaim
{
    /// <summary>The window is now yours to play.</summary>
    Claimed = 0,

    /// <summary>
    /// Another process took it moments ago and is playing it. Nothing to do: the world is
    /// moving, just not by this process.
    /// </summary>
    HeldByAnotherProcess = 1,

    /// <summary>Every fixture of the window is already played. There is nothing left to do.</summary>
    AlreadyPlayed = 2,

    /// <summary>The window is not in the calendar any more.</summary>
    NotFound = 3
}

/// <summary>
/// Takes a window of football so that exactly one process plays it.
///
/// <para>
/// It exists apart from <see cref="IRoundRepository"/> because this is the one write in the
/// game that has to be atomic against another <i>process</i> rather than against another
/// request. Reading a window, deciding it is free and writing the claim are three steps, and
/// two schedulers that fire in the same minute take all three before either of them writes.
/// The test and the write are therefore one statement, taken under the database's own row
/// lock — which is why there is no Redis, no distributed lock table and no "probably only one
/// scheduler" in the design.
/// </para>
///
/// <para>
/// The claim carries a lease rather than being held until released, because the process that
/// holds it can die while holding it. A window whose claim has gone stale is claimable again,
/// and the fixtures already finished are recognised as finished rather than played twice.
/// </para>
/// </summary>
public interface IRoundExecutionStore
{
    /// <summary>
    /// Takes the window if it is free, and says why it was not if it is not. The window is
    /// left as it was found in every refusal, so a second process cannot disturb a window
    /// that is being played.
    /// </summary>
    /// <param name="roundId">The window to take.</param>
    /// <param name="lease">How long the claim is honoured before it may be taken over.</param>
    /// <param name="now">The instant the claim is taken at.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<RoundClaim> TryClaimAsync(
        Guid roundId,
        TimeSpan lease,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the window as played out, but only while this process still holds it. A window
    /// whose claim was taken over while it was being played is not completed by the process
    /// that lost it, so the two never both write the same closing state.
    /// </summary>
    Task<bool> TryCompleteAsync(
        Guid roundId,
        TimeSpan lease,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a window back without completing it. Used when the window could not be finished,
    /// so that the next run may take it rather than waiting out the lease.
    /// </summary>
    Task ReleaseAsync(Guid roundId, CancellationToken cancellationToken = default);
}
