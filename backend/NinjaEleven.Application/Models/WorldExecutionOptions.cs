namespace NinjaEleven.Application.Models;

/// <summary>
/// How the world is moved, in the four numbers that decide how careful it is about it.
///
/// <para>
/// These are not football rules and they are not settings of a screen either: they are the
/// tolerances of a process that shares one world with other processes. A claim lease is how
/// long a window belongs to whoever took it, a due tolerance is how late a window may go out
/// and still be run, a session lease is how long a match's working memory is honoured after
/// the process holding it stops answering, and the parallelism is how much of a matchday the
/// world plays at once.
/// </para>
///
/// <para>
/// They live in the configuration rather than in the constants of the scheduler because they
/// have to be answerable by whoever is running the world, and a world being debugged wants
/// them to be different from a world being played.
/// </para>
///
/// <para>
/// There is no speed here on purpose. A matchday is a day, a window goes out at the hour its
/// competition goes out, and a season takes thirty-four days. A world that wants to be moved
/// by hand is moved by hand through <c>POST /world/advance</c>, which walks the same calendar
/// a day at a time — the shape is the calendar's and not a setting, so a world being hurried
/// and a world being played are the same football.
/// </para>
/// </summary>
public class WorldExecutionOptions
{
    public const string SectionName = "World";

    /// <summary>
    /// How long one process owns a window of football after taking it. A window whose claim
    /// is older than this may be taken by another process, which is what stops a scheduler
    /// that dies mid-round from owning that round for ever.
    /// </summary>
    public TimeSpan RoundClaimLease { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How late a window of the calendar may go out and still count as due. It is the width
    /// of the net under a job that fired a minute late, a process that was restarting when
    /// the hour came round, or a world that was down over a matchday and owes two of them.
    ///
    /// <para>
    /// It reaches backwards as well as forwards, which is deliberate: a matchday's window goes
    /// out at fifteen hundred and the cup's at twenty-one hundred, so half an hour either way
    /// is a kindness measured against six hours of football rather than a licence to skip a
    /// day. A world whose windows are a day apart cannot swallow a whole matchday with a
    /// tolerance of half an hour.
    /// </para>
    /// </summary>
    public TimeSpan DueTolerance { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long a match's working memory is honoured after the process holding it last ticked.
    /// A match whose owner has been quiet for longer than this is anybody's to take over, so a
    /// process killed mid-match does not leave a fixture in progress for ever.
    /// </summary>
    public TimeSpan SessionLease { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How many fixtures of a window are played at the same time. A matchday is not a
    /// sequence of games — every division plays in the same wave — so this is what a wave
    /// costs the database, and it is bounded so a thirty-two match window does not open
    /// thirty-two chains of work at once.
    /// </summary>
    public int MaxParallelFixtures { get; set; } = 8;
}
