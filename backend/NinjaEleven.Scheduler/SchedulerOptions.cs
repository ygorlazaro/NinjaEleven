namespace NinjaEleven.Scheduler;

/// <summary>
/// When the world is woken up, and whether it is woken up at all.
///
/// <para>
/// Every value here is a question about the clock and none of them is a question about
/// football. The championship window of a matchday goes out at sixteen and the cup window at
/// twenty, and those hours are rules of the game and live in the domain with the rest of them
/// — what is configured here is <i>when this process looks</i>, not when the world plays.
/// The two are the same by default and are deliberately allowed to drift: a world being
/// debugged wants to be looked at every minute, and the calendar still says which windows
/// are due.
/// </para>
/// </summary>
public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>
    /// Whether this process fires its jobs. Off means the process starts, wires everything
    /// up, logs that it is holding back and does nothing else — which is what an environment
    /// that wants the code present but the world still needs a developer to step on wants.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When to look for a championship window that is due. Quartz cron, in the timezone the
    /// host runs in.
    /// </summary>
    public string LeagueRoundCron { get; set; } = "0 5 16 * * *";

    /// <summary>When to look for a cup window that is due.</summary>
    public string CupRoundCron { get; set; } = "0 5 20 * * *";

    /// <summary>
    /// When to look for a Supercup, which is one match on the first matchday of a season and
    /// is the window the day opens with. It has a trigger of its own for the same reason the
    /// cup does: it is a wave of the matchday like any other, and a wave with no trigger is a
    /// wave nobody plays.
    /// </summary>
    public string SuperCupRoundCron { get; set; } = "0 5 13 * * *";

    /// <summary>
    /// When to look for managers who have not signed in for long enough to be dismissed.
    ///
    /// Daily, and early, for one reason: the sweep hands clubs back to the world, and a club
    /// that is handed back at night has a whole day of NPC-managed football behind it before
    /// anybody is looking. The threshold it measures against is in days, so a fire an hour
    /// earlier would find exactly the same managers and say exactly the same thing.
    /// </summary>
    public string ManagerDormancyCron { get; set; } = "0 17 3 * * *";

    /// <summary>
    /// Whether to play whatever is already due a few seconds after this process starts.
    ///
    /// It is the restart path. A scheduler that was down when a window went out has a matchday
    /// the world never played, and without this the world would owe it until the next
    /// scheduled hour — and if the process was down over a whole matchday, the next hour
    /// would find it and the one after it as well, a matchday at a time. The trigger store
    /// is in memory, so this is also what makes a fresh process know about a day the old one
    /// was supposed to play.
    /// </summary>
    public bool RunDueOnStartup { get; set; } = true;

    /// <summary>
    /// How long after startup the catch-up runs, in seconds. Long enough for the database to
    /// have migrated and for the log to have said what it is doing.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 10;

    /// <summary>
    /// How late a fire may be and still be worth acting on, in seconds. A process that was
    /// stopped for a minute and started again has a trigger that missed, and this is what
    /// says the missed one is still worth playing rather than dropped on the floor.
    /// </summary>
    public int MisfireThresholdSeconds { get; set; } = 300;

    /// <summary>
    /// Whether the scheduler runs the database's own migrations on startup. Off by default,
    /// for the same reason the API's seeding is: a schema is changed by a person, not by a
    /// process that happened to boot. It is here so a development machine can be brought up
    /// in one command.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}
