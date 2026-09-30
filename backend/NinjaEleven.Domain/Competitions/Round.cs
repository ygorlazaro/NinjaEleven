namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One window of football: the fixtures of one competition that are played on one matchday.
///
/// A round is no longer a unit of time on its own. Two competitions run at the same time, so
/// a matchday holds a window for the championship and a window for the cup, and a round is
/// the set of fixtures in one of those windows. The window is what energy is recovered
/// against and what a matchday scoreboard shows, so it is the unit the calendar is built on.
///
/// <para>
/// It is also the unit the world is advanced in. The Scheduler asks the calendar what is due,
/// claims the window and plays what is left of it; the claim and the completion live here
/// rather than in the Scheduler, because whether a window has been played is a fact about the
/// world and not about the process that happened to play it.
/// </para>
/// </summary>
public class Round
{
    public Guid Id { get; private set; }
    public Guid CompetitionSeasonId { get; private set; }

    /// <summary>Counted from one inside its own competition: matchday 4, window 2.</summary>
    public int Number { get; private set; }

    /// <summary>The matchday this window belongs to, and null only while it is being built.</summary>
    public Guid? MatchDayId { get; private set; }

    /// <summary>
    /// Which window of the matchday this is. The championship is the first and the cup the
    /// second, so the final of a season is the last football played without anything having to
    /// be sorted afterwards.
    /// </summary>
    public int Window { get; private set; }

    /// <summary>
    /// When the last fixture of this window was finished, and null while any of them are
    /// still to be played. It is the guard that stops the window's recovery from being applied
    /// twice, and it is why a window can be closed exactly once.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// How far whoever moves the world has taken this window: nobody, somebody, or everybody.
    /// </summary>
    public RoundExecutionStatus ExecutionStatus { get; private set; }

    /// <summary>
    /// When the current claim was taken, and null while nobody holds the window. It is what
    /// makes a claim expire: a process that took a window and died holding it must not keep
    /// the window to itself for ever.
    /// </summary>
    public DateTimeOffset? ExecutionStartedAt { get; private set; }

    /// <summary>
    /// Which process took the current claim, and null while nobody holds the window.
    ///
    /// <para>
    /// The timestamp alone cannot say who is allowed to finish a window. When a lease runs out
    /// and a second process takes the window over, the row still says <em>a</em> claim that is
    /// young, and the process that lost it would go on looking like a valid owner for the length
    /// of the new claim — long enough to close a window over the top of the process that had
    /// actually taken it, writing "completed" above fixtures that were still to be played.
    /// </para>
    ///
    /// <para>
    /// So the claim says whose it is, and only that process may complete it. A window claimed
    /// before this column existed, or claimed by a caller that does not say who it is, is held
    /// by nobody in particular and may be completed by whoever asks.
    /// </para>
    /// </summary>
    public string? ExecutionHost { get; private set; }

    private Round() { }

