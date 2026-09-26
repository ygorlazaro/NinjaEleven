using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Api.Realtime;
using FootballManager.Application.Matches;
using FootballManager.Application.Services;

namespace FootballManager.Api.Realtime;

/// <summary>
/// The single simulation loop. It owns no football rules: it walks the matches that are
/// currently being played, asks the service to advance each one by a tick, and
/// republishes what came back. Because the loop is the only caller that advances the
/// clock, a match can never be simulated by two callers at once.
/// </summary>
public sealed class MatchLoopService : BackgroundService
{
    /// <summary>
    /// Real milliseconds between two ticks at 1x. The engine advances one minute per
    /// tick, so a full match takes about a minute and a half to watch.
    /// </summary>
    private const int BaseTickIntervalMs = 1000;

    private const int IdlePollIntervalMs = 250;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMatchSessionRegistry _sessions;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly ILogger<MatchLoopService> _logger;

    public MatchLoopService(
        IServiceScopeFactory scopeFactory,
        IMatchSessionRegistry sessions,
        IMatchBroadcaster broadcaster,
        ILogger<MatchLoopService> logger)
    {
        _scopeFactory = scopeFactory;
        _sessions = sessions;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Match loop started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var activeMatches = _sessions.ActiveMatchIds;
            var shortestWait = BaseTickIntervalMs;

            foreach (var matchId in activeMatches)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                var speed = await AdvanceAsync(matchId, stoppingToken);
                if (speed > 1)
                {
                    shortestWait = Math.Min(shortestWait, BaseTickIntervalMs / speed);
                }
            }

            if (activeMatches.Count == 0)
            {
                shortestWait = IdlePollIntervalMs;
            }

            try
            {
                await Task.Delay(shortestWait, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Advances one match and publishes what changed. Returns the session speed so the
    /// loop can shorten its own wait: the speed is a playback preference, never a rule
    /// of the simulation.
    /// </summary>
    private async Task<int> AdvanceAsync(Guid matchId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();

            var state = await matchService.GetStateAsync(matchId, cancellationToken);

            if (state.IsFinished || state.IsPaused || state.IsHalfTime)
            {
                return Math.Max(1, state.Speed);
            }

            var result = await matchService.TickAsync(matchId, cancellationToken);

            if (result.Accepted && result.Events.Count > 0)
            {
                await _broadcaster.PublishEventsAsync(
                    matchId,
                    result.Events.Select(engineEvent => engineEvent.ToDto()).ToList(),
                    cancellationToken);
            }

            var updated = await matchService.GetStateAsync(matchId, cancellationToken);
            await _broadcaster.PublishStateAsync(matchId, updated.ToDto(), cancellationToken);

            if (updated.IsFinished)
            {
                var resultDto = await matchService.GetResultAsync(matchId, cancellationToken);
                await _broadcaster.PublishResultAsync(matchId, resultDto.ToDto(), cancellationToken);
            }

            return Math.Max(1, updated.Speed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to advance match {MatchId}", matchId);
            return 1;
        }
    }
}
