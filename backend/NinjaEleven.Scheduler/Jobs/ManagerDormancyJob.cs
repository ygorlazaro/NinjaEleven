using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Services;
using Quartz;

namespace NinjaEleven.Scheduler.Jobs;

/// <summary>
/// Looks for managers who have stopped signing in and hands their clubs back to the world.
///
/// The job is thin on purpose, exactly like the three that play the football: it knows when
/// to look and it asks <see cref="ManagerService"/> who is overdue. How long a manager may be
/// away, what happens to the account, and what happens to the club are rules of the game and
/// they live with the rest of them — a job that restated them would be a second opinion about
/// them, and a second opinion is how two answers end up where there should be one.
///
/// It is a daily fire rather than an hourly one because a dismissal is measured in days. The
/// cost of looking too often is a question asked of every manager in the world; the cost of
/// looking too rarely is a club left waiting on a person who is not coming, which is the whole
/// failure this exists to prevent.
/// </summary>
public sealed class ManagerDormancyJob : IJob
{
    private readonly ManagerService _managers;
    private readonly ILogger<ManagerDormancyJob> _logger;

    public ManagerDormancyJob(ManagerService managers, ILogger<ManagerDormancyJob> logger)
    {
        _managers = managers;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var dismissed = await _managers.DismissTheManagersWhoHaveGoneQuietAsync(cancellationToken);

            if (dismissed.Count == 0)
            {
                _logger.LogDebug("Dormancy sweep: every manager in the world is still signing in.");
                return;
            }

            _logger.LogWarning(
                "Dormancy sweep dismissed {Count} manager(s): {Clubs}.",
                dismissed.Count,
                string.Join(", ", dismissed));
        }
        catch (Exception exception)
        {
            // Logged, not retried, and rethrown so Quartz records the fire as failed. Nothing
            // is lost by a sweep that does not happen: the next day's sweep asks the same
            // question of the same world and a manager thirty-one days quiet is still quiet
            // on the thirty-second. What must not happen is a dismissal that half-applied.
            _logger.LogError(exception, "The dormancy sweep failed.");

            throw new JobExecutionException($"The dormancy sweep failed: {exception.Message}", exception);
        }
    }
}
