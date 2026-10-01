using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Scheduler.Jobs;
using Quartz;

namespace NinjaEleven.Scheduler;

/// <summary>
/// Wires the triggers. It is separate from the jobs and from the host so that "is the
/// championship job registered?" is a question with an answer, and so that turning the
/// scheduler off is one branch rather than three.
/// </summary>
public static class SchedulerRegistration
{
    /// <summary>
    /// The triggers this process owns: the three windows a matchday can have, and the sweep
    /// that hands back the clubs of managers who have gone quiet.
    ///
    /// Each one is a job and a cron expression, and the expression is read from configuration
    /// rather than written here: a schedule that lives in code is a schedule that can only be
    /// changed by a rebuild, and a world being played on somebody's laptop has to be playable
    /// on a different laptop.
    ///
    /// The three waves are typed with the competition they play, because that is what makes
    /// them interchangeable. The sweep is not a wave and has no competition at all — it is a
    /// question asked of the accounts rather than of the calendar — so it carries its own
    /// entry here rather than pretending to be a fourth window.
    /// </summary>
    private static readonly (Type Job, string Identity, Func<SchedulerOptions, string> Cron)[] Triggers =
    [
        (typeof(LeagueRoundJob), "league-round", options => options.LeagueRoundCron),
        (typeof(CupRoundJob), "cup-round", options => options.CupRoundCron),
        (typeof(SuperCupRoundJob), "supercup-round", options => options.SuperCupRoundCron),
        (typeof(ManagerDormancyJob), "manager-dormancy", options => options.ManagerDormancyCron)
    ];

    /// <summary>
    /// The identities a job is registered under, in the order the matchday plays them. It is
    /// public so a test can ask what was set up without reaching into the container's
    /// internals, and so a log line and a trigger can be tied together by name.
    /// </summary>
    public static IReadOnlyList<string> RegisteredIdentities =>
        Triggers.Select(trigger => trigger.Identity).ToList();

    /// <summary>
    /// Adds the jobs and their triggers.
    ///
    /// <para>
    /// Nothing is registered when the scheduler is off, rather than everything being
    /// registered and skipped at run time. A process that is holding back should not be
    /// holding a queue of triggers it is going to refuse, and a test that says "the scheduler
    /// is off" should be able to see that nothing was set up at all.
    /// </para>
    ///
    /// <para>
    /// The trigger store is in memory. That is not a shortcut taken because the world is
    /// small: a persistent Quartz store would make Quartz the record of which jobs exist, and
    /// the record of which windows of football have been played is a column on the window
    /// itself. A scheduler that starts with an empty queue and asks the calendar what is due
    /// cannot be out of step with the world, because it has nothing of its own to be out of
    /// step with. It is also why the catch-up trigger below is not optional decoration.
    /// </para>
    /// </summary>
    public static IServiceCollection AddNinjaElevenScheduler(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SchedulerOptions>(configuration.GetSection(SchedulerOptions.SectionName));
        services.Configure<WorldExecutionOptions>(configuration.GetSection(WorldExecutionOptions.SectionName));

        var options = configuration
            .GetSection(SchedulerOptions.SectionName)
            .Get<SchedulerOptions>() ?? new SchedulerOptions();

        if (!options.Enabled)
        {
            return services;
        }

        // The three crons fire once a day at the hour their competition goes out, which is the
        // moment the calendar says the window does. Nothing here decides when a matchday is
        // due: the job asks the calendar, and the calendar's answer is the same answer the
        // window's own date and hour give it.
        services.AddQuartz(quartz =>
        {
            quartz.UseInMemoryStore();
            quartz.UseDefaultThreadPool();

            foreach (var trigger in Triggers)
            {
                Register(quartz, trigger.Job, trigger.Identity, trigger.Cron(options), options);
            }
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        return services;
    }

    /// <summary>
    /// Registers one wave's job, its trigger and — when the catch-up is on — the one trigger
    /// that fires shortly after startup.
    /// </summary>
    private static void Register(
        IQuartzBuilder quartz,
        Type job,
        string identity,
        string cron,
        SchedulerOptions options)
    {
        quartz.AddJob(job, jobConfigurator => jobConfigurator
            .WithIdentity(identity)
            .StoreDurably()
            // One wave is one job, and one job is one wave at a time. A second fire that
            // arrives while the first is still playing the window would be a second process
            // asking for a window the first one holds, and the claim would refuse it — but
            // refusing it by a database round trip is a poor way to say "busy".
            .DisallowConcurrentExecution());

        // A fire that was missed while the process was down is still worth playing, within a
        // window: the calendar still says the matchday is due and the claim still says nobody
        // has played it. Beyond the misfire threshold it is not, because by then the window
        // has either been played by the next trigger or is a matchday the world has run past.
        quartz.AddTrigger(trigger => trigger
            .ForJob(identity)
            .WithIdentity($"{identity}-cron")
            .WithSchedule(CronScheduleBuilder
                .Create(cron)
                .WithMisfireInstruction(CronTriggerMisfireInstruction.FireAndProceed)));

        if (!options.RunDueOnStartup)
        {
            return;
        }

        // The restart path. A scheduler that was down when a window went out has a matchday
        // the world never played, and with an in-memory trigger store a fresh process has
        // nothing to replay — so it asks the calendar once, a few seconds after it is up, and
        // plays whatever the calendar says is still owed. This is the same call the daily
        // trigger makes; only the reason for making it is different.
        quartz.AddTrigger(trigger => trigger
            .ForJob(identity)
            .WithIdentity($"{identity}-startup")
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, options.StartupDelaySeconds)))
            .WithSimpleSchedule(schedule => schedule
                .WithRepeatCount(0)));
    }
}

/// <summary>
/// Brings the host up, and says so.
///
/// It exists so the log says which process is moving the world, under which identity, with
/// which three cron expressions. A world that moves itself is exactly the thing whose
/// behaviour you cannot work out afterwards without knowing what was scheduled.
/// </summary>
public sealed class SchedulerAnnouncer : IHostedService
{
    private readonly IOptions<SchedulerOptions> _options;
    private readonly IMatchHost _host;
    private readonly ILogger<SchedulerAnnouncer> _logger;

    public SchedulerAnnouncer(
        IOptions<SchedulerOptions> options,
        IMatchHost host,
        ILogger<SchedulerAnnouncer> logger)
    {
        _options = options;
        _host = host;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogWarning(
                "Scheduler started with Scheduler:Enabled = false. The process is up and no job will fire; " +
                "the world only moves when something else plays it.");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Scheduler started as {HostId}. Championship: '{LeagueCron}'. Cup: '{CupCron}'. " +
            "Supercup: '{SuperCupCron}'. Catch-up on startup: {CatchUp}. " +
            "A matchday is a day: the championship goes out at fifteen hundred and the cup at " +
            "twenty-one hundred, and the day after that one is the next day's.",
            _host.HostId,
            options.LeagueRoundCron,
            options.CupRoundCron,
            options.SuperCupRoundCron,
            options.RunDueOnStartup);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Scheduler stopping. A window in progress is left where it is; the next process to " +
            "start finds it and finishes the fixtures that are left.");

        return Task.CompletedTask;
    }
}
