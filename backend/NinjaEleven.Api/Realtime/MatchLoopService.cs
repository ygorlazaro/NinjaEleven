using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Api.Realtime;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Matches;
using NinjaEleven.Domain.Common;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;

namespace NinjaEleven.Api.Realtime;

/// <summary>
/// The single simulation loop. It owns no football rules: it walks the matches that are
/// currently being played, asks the service to advance each one by a tick, and
/// republishes what came back. Because the loop is the only caller that advances the
/// clock, a match can never be simulated by two callers at once.
///
/// <para>
/// It also reads, rather than plays, the matches another process is playing. The Scheduler
/// moves the windows nobody is watching and the API owns the connections, so a match the
/// manager is watching may well be a match this process never kicked off. Those are read
/// out of the database — which is the truth about them — and republished, because a client
/// following a match that is being played is entitled to see it whatever process is
/// playing it. Nothing here advances a foreign match: two simulators on one match is the one
/// thing this loop exists to prevent.
/// </para>
/// </summary>
public sealed class MatchLoopService : BackgroundService
{
    /// <summary>
    /// Real milliseconds between two ticks at 1x. The engine advances one minute per
    /// tick, so a full match takes about a minute and a half to watch.
    /// </summary>
    private const int BaseTickIntervalMs = 1000;

    private const int IdlePollIntervalMs = 250;

    /// <summary>
    /// Real milliseconds between two kicks of a shootout when there is nothing faster to
    /// watch. A tick of a match is half a minute of football and a kick at the spot is a
    /// walk from the circle, a run-up and a shot: the same second of wall clock is not the
    /// same thing in the two, and a shootout that went by at the pace of a match would be a
    /// row of numbers appearing rather than a manager watching five men take a penalty.
    /// </summary>
    private const int ShootoutTickIntervalMs = BaseTickIntervalMs * 3;

    /// <summary>
    /// How often the loop goes looking for a match whose owner stopped answering.
    ///
    /// <para>
    /// A lease is five minutes, and the sweep has to be slower than the moment a lease runs
    /// out rather than faster than it: reclaiming a live match would take the football away
    /// from the process playing it, and a sweep faster than the lease is a race the reclaim
    /// wins. Thirty seconds puts the worst case — a match nobody is playing, sitting on a
    /// dead owner's name — at about half a minute past the lease rather than never.
    /// </para>
    ///
    /// <para>
    /// It repeats because a process that is already running is the normal case, not the
    /// exception. Rescuing interrupted matches only at startup rescues a match whose owner
    /// died <i>before</i> this one started: a host that goes away while the API is up leaves a
    /// match frozen at the minute it was on, its fixture held, and its window reporting a
    /// match "played by another process" for ever, with nobody left to play it. The owner
    /// stops being its owner five minutes after it dies, whether or not anybody was looking.
    /// </para>
    /// </summary>
    private static readonly TimeSpan RecoverySweepInterval = TimeSpan.FromSeconds(30);

    private DateTimeOffset _lastRecoverySweep = DateTimeOffset.MinValue;

    /// <summary>
    /// How far a foreign match has been republished. A match is first seen at whatever
    /// sequence it is on, and everything after that is what this process has not told its
    /// clients yet — so a manager who opens a match at minute sixty is sent the state at
    /// minute sixty and then the events that follow it, which is the whole of what a
    /// reconnect is.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, int> _republished = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMatchSessionRegistry _sessions;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly IMatchHost _host;
    private readonly ILogger<MatchLoopService> _logger;

    public MatchLoopService(
        IServiceScopeFactory scopeFactory,
        IMatchSessionRegistry sessions,
        IMatchBroadcaster broadcaster,
        IMatchHost host,
        ILogger<MatchLoopService> logger)
    {
        _scopeFactory = scopeFactory;
        _sessions = sessions;
        _broadcaster = broadcaster;
        _host = host;
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

            var atTheSpot = 0;

            foreach (var matchId in activeMatches)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                var advance = await AdvanceAsync(matchId, stoppingToken);
                if (advance.InShootout)
                {
                    atTheSpot++;
                }
                else if (advance.Speed > 1)
                {
                    shortestWait = Math.Min(shortestWait, BaseTickIntervalMs / advance.Speed);
                }
            }

            if (activeMatches.Count == 0)
            {
                shortestWait = IdlePollIntervalMs;
            }
            else if (atTheSpot == activeMatches.Count)
            {
                // Everything on the pitch is at the spot, so the only thing there is to watch
                // is a kick, and a kick is not a minute of football.
                shortestWait = ShootoutTickIntervalMs;
            }

