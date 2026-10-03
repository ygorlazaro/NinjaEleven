namespace NinjaEleven.Domain.Teams;

/// <summary>
/// How a club's ground is being built, and how far along it is.
///
/// <para>
/// The project itself — the seats, the price and the time — is not stored here. It is read from
/// <see cref="StadiumRules"/> by its seat count, so a club that started the ten-thousand stand
/// and a club that starts it next season are the same project, and a rule that was retuned
/// between the two reaches both. Storing a copy of the cost would leave sixty-four rows saying
/// one thing and the file saying another, and the book would be the one nobody could check.
/// </para>
///
/// <para>
/// What is stored is the part that is a fact about this club's season: which ground, which
/// season, how many seats were asked for, when the work started and whether it has finished.
/// The completion is idempotent — a project can be closed once and then only be read as closed —
/// because the world closes its windows more than once and a second close that added the seats
/// again would be a club whose ground grew every time the Scheduler ran.
/// </para>
/// </summary>
public class StadiumConstruction
{
    public Guid Id { get; private set; }

    /// <summary>The club doing the building.</summary>
    public Guid ClubId { get; private set; }

    /// <summary>The ground being built. A club has one, so this is also the club's stadium.</summary>
    public Guid StadiumId { get; private set; }

    /// <summary>The season the work was paid for in. A ground does not straddle two books.</summary>
    public Guid SeasonId { get; private set; }

    /// <summary>How many seats the project adds, and the key it is read back by.</summary>
    public int Seats { get; private set; }

    /// <summary>What the club paid, kept so the ledger line and the project cannot disagree.</summary>
    public decimal Cost { get; private set; }

    /// <summary>How many rounds of football the work takes, kept for the same reason.</summary>
    public int Rounds { get; private set; }

    /// <summary>
    /// The last round that had been played when the work started, counted from one.
    ///
    /// <para>
    /// It is a round that has already happened rather than one about to, so that "two rounds of
    /// work" means two rounds the club then plays in the building site, and never a project that
    /// is finished on the day it was paid for.
    /// </para>
    /// </summary>
    public int StartedAfterRound { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When the seats arrived, and null while the work is still going on.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private StadiumConstruction() { }

    /// <summary>
    /// Starts a project.
    /// </summary>
    /// <param name="clubId">The club doing the building.</param>
    /// <param name="stadiumId">The ground being built.</param>
    /// <param name="seasonId">The season the cost is charged to.</param>
    /// <param name="project">Which of the three projects this is.</param>
    /// <param name="startedAfterRound">The last round already played when the work began.</param>
    /// <param name="now">When the work began.</param>
    public static StadiumConstruction Create(
        Guid clubId,
        Guid stadiumId,
        Guid seasonId,
        StadiumProject project,
        int startedAfterRound,
        DateTimeOffset now)
    {
        if (clubId == Guid.Empty)
        {
            throw new ArgumentException("A construction belongs to a club.", nameof(clubId));
        }

        if (stadiumId == Guid.Empty)
        {
            throw new ArgumentException("A construction belongs to a ground.", nameof(stadiumId));
        }

        if (seasonId == Guid.Empty)
        {
            throw new ArgumentException("A construction belongs to a season.", nameof(seasonId));
        }

        if (StadiumRules.ProjectOf(project.Seats) is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(project),
                project.Seats,
                "A club may only build one of the projects the catalogue has.");
        }

        if (startedAfterRound < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startedAfterRound), startedAfterRound, "No round has been played before the first.");
        }

        return new StadiumConstruction
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            StadiumId = stadiumId,
            SeasonId = seasonId,
            Seats = project.Seats,
            Cost = project.Cost,
            Rounds = project.Rounds,
            StartedAfterRound = startedAfterRound,
            StartedAt = now
        };
    }

    public bool IsFinished => CompletedAt is not null;

    /// <summary>
    /// The round at which the seats arrive: the last round played when the work started, plus
    /// the rounds the work takes.
    /// </summary>
    public int FinishesAfterRound => StartedAfterRound + Rounds;

    /// <summary>
    /// Whether the work is done, judged against the last round that has been played.
    /// </summary>
    /// <param name="playedThroughRound">The highest round number played so far, counted from one.</param>
    public bool IsDue(int playedThroughRound) => playedThroughRound >= FinishesAfterRound;

    /// <summary>How many rounds of the work are still to come, never less than none.</summary>
    /// <param name="playedThroughRound">The highest round number played so far, counted from one.</param>
    public int RoundsRemaining(int playedThroughRound) => Math.Max(0, FinishesAfterRound - playedThroughRound);

    /// <summary>
    /// Closes the work, and says whether this call was the one that did it.
    ///
    /// <para>
    /// The answer is what makes the close safe to run from two places. The world closes its
    /// windows more than once — a manager can finish a matchday by hand and the Scheduler can
    /// come along afterwards and find nothing owed — and a close that only added the seats on
    /// the first call would be right, while one that added them every time would be a ground
    /// that grows a stand per Scheduler run. So the second caller is told it closed nothing,
    /// and the caller that gets that answer does not touch the ground.
    /// </para>
    /// </summary>
    /// <param name="now">When the seats arrived.</param>
    public bool Complete(DateTimeOffset now)
    {
        if (IsFinished) return false;

        CompletedAt = now;

        return true;
    }
}