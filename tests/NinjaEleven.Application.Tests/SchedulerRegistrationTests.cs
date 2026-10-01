using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NinjaEleven.Scheduler;
using Quartz;
using Xunit;
using SchedulerRegistration = NinjaEleven.Scheduler.SchedulerRegistration;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What this process is going to wake up for.
///
/// A trigger that is not registered is a job that never runs, and a job that never runs looks
/// exactly like a job that ran and found nothing to do. The dormancy sweep is the case that
/// matters most: a world whose sweep is not wired hands no club back, so a manager who has
/// gone for ever keeps his chair and the season waits for a person who is not coming — with no
/// error anywhere, because nothing failed. Only the registration answers that.
/// </summary>
public class SchedulerRegistrationTests
{
    [Fact]
    public void TheDormancySweepIsRegistered()
    {
        Assert.Contains("manager-dormancy", SchedulerRegistration.RegisteredIdentities);
    }

    [Fact]
    public void EveryWindowAndTheSweepAreRegistered()
    {
        // Three windows and one sweep. The count is the assertion because the list is what the
        // process sets up, and a fourth job added without a trigger is exactly the failure a
        // membership test would miss.
        Assert.Equal(4, SchedulerRegistration.RegisteredIdentities.Count);
    }

    [Fact]
    public void NothingIsRegisteredWhenTheSchedulerIsOff()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduler:Enabled"] = "false"
            })
            .Build();

        services.AddNinjaElevenScheduler(configuration);
        using var provider = services.BuildServiceProvider();

        // The process is up and holding back: options are still bound, because a process that
        // is off still reads its own configuration, but there is no scheduler behind them and
        // therefore no trigger that will ever fire.
        Assert.Null(provider.GetService<ISchedulerFactory>());
    }

    [Fact]
    public void TheSweepFiresOnItsOwnCronRatherThanAWindowCron()
    {
        // The sweep's schedule is a different question from the championship's: it is measured
        // in days, so sharing a trigger with a window would tie a dismissal to a matchday.
        var options = new SchedulerOptions();

        Assert.NotEqual(options.LeagueRoundCron, options.ManagerDormancyCron);
        Assert.NotEqual(options.CupRoundCron, options.ManagerDormancyCron);
    }
}