            // The matches somebody else is playing, read rather than played. It is on the same
            // pass as the loop's own because a client watching a matchday should not be able to
            // tell which process is playing which of its fixtures.
            await PublishForeignMatchesAsync(stoppingToken);

            // And the matches nobody is playing any more, on a slower beat than the loop's
            // own. This is the pass that notices an owner that went away while this process
            // was already up.
            if (DateTimeOffset.UtcNow - _lastRecoverySweep >= RecoverySweepInterval)
            {
                _lastRecoverySweep = DateTimeOffset.UtcNow;
                await RecoverInterruptedMatchesAsync(stoppingToken);
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
    /// Republishes the matches another process is playing.
    ///
    /// <para>
    /// The world is played by more than one process now, and this one is the one holding the
    /// connections. A match the Scheduler kicked off is a real match with real events, and a
    /// client that subscribed to it is entitled to see them — so they are read out of the
    /// event log and pushed, at the same moment they were written down, by whichever process
    /// happens to own the socket.
    /// </para>
    ///
    /// <para>
    /// It is a read and nothing more. A foreign match is never ticked here, never paused and
    /// never resumed: the process that kicked it off is the only one that may move its clock,
    /// and a loop that adopted somebody else's match would be the exact failure this whole
    /// arrangement exists to avoid.
    /// </para>
    ///
    /// <para>
    /// It is also cheap when there is nothing to do. One query a second returns nothing while
    /// the API is playing the whole matchday itself, which is every matchday on a machine with
    /// no scheduler running, and the dictionary is emptied the moment a match is no longer
    /// live so a season of football does not leave a season of rows behind it.
    /// </para>
    /// </summary>
    private async Task PublishForeignMatchesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();

            var live = await repository.ListLiveExceptHostAsync(_host.HostId, stoppingToken);

            foreach (var row in live)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                var match = row.Match;

                // The first time a match is seen it is seen whole: the state goes out at the
                // sequence it is on, and only what comes after that is anybody's business.
                // Replaying its history to a group that may have nobody in it would be a great
                // deal of work for a screen that is not there.
                var since = _republished.GetOrAdd(match.Id, _ => match.Sequence);

                var events = (await matchService.GetEventsAsync(match.Id, since, stoppingToken))
                    .Where(played => played.Sequence > since)
                    .ToList();

                if (events.Count > 0)
                {
                    var published = events.Select(played => played.ToDto()).ToEngineDtos();

                    await _broadcaster.PublishEventsAsync(match.Id, published, stoppingToken);
                    await _broadcaster.PublishMatchdayEventsAsync(row.RoundId, match.Id, published, stoppingToken);

                    _republished[match.Id] = events[^1].Sequence;
                }

                var state = await matchService.GetStateAsync(match.Id, stoppingToken);
                await _broadcaster.PublishStateAsync(match.Id, state.ToDto(), stoppingToken);

                var score = await matchService.GetScoreAsync(match.Id, stoppingToken);
                await _broadcaster.PublishScoreAsync(row.RoundId, score.ToDto(), stoppingToken);
            }

            // A match that has left the live list is one this process will not see again, and
            // its high-water mark would otherwise stay in memory until the season ended.
            if (live.Count > 0)
            {
                var current = live.Select(row => row.Match.Id).ToHashSet();

                foreach (var forgotten in _republished.Keys.Where(id => !current.Contains(id)).ToList())
                {
                    _republished.TryRemove(forgotten, out _);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down. Nothing was missed: the database has the whole match in it.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to republish the matches another process is playing.");
        }
    }

    /// <summary>
    /// A live session only exists in memory. A match that was still open when the
    /// process stopped can therefore never be resumed, so it is abandoned here and its
    /// fixture goes back on the schedule. Without this a restart would leave a fixture
    /// that reports "already started" and can neither be played nor watched.
    /// </summary>
    /// <summary>
    /// Hands back every match this process is playing before it goes.
    ///
    /// <para>
    /// The loop is the only thing that knows which matches this process is driving, so it is
    /// the only place that can say goodbye to them. A process that stops without doing this
    /// looks exactly like one that was killed, and the next one waits out a five-minute lease
    /// on a match nobody is playing — which is charged to whoever was watching it, as a frozen
    /// scoreboard, for a restart that was supposed to cost nothing.
    /// </para>
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();

