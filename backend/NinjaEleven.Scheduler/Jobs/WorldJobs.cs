using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using Quartz;

namespace NinjaEleven.Scheduler.Jobs;

/// <summary>
/// The base every job in this process is built on, and the reason they are all the same.
///
/// <para>
/// A job here does two things: it asks the Application layer what the world owes, and it
/// reports how it went. It holds no rule of football, no random source, no eleven and no
/// probability, and there is no code path by which it could — everything below this line is in
/// the Application and Domain projects, and this class only decides <i>when</i> to call it.
/// </para>
///
/// <para>
/// Its dependencies are ordinary constructor parameters resolved from the container for every
/// fire, so a job is a scoped service like any other and a day of thirty-two matches is
/// thirty-two scopes inside one run rather than one object shared by every run of every job.
/// </para>
///
/// <para>
/// <b>Nothing here retries.</b> No retry policy is attached to a trigger and there is no loop
/// around the call: a job that throws is logged and ends, and the windows it was going to play
/// are left exactly as they were found — claimed or not, and with whatever fixtures were
/// already finished still finished. The next trigger finds them again, because a window's own
/// state is what says whether it has been played, not whether a job has run recently.
/// </para>
/// </summary>
public abstract class WorldJob : IJob
{
    private readonly CompetitionExecutionService _execution;
    private readonly ILogger _logger;

    protected WorldJob(CompetitionExecutionService execution, ILoggerFactory loggerFactory)
    {
        _execution = execution;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    /// <summary>Which wave of the matchday this job is responsible for waking up.</summary>
    protected abstract CompetitionType Wave { get; }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        var wave = Wave;
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("{Wave} round job triggered.", wave);

        try
        {
            // The world is told what it owes, not what to do: the calendar decides which
            // windows are due, and the claim in the database decides which of them this
            // process may play.
            var runs = await _execution.PlayDueRoundsAsync(wave, cancellationToken: cancellationToken);
            var duration = DateTimeOffset.UtcNow - startedAt;

            if (runs.Count == 0)
            {
                _logger.LogInformation(
                    "{Wave} round job finished: nothing was due. Duration {Duration}.",
                    wave,
                    duration);
                return;
            }

            foreach (var run in runs)
            {
                _logger.LogInformation(
                    "{Wave} window {RoundId}: {Played} played, {Already} were already played, " +
                    "{Failed} failed, {Elsewhere} already being played, {ForManager} started for the manager. " +
                    "Complete: {Complete}. Duration {Duration}.",
                    wave,
                    run.RoundId,
                    run.Played,
                    run.AlreadyPlayed,
                    run.Failed,
                    run.PlayedElsewhere,
                    run.StartedForTheManager,
                    run.IsComplete,
                    run.Duration);
            }

            _logger.LogInformation(
                "{Wave} round job finished: {Count} window(s). Duration {Duration}.",
                wave,
                runs.Count,
                duration);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("{Wave} round job was cancelled while it was running.", wave);
        }
        catch (Exception exception)
        {
            // Logged, not retried, and rethrown so Quartz records the fire as failed. The
            // windows this run would have played are still open and still due, so the next
            // trigger finds them again; what must not happen is this class quietly trying
            // three more times and burying the first failure.
            _logger.LogError(exception, "{Wave} round job failed.", wave);

            throw new JobExecutionException($"The {wave} round job failed: {exception.Message}", exception);
        }
    }
}

/// <summary>
/// Wakes up the championship.
///
/// Thirty-two fixtures across four divisions, all in the same wave: a matchday is not a
/// sequence of games, and a pyramid where the first division has played two more games than
/// the third is not a pyramid. The job does not say any of that — the calendar's wave order
/// does — it only asks for the championship wave to be played if the calendar says it is due.
/// </summary>
public sealed class LeagueRoundJob : WorldJob
{
    public LeagueRoundJob(CompetitionExecutionService execution, ILoggerFactory loggerFactory)
        : base(execution, loggerFactory)
    {
    }

    protected override CompetitionType Wave => CompetitionType.League;
}

/// <summary>
/// Wakes up the cup.
///
/// The same shape as the championship and not a different kind of job. The cup's own rules
/// are the ones already in the domain — sixty-four clubs, no byes, two legs, a bracket that
/// advances on the finish of a tie — and none of them is restated here. The window is only
/// played once the championship wave of the same day is over, because that is what a matchday
/// is, and it is the Application layer that enforces it.
/// </summary>
public sealed class CupRoundJob : WorldJob
{
    public CupRoundJob(CompetitionExecutionService execution, ILoggerFactory loggerFactory)
        : base(execution, loggerFactory)
    {
    }

    protected override CompetitionType Wave => CompetitionType.Cup;
}

/// <summary>
/// Wakes up the Supercup.
///
/// One match, on the first matchday of a season, in the window the day opens with. It is a
/// job of its own for the same reason the cup is: it is a wave of the matchday, and a wave
/// with no trigger is a wave nobody plays — a season whose first day waits for a competition
/// that is not coming until the evening.
/// </summary>
public sealed class SuperCupRoundJob : WorldJob
{
    public SuperCupRoundJob(CompetitionExecutionService execution, ILoggerFactory loggerFactory)
        : base(execution, loggerFactory)
    {
    }

    protected override CompetitionType Wave => CompetitionType.SuperCup;
}
