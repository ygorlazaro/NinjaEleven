using NinjaEleven.Domain.Competitions;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A window of football may only be played once, and the process that plays it may die while
/// it is playing it.
///
/// <para>
/// Those two facts are in tension and the tension is the whole design: a claim that never
/// expires means a crash owns a round for ever, and a claim that expires too eagerly means
/// two processes play the same round. These tests hold both ends — the refusal, and the
/// takeover — because a test that only checked the first would pass with a lease of one
/// second and a test that only checked the second would pass with no refusal at all.
/// </para>
/// </summary>
public class RoundExecutionTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 16, 0, 0, TimeSpan.Zero);

    private static Round AWindow() => Round.Create(Guid.NewGuid(), 1);

    [Fact]
    public void A_new_window_has_been_executed_by_nobody()
    {
        var window = AWindow();

        Assert.Equal(RoundExecutionStatus.Scheduled, window.ExecutionStatus);
        Assert.False(window.HasBeenExecuted);
    }

    [Fact]
    public void A_window_nobody_has_taken_can_be_taken()
    {
        var window = AWindow();

        Assert.True(window.TryBeginExecution(Now, Lease));
        Assert.Equal(RoundExecutionStatus.Running, window.ExecutionStatus);
        Assert.Equal(Now, window.ExecutionStartedAt);
    }

    [Fact]
    public void A_window_somebody_is_holding_cannot_be_taken_while_the_lease_is_alive()
    {
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);

        Assert.False(window.TryBeginExecution(Now.AddMinutes(5), Lease));
        Assert.Equal(RoundExecutionStatus.Running, window.ExecutionStatus);
    }

    [Fact]
    public void A_window_whose_owner_died_can_be_taken_once_the_lease_has_run_out()
    {
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);

        // Thirty-one minutes later the process that took it has said nothing since, and a
        // round nobody is playing is a round that stops the season.
        Assert.True(window.TryBeginExecution(Now.AddMinutes(31), Lease));
        Assert.Equal(Now.AddMinutes(31), window.ExecutionStartedAt);
    }

    [Fact]
    public void A_window_that_was_played_whole_is_never_taken_again()
    {
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);
        window.CompleteExecution();

        Assert.True(window.HasBeenExecuted);
        Assert.False(window.TryBeginExecution(Now.AddDays(7), Lease));
    }

    [Fact]
    public void A_window_a_manager_played_by_hand_is_never_offered_to_the_scheduler()
    {
        // Nobody claimed it: a manager pressed the button and the window closed on the
        // finish of its last fixture. Its execution status is still the default, and reading
        // only that column would hand a played matchday to the scheduler to play again.
        var window = AWindow();
        window.Complete();

        Assert.Equal(RoundExecutionStatus.Scheduled, window.ExecutionStatus);
        Assert.True(window.HasBeenExecuted);
        Assert.False(window.TryBeginExecution(Now.AddDays(7), Lease));
    }

    [Fact]
    public void A_released_window_can_be_taken_again_at_once()
    {
        // A window that failed is not held for the length of the lease: the next run has to
        // be able to pick the fixtures that are left rather than wait half an hour for them.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);
        window.ReleaseExecution();

        Assert.True(window.TryBeginExecution(Now.AddSeconds(1), Lease));
    }

    [Fact]
    public void A_completed_window_is_written_up_by_the_process_that_still_holds_it()
    {
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);

        Assert.True(window.CanBeCompletedBy(Lease, Now.AddMinutes(5)));
        Assert.False(window.CanBeCompletedBy(Lease, Now.AddMinutes(31)));
    }

    [Fact]
    public void An_untaken_window_is_not_waiting_for_anybody()
    {
        var window = AWindow();

        Assert.False(window.HasTheClaimExpired(Now.AddDays(1), Lease));
        Assert.True(window.CanBeCompletedBy(Lease, Now));
    }

    [Fact]
    public void The_process_holding_the_claim_is_the_one_that_closes_the_window()
    {
        var window = AWindow();
        window.TryBeginExecution(Now, Lease, "scheduler-1");

        Assert.True(window.CanBeCompletedBy(Lease, Now.AddMinutes(5), "scheduler-1"));
    }

    [Fact]
    public void A_process_that_lost_the_claim_does_not_close_the_window_over_the_one_that_took_it()
    {
        // The window the first process was playing, its lease ran out, and a second process
        // took the window over and is finishing it. The first one arrives with its results
        // nine minutes later — inside the *new* claim's lease, so a rule that only asked how
        // young the claim is would let it through and close a window with fixtures still to be
        // played.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease, "scheduler-1");
        window.TryBeginExecution(Now.AddMinutes(31), Lease, "scheduler-2");

        Assert.False(window.CanBeCompletedBy(Lease, Now.AddMinutes(40), "scheduler-1"));
        Assert.True(window.CanBeCompletedBy(Lease, Now.AddMinutes(40), "scheduler-2"));
    }

    [Fact]
    public void A_window_claimed_before_there_were_hosts_is_closed_by_whichever_process_asks()
    {
        // A row written by a world that had no hosts on its claims, and a caller that does not
        // say who it is. Neither of them may be refused on the grounds of an identity that was
        // never recorded.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);

        Assert.True(window.CanBeCompletedBy(Lease, Now, "scheduler-1"));
    }

    [Fact]
    public void A_released_window_cannot_be_released_again_by_the_process_that_used_to_hold_it()
    {
        // Releasing a window somebody else has taken over would clear the new owner's claim
        // and hand the window to a third process, so the refusal is part of the rule rather
        // than a courtesy.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease, "scheduler-1");
        window.TryBeginExecution(Now.AddMinutes(31), Lease, "scheduler-2");

        Assert.False(window.IsHeldBy("scheduler-1"));
        Assert.True(window.IsHeldBy("scheduler-2"));
    }

    [Fact]
    public void A_window_closed_over_a_hole_goes_back_on_the_schedule()
    {
        // A matchday is never lost, and this is the way back from being one. The window was
        // written up as played while a fixture of it was still on the schedule, and the only
        // thing that can undo that is putting the window back where a window with football left
        // in it belongs.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);
        window.CompleteExecution();

        window.ReopenExecution();

        Assert.False(window.HasBeenExecuted);
        Assert.Equal(RoundExecutionStatus.Scheduled, window.ExecutionStatus);
        Assert.Null(window.CompletedAt);
    }

    [Fact]
    public void A_window_closed_by_hand_over_a_hole_goes_back_on_the_schedule_too()
    {
        // A window a manager finished by pressing a button has a completion and no claim, so
        // the reopen has to clear the completion as well — otherwise a window whose last
        // fixture was put back on the schedule is still a window nobody may walk into.
        var window = AWindow();
        window.Complete();

        window.ReopenExecution();

        Assert.False(window.HasBeenExecuted);
        Assert.Null(window.CompletedAt);
    }

    [Fact]
    public void A_window_reopened_over_a_hole_can_be_taken_at_once()
    {
        // The reopen is not a state the world sits in: the next run has to be able to walk
        // straight into the window and play what is left of it.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease);
        window.CompleteExecution();
        window.ReopenExecution();

        Assert.True(window.TryBeginExecution(Now.AddDays(3), Lease));
        Assert.Equal(RoundExecutionStatus.Running, window.ExecutionStatus);
    }

    [Fact]
    public void Reopening_a_window_does_not_lose_the_claim_of_the_process_holding_it()
    {
        // The reopen is about the completion, not about ownership: a window that is being
        // played is not a window with a hole in it, and one that is must not become claimable
        // by a second process while the first one is still on it.
        var window = AWindow();
        window.TryBeginExecution(Now, Lease, "scheduler-1");

        window.ReopenExecution();
        window.TryBeginExecution(Now, Lease, "scheduler-1");

        Assert.False(window.TryBeginExecution(Now.AddMinutes(2), Lease, "scheduler-2"));
    }
}
