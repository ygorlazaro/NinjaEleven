using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Api.Realtime;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Services;

namespace NinjaEleven.Api.Realtime;

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

        await RecoverInterruptedMatchesAsync(stoppingToken);

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
    /// A live session only exists in memory. A match that was still open when the
    /// process stopped can therefore never be resumed, so it is abandoned here and its
    /// fixture goes back on the schedule. Without this a restart would leave a fixture
    /// that reports "already started" and can neither be played nor watched.
    /// </summary>
    private async Task RecoverInterruptedMatchesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
            var recovered = await matchService.RecoverInterruptedMatchesAsync(cancellationToken);

            if (recovered > 0)
            {
                _logger.LogWarning(
                    "Abandoned {Count} match(es) interrupted by a restart; their fixtures are playable again.",
                    recovered);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to recover the matches interrupted by a restart.");
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

            if (state.IsFinished || state.IsPaused)
            {
                return Math.Max(1, state.Speed);
            }

            // A penalty of the manager's own club is waiting for him to name the taker.
            // The clock stands still until he does, exactly as it does at the interval, so
            // the loop leaves the match alone instead of ticking it for nothing.
            if (state.Penalty.AwaitingSelection)
            {
                return Math.Max(1, state.Speed);
            }

            if (state.IsHalfTime)
            {
                // A watched match waits for the manager to press "second half". A match
                // of another club has nobody watching, so the loop leaves the interval
                // for it: that is what keeps the whole matchday moving together.
                if (!_sessions.TryGet(matchId, out var headless) || !headless.AutoContinue)
                {
                    return Math.Max(1, state.Speed);
                }

                // And it only leaves the interval once the match the manager is watching
                // has left it too. Without this the other three would run to full time
                // while the manager's match waited for a button, and the round would stop
                // looking like a matchday at all.
                if (RoundIsWaitingOnTheManager(headless.RoundId, matchId))
                {
                    return Math.Max(1, state.Speed);
                }

                var resumed = await matchService.ContinueSecondHalfAsync(matchId, cancellationToken);
                if (!resumed.Accepted)
                {
                    return Math.Max(1, state.Speed);
                }

                await PublishAsync(matchId, resumed.Events.Select(engineEvent => engineEvent.ToDto()).ToList(), cancellationToken);
                return await PublishStateAndScoreAsync(matchId, matchService, cancellationToken);
            }

            var result = await matchService.TickAsync(matchId, cancellationToken);

            if (result.Accepted && result.Events.Count > 0)
            {
                var events = result.Events.Select(engineEvent => engineEvent.ToDto()).ToList();
                await _broadcaster.PublishEventsAsync(matchId, events, cancellationToken);
                await PublishToTheMatchdayAsync(matchId, events, cancellationToken);
            }

            return await PublishStateAndScoreAsync(matchId, matchService, cancellationToken);
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

    /// <summary>
    /// True while the match the manager is watching has not left the first half yet. The
    /// interval is a barrier for the whole matchday, but it has a single owner: a round
    /// with nobody watching has no break to wait for, and if the watched match ends the
    /// others are free to finish on their own. Only the watched match is asked, never a
    /// peer, so two headless matches can never wait for each other.
    /// </summary>
    private bool RoundIsWaitingOnTheManager(Guid roundId, Guid matchId)
    {
        foreach (var otherId in _sessions.ActiveMatchIds)
        {
            if (otherId == matchId)
            {
                continue;
            }

            if (!_sessions.TryGet(otherId, out var other)
                || other.RoundId != roundId
                || other.AutoContinue)
            {
                continue;
            }

            lock (other.Gate)
            {
                if (!other.State.MatchFinished
                    && (other.State.Half == 0 || other.State.HalfTimePauseActive))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private async Task PublishAsync(        Guid matchId,
        IReadOnlyList<MatchEngineEventDto> events,
        CancellationToken cancellationToken)
    {
        if (events.Count > 0)
        {
            await _broadcaster.PublishEventsAsync(matchId, events, cancellationToken);
        }
    }

    /// <summary>
    /// Sends the beats of a match to the clients following the round, so a manager who is
    /// watching his own game can still read the other three.
    ///
    /// The words are the match's own: the same events its own followers receive, published
    /// a second time to a wider group. A matchday that reworded the other clubs' goals
    /// would be four matches and five accounts of the same afternoon.
    ///
    /// A match nobody is watching is a session the loop still has, so its round is known.
    /// One that is not is left alone rather than guessed at: publishing to a round the match
    /// does not belong to would put a stranger's goal on somebody else's scoreboard.
    /// </summary>
    private async Task PublishToTheMatchdayAsync(
        Guid matchId,
        IReadOnlyList<MatchEngineEventDto> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0 || !_sessions.TryGet(matchId, out var session))
        {
            return;
        }

        await _broadcaster.PublishMatchdayEventsAsync(session.RoundId, matchId, events, cancellationToken);
    }

    /// <summary>
    /// Publishes the new state of the match to the clients watching it, and its score
    /// to the clients watching the round, so a manager follows its own match in full
    /// and the rest of the matchday on a scoreboard.
    /// </summary>
    private async Task<int> PublishStateAndScoreAsync(
        Guid matchId,
        MatchService matchService,
        CancellationToken cancellationToken)
    {
        var updated = await matchService.GetStateAsync(matchId, cancellationToken);
        await _broadcaster.PublishStateAsync(matchId, updated.ToDto(), cancellationToken);

        if (updated.IsFinished)
        {
            var resultDto = await matchService.GetResultAsync(matchId, cancellationToken);
            await _broadcaster.PublishResultAsync(matchId, resultDto.ToDto(), cancellationToken);
        }

        var score = await matchService.GetScoreAsync(matchId, cancellationToken);
        await _broadcaster.PublishScoreAsync(score.RoundId, score.ToDto(), cancellationToken);

        return Math.Max(1, updated.Speed);
    }
}