            await matchService.ReleaseTheMatchesOfThisHostAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // A shutdown that cannot reach the database is still a shutdown. The matches fall
            // back to waiting out their lease, which is what happened before this existed.
            _logger.LogError(exception, "Failed to release the matches of this host on the way out.");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task RecoverInterruptedMatchesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
            var recovered = await matchService.RecoverInterruptedMatchesAsync(
                cancellationToken,
                leaveRunningMatchesAlone: true);

            if (recovered.Count > 0)
            {
                _logger.LogWarning(
                    "Abandoned {Count} match(es) interrupted by a restart; their fixtures are playable again.",
                    recovered.Count);

                // Told to whoever was watching them. A match that was given up on stops being
                // driven, so without this the last thing its followers ever hear is a minute
                // that never advances — a frozen scoreboard and no end to the match. The group
                // is still theirs, the match is simply over, and the state they are sent says
                // so.
                foreach (var abandonedMatchId in recovered)
                {
                    var state = await matchService.GetStateAsync(abandonedMatchId, cancellationToken);
                    await _broadcaster.PublishStateAsync(abandonedMatchId, state.ToDto(), cancellationToken);
                }
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
    private async Task<(int Speed, bool InShootout)> AdvanceAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        try
        {
            // A match somebody else is driving is not this loop's to drive. The headless
            // player walks a match of the world's own from kick-off to the final whistle in
            // one go, and the world can also open the manager's own match and stop — both sit
            // in the registry like any other, so without this the loop moves the same clock at
            // the same time, and whichever reaches full time first leaves the other asking for
            // a second half of a match that is already over. A match left on the touchline for
            // the manager is nobody's to drive at all until he claims it: a loop that ran it
            // out from under him is the world playing his evening for him.
            if (_sessions.TryGet(matchId, out var owned) && owned.Driver is not MatchDriver.None)
            {
                return (Math.Max(1, owned.State.Speed), false);
            }

            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();

            // A claim nobody is answering is given back before anything below decides the
            // match is waiting. The two decisions that hold the clock for a manager are worth
            // waiting for a person and not worth waiting for a closed tab, and the loop is
            // the only thing that keeps looking at the match while nobody does.
            await matchService.ReleaseAStaleClaimAsync(matchId, cancellationToken);

            // A window whose time is up is closed here, by the backend's clock and not by
            // anything a screen did: the penalty goes to somebody drawn from the eleven, the
            // man who cannot carry on is replaced by the bench, and an interval nobody ended
            // ends itself. What came out of it is published, because a penalty taken in
            // silence is a goal the manager never saw scored.
            var expired = await matchService.CloseTheExpiredWindowsAsync(matchId, cancellationToken);

            if (expired.Count > 0)
            {
                await PublishAsync(matchId, expired.Select(engineEvent => engineEvent.ToDto()).ToList(), cancellationToken);
            }

            var state = await matchService.GetStateAsync(matchId, cancellationToken);

            if (state.IsFinished || state.IsPaused)
            {
                return (Math.Max(1, state.Speed), false);
            }

            // A match at the spot is not a match being played: it is a kick a tick, and the
            // loop paces it differently because a walk from the circle is not a minute of
            // football.
            if (state.Shootout is not null)
            {
                return (Math.Max(1, state.Speed),
                    await AdvanceShootoutAsync(matchId, matchService, state, cancellationToken));
            }

            // A penalty of the manager's own club is the one window in a match that still
            // stops the clock, and it stops for fifteen seconds and no longer: the loop
            // leaves the match alone until the deadline above takes the decision away.
            if (state.Penalty.AwaitingSelection)
            {
                return (Math.Max(1, state.Speed), false);
            }

            // A man who cannot carry on no longer stops anything. The window is open and the
            // match goes on around it, and the deadline above is what puts somebody on for
            // him if the manager has not named one.

            if (state.IsHalfTime)
            {
                // A watched match keeps the break the backend is counting down, and the count
                // ending it is above: the manager may end it early, he may not keep it. A
                // match of another club has nobody watching, so the loop leaves the interval
                // for it at once — that is what keeps the whole matchday moving together.
                if (!_sessions.TryGet(matchId, out var headless) || !headless.AutoContinue)
                {
                    return (Math.Max(1, state.Speed), false);
                }

                // And it only leaves the interval once the match the manager is watching
                // has left it too. Without this the other three would run to full time
                // while the manager's match waited for a button, and the round would stop
                // looking like a matchday at all.
                if (TheManagerIsStillAtHalfTime(matchId))
                {
                    return (Math.Max(1, state.Speed), false);
                }

                var resumed = await matchService.ContinueSecondHalfAsync(matchId, cancellationToken);
                if (!resumed.Accepted)
                {
                    return (Math.Max(1, state.Speed), false);
                }

                await PublishAsync(matchId, resumed.Events.Select(engineEvent => engineEvent.ToDto()).ToList(), cancellationToken);
                await PublishStateAndScoreAsync(matchId, matchService, cancellationToken);
                return (Math.Max(1, state.Speed), false);
            }

            var result = await matchService.TickAsync(matchId, cancellationToken);

            if (result.Accepted && result.Events.Count > 0)
            {
                var events = result.Events.Select(engineEvent => engineEvent.ToDto()).ToList();
                await _broadcaster.PublishEventsAsync(matchId, events, cancellationToken);
                await PublishToTheMatchdayAsync(matchId, events, cancellationToken);
            }

            await PublishStateAndScoreAsync(matchId, matchService, cancellationToken);
            return (Math.Max(1, state.Speed), false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (1, false);
        }
        catch (EntityNotFoundException exception)
        {
            // The match this session was playing is not in the database. It is not coming
            // back — a match row that has been removed is not a match that is merely slow to
            // load — so the session is dropped rather than left to fail again on the next
            // pass. Keeping it would turn one deleted row into the same error once a second
            // for the rest of the process's life, which is how a real problem gets lost in
            // noise that never stops.
            _sessions.Remove(matchId);

            _logger.LogWarning(
                exception,
                "Dropped a live session for match {MatchId}: the match is no longer in the database",
                matchId);

            return (1, false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to advance match {MatchId}", matchId);
            return (1, false);
        }
    }

    /// <summary>
    /// Advances a match that is at the spot, and says whether it is still there afterwards.
    ///
    /// A shootout is a kick a tick, and a manager who has not named his order is holding it:
    /// the loop ticks nothing until he does, exactly as it holds the clock at the interval
    /// and for the taker of a penalty. What it does not do is decide who takes — that is the
    /// engine's for the club nobody is watching and the manager's for his own.
    /// </summary>
    private async Task<bool> AdvanceShootoutAsync(
        Guid matchId,
        MatchService matchService,
        Application.Models.MatchStateView state,
        CancellationToken cancellationToken)
    {
        if (state.Shootout!.AwaitingOrder)
        {
            // The manager is standing at the spot deciding who goes first. The match waits.
            return true;
        }

        var result = await matchService.TickAsync(matchId, cancellationToken);

        if (result.Accepted && result.Events.Count > 0)
        {
            var events = result.Events.Select(engineEvent => engineEvent.ToDto()).ToList();
            await _broadcaster.PublishEventsAsync(matchId, events, cancellationToken);
            await PublishToTheMatchdayAsync(matchId, events, cancellationToken);
        }

        await PublishStateAndScoreAsync(matchId, matchService, cancellationToken);

        // Still at the spot, or the last kick has been taken and the tie is decided. The
        // caller paces the loop by the answer, and a match that has just been decided has no
        // session left to pace.
        return result.Events.All(engineEvent =>
            engineEvent.Type != Domain.Matches.MatchEventType.MatchFinished);
    }

    /// <summary>
    /// True while the match the manager is watching has not left the first half yet. The
    /// interval is a barrier for the whole matchday, but it has a single owner: a round
    /// with nobody watching has no break to wait for, and if the watched match ends the
    /// others are free to finish on their own. Only the watched match is asked, never a
    /// peer, so two headless matches can never wait for each other.
    /// </summary>
    /// <summary>
    /// True while the match the manager is watching has not left the first half yet.
    /// </summary>
    ///
    /// The question is asked of the whole matchday and not of one round, because a matchday is
    /// the thing that plays together: the first division and the third are in different rounds
    /// and on the same afternoon, and a barrier that only looked inside a round would let the
    /// third division run to full time while the manager was still at his own interval. There
    /// is one watched match and it is found by that — a session nobody auto-continues is the
    /// manager's — so a headless match never waits on a peer and two headless matches can
    /// never wait on each other.
    private bool TheManagerIsStillAtHalfTime(Guid matchId)
    {
        foreach (var otherId in _sessions.ActiveMatchIds)
        {
            if (otherId == matchId)
            {
                continue;
            }

            if (!_sessions.TryGet(otherId, out var other)
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