    public static Round Create(Guid competitionSeasonId, int number, int window = CompetitionRules.ChampionshipWindow)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "The round number must be greater than zero.");
        }

        if (window <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "A window is counted from one.");
        }

        return new Round
        {
            Id = Guid.NewGuid(),
            CompetitionSeasonId = competitionSeasonId,
            Number = number,
            Window = window,
            ExecutionStatus = RoundExecutionStatus.Scheduled
        };
    }

    public void ScheduleOn(Guid matchDayId) => MatchDayId = matchDayId;

    public void Unschedule() => MatchDayId = null;

    public void Complete() => CompletedAt = DateTimeOffset.UtcNow;

    /// <summary>Whether every fixture of this window has been played.</summary>
    public bool IsCompleted => CompletedAt is not null;

    /// <summary>
    /// Whether this window has already been played out in full, by the Scheduler or by hand.
    ///
    /// It answers from either column, and that is deliberate: a window whose fixtures are all
    /// finished is a window nobody should walk into again, whichever of the two wrote its row.
    /// A window that was finished by a manager pressing a button has a
    /// <see cref="CompletedAt"/> and a <see cref="RoundExecutionStatus.Scheduled"/> claim, and
    /// reading only the claim would offer it to the Scheduler to be played a second time.
    /// </summary>
    public bool HasBeenExecuted =>
        ExecutionStatus is RoundExecutionStatus.Completed || IsCompleted;

    /// <summary>
    /// Takes the window, or says why it cannot be taken.
    ///
    /// <para>
    /// The check is on the state and not on the caller, so two processes that fire at the same
    /// minute cannot both walk in: the first call moves the window to
    /// <see cref="RoundExecutionStatus.Running"/> and the second is refused by the state it
    /// finds. The database repeats the same test under a row lock, so a second process on a
    /// second machine is refused by the same rule rather than by luck.
    /// </para>
    ///
    /// <para>
    /// A window that is <see cref="RoundExecutionStatus.Running"/> is only claimable once its
    /// claim has gone stale. That is what makes a crashed process recoverable: without the
    /// lease, a round that died at fixture seventeen of thirty-two would be a round nobody is
    /// ever allowed to finish, which is worse than playing a few fixtures twice.
    /// </para>
    /// </summary>
    /// <param name="now">The instant the claim is being taken at.</param>
    /// <param name="lease">How long a claim is honoured before another process may take it.</param>
    /// <param name="host">Which process is taking it, so that only that process may finish it.</param>
    public bool TryBeginExecution(DateTimeOffset now, TimeSpan lease, string? host = null)
    {
        if (HasBeenExecuted)
        {
            return false;
        }

        if (ExecutionStatus is RoundExecutionStatus.Running && !HasTheClaimExpired(now, lease))
        {
            return false;
        }

        ExecutionStatus = RoundExecutionStatus.Running;
        ExecutionStartedAt = now;
        ExecutionHost = host;
        return true;
    }

    /// <summary>
    /// Whether the current claim is old enough for somebody else to take the window.
    /// A window nobody has claimed is never expired, and a window that has been played is
    /// never taken at all.
    /// </summary>
    public bool HasTheClaimExpired(DateTimeOffset now, TimeSpan lease) =>
        ExecutionStatus is RoundExecutionStatus.Running
        && (ExecutionStartedAt is null || ExecutionStartedAt.Value + lease <= now);

    /// <summary>
    /// Whether this process is the one that should be completing this window.
    ///
    /// <para>
    /// It is the mirror of <see cref="TryBeginExecution"/>, and it asks two questions. Is the
    /// claim still alive — because a claim that has gone stale belongs to whoever takes it
    /// next, not to the process that let it lapse. And is this the process that holds it,
    /// because a claim that was taken over while the first process was still playing its
    /// fixtures is a window the first process no longer gets to close.
    /// </para>
    ///
    /// <para>
    /// The second question is the one that needs the owner recorded. A window that only knew
    /// <em>when</em> its claim was taken could not tell a lease that ran out and was retaken
    /// from a lease that is merely young, so the process that lost the claim would still look
    /// like a valid owner for half an hour after it stopped being one — and it would close the
    /// window over the top of the process that had actually taken it over, writing
    /// "completed" above fixtures that were still to be played.
    /// </para>
    /// </summary>
    /// <param name="lease">How long the claim is honoured for.</param>
    /// <param name="now">The instant the completion is happening at.</param>
    /// <param name="host">Which process is asking to complete the window.</param>
    public bool CanBeCompletedBy(TimeSpan lease, DateTimeOffset now, string? host = null) =>
        IsHeldBy(host) && !HasTheClaimExpired(now, lease);

    /// <summary>
    /// Whether the current claim belongs to the given process. A window nobody has claimed
    /// belongs to whoever is asking, because there is nobody to take it from.
    /// </summary>
    public bool IsHeldBy(string? host) =>
        ExecutionHost is null || host is null || string.Equals(ExecutionHost, host, StringComparison.Ordinal);

    /// <summary>
    /// Puts a window that was closed over a hole back on the schedule.
    ///
    /// <para>
    /// This is the way back from <see cref="HasBeenExecuted"/>, and it exists because the two
    /// columns that answer it are a <em>record</em> of the window having been played, not the
    /// fact of it. The fact is in the fixtures: a window whose every fixture is finished is
    /// played whatever its row says, and a window with a fixture nobody has played is not,
    /// whatever its row says. A window that was closed over a hole is a matchday the world
    /// would never offer again, and a matchday is never lost — so whoever has read the
    /// fixtures and found the hole puts the window back rather than leaving a season with a
    /// day missing out of it.
    /// </para>
    ///
    /// <para>
    /// It clears the hand-completion too, for the same reason: a window closed by hand is
    /// closed because its last fixture finished, and a fixture of it that is still on the
    /// schedule says that it did not.
    /// </para>
    /// </summary>
    public void ReopenExecution()
    {
        CompletedAt = null;
        ExecutionStatus = RoundExecutionStatus.Scheduled;
        ExecutionStartedAt = null;
        ExecutionHost = null;
    }

    /// <summary>
    /// Marks the window as played out. Only a window whose every fixture is finished may be
    /// completed, so a round that failed half way through says so instead of closing over a
    /// hole — which is why the caller has to have read the fixtures before it gets here, and
    /// why <see cref="ReopenExecution"/> exists for the window that was closed over one.
    /// </summary>
    public void CompleteExecution()
    {
        ExecutionStatus = RoundExecutionStatus.Completed;
        ExecutionStartedAt = null;
        ExecutionHost = null;
    }

    /// <summary>
    /// Gives the window back without completing it, so a window that failed stays claimable
    /// by the next run rather than being locked to a process that is no longer playing it.
    ///
    /// The status goes back to <see cref="RoundExecutionStatus.Scheduled"/> rather than
    /// staying on <see cref="RoundExecutionStatus.Running"/>: the claim is judged by
    /// <see cref="ExecutionStartedAt"/>, so a released window is already claimable, and a row
    /// that still says "running" is a row that says a process is playing a window that
    /// nobody is playing.
    /// </summary>
    public void ReleaseExecution()
    {
        ExecutionStatus = RoundExecutionStatus.Scheduled;
        ExecutionStartedAt = null;
        ExecutionHost = null;
    }
}
