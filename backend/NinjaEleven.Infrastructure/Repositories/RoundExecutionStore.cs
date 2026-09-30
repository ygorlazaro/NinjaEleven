using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

/// <summary>
/// The claim on a window of football, taken in the database.
///
/// <para>
/// The claim is a row lock and a state change in one short transaction, and that is the
/// whole answer to two schedulers firing in the same minute. There is no Redis, no lock
/// table and no "there is only one of these" comment: the second process blocks on the row
/// for as long as the first one's transaction lasts — a few milliseconds — and then reads a
/// window that is already <see cref="RoundExecutionStatus.Running"/> and is refused by the
/// same rule the first one satisfied.
/// </para>
///
/// <para>
/// It shares the context with the request rather than owning one, because the claim has to
/// be committed before a single fixture is touched and a window is then played one fixture
/// at a time. The transaction here is the only one in this class, and it is deliberately
/// short: the football happens after it has been committed, never inside it.
/// </para>
/// </summary>
public sealed class RoundExecutionStore : IRoundExecutionStore
{
    private readonly NinjaElevenDbContext _dbContext;
    private readonly IMatchHost _host;

    public RoundExecutionStore(NinjaElevenDbContext dbContext, IMatchHost host)
    {
        _dbContext = dbContext;
        _host = host;
    }

    public async Task<RoundClaim> TryClaimAsync(
        Guid roundId,
        TimeSpan lease,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var round = await LockAsync(roundId, cancellationToken);

            if (round is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return RoundClaim.NotFound;
            }

            // A window that says it has been played is only believed once the fixtures agree.
            // The two columns are a record of the window having been closed, and a window can
            // be closed over a hole — a fixture whose match was given up on is put back on the
            // schedule while the window above it is being written up. Believing the columns
            // there would lose that matchday for the rest of the season, and a matchday is
            // never lost: the hole is found, the window goes back on the schedule, and the
            // fixtures that are left are played.
            if (round.HasBeenExecuted)
            {
                if (!await HasFixturesLeftToPlayAsync(roundId, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RoundClaim.AlreadyPlayed;
                }

                round.ReopenExecution();
            }

            var claim = Decide(round, lease, now);

            if (claim is not RoundClaim.Claimed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return claim;
            }

            // The window's own state machine decides; this only writes what it decided. A
            // window that is released on a failure is one the next run may take without
            // waiting out the lease, and a window that is completed is one nobody walks
            // into again.
            round!.TryBeginExecution(now, lease, _host.HostId);
            _dbContext.Rounds.Update(round);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return RoundClaim.Claimed;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> TryCompleteAsync(
        Guid roundId,
        TimeSpan lease,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var round = await LockAsync(roundId, cancellationToken);

            // The completion is written by the process that still holds the claim and by
            // nobody else: a window whose lease ran out while it was being played belongs to
            // whoever took it, and two processes both writing "completed" is the kind of
            // agreement that only happens while both of them are wrong.
            if (round is null || !round.CanBeCompletedBy(lease, now, _host.HostId))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            // And only over a window that is actually played out. The caller decides this from
            // its own results, and its results are a report of what happened rather than a
            // reading of the window — a match that reached full time in one process can have
            // its fixture reopened by another before the report is written. Asking the
            // fixtures here is what makes a hole impossible to close over: the write that
            // loses a matchday cannot happen in the first place.
            if (await HasFixturesLeftToPlayAsync(roundId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            round.CompleteExecution();
            _dbContext.Rounds.Update(round);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task ReleaseAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        var round = await _dbContext.Rounds
            .FirstOrDefaultAsync(item => item.Id == roundId, cancellationToken);

        // A window somebody else has taken over is not this process's to give back. Releasing
        // it would clear the new owner's claim and hand the window to a third.
        if (round is null || !round.IsHeldBy(_host.HostId))
        {
            return;
        }

        round.ReleaseExecution();
        _dbContext.Rounds.Update(round);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the window under the database's own row lock, so the state that is decided on
    /// is the state that is written. Everything this class does wrong is done wrong twice;
    /// this is the one place that cannot be.
    /// </summary>
    private Task<Round?> LockAsync(Guid roundId, CancellationToken cancellationToken) =>
        _dbContext.Rounds
            .FromSqlInterpolated($"SELECT * FROM rounds WHERE id = {roundId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Whether any fixture of this window is still on the schedule.
    ///
    /// <para>
    /// This is the question the two columns on the window cannot answer, and it is asked of the
    /// fixtures because they are the ones that were played. "Not finished" is the same test
    /// <c>ReconcileAsync</c> uses to decide what is still owed, so a fixture this says is left
    /// is a fixture the next run of the window will go and play.
    /// </para>
    ///
    /// <para>
    /// It is an existence test and not a count because the answer is a yes or a no about a
    /// window of at most sixteen fixtures, and the window's own row is locked while it is
    /// asked — so the fixtures it is asking about cannot be claimed by anybody else halfway
    /// through the answer.
    /// </para>
    /// </summary>
    private Task<bool> HasFixturesLeftToPlayAsync(Guid roundId, CancellationToken cancellationToken) =>
        _dbContext.Fixtures
            .AsNoTracking()
            .AnyAsync(fixture => fixture.RoundId == roundId && fixture.Status != FixtureStatus.Finished, cancellationToken);

    /// <summary>
    /// The window's own answer to "may I have this", asked of the row the lock has just
    /// read. Every refusal comes out of the state rather than out of the caller, so two
    /// processes are refused by the same rule and not by two implementations of it.
    ///
    /// <para>
    /// "Played" is not among the refusals, because the row cannot answer it: whether a window
    /// has been played out is a question about its fixtures, and the caller has just asked
    /// them.
    /// </para>
    /// </summary>
    private static RoundClaim Decide(Round? round, TimeSpan lease, DateTimeOffset now)
    {
        if (round is null)
        {
            return RoundClaim.NotFound;
        }

        if (round.ExecutionStatus is RoundExecutionStatus.Running
            && !round.HasTheClaimExpired(now, lease))
        {
            return RoundClaim.HeldByAnotherProcess;
        }

        return RoundClaim.Claimed;
    }
}
