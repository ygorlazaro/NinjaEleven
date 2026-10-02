using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Plays one match from kick-off to the final whistle, with nobody watching.
///
/// <para>
/// It is the same <see cref="MatchService"/> the manager's own match goes through, tick for
/// tick and command for command. That is the whole point of it existing: a match the world
/// played on its own has to be the same match, with the same events, the same statistics and
/// the same ledger as a match a person watched, or a manager who opens one of them is reading
/// about a different game.
/// </para>
///
/// <para>
/// The interval is left on its own, and so is the spot, because there is nobody to press the
/// button. A match that is waiting for a named penalty taker is <b>not</b> ticked: the
/// engine picks the taker of a match nobody is managing, so a state that is waiting for a
/// manager is a state this path should never be in, and one that is would be a clock that
/// moves over a decision nobody made.
/// </para>
/// </summary>
public sealed class HeadlessMatchPlayer : IHeadlessMatchPlayer
{
    /// <summary>
    /// The engine needs about a hundred ticks to play a match from whistle to whistle. The
    /// guard is not a rule of football; it exists so a state that never reaches full time
    /// cannot spin for ever and leave a window claimed.
    /// </summary>
    private const int MaxTicksPerMatch = 400;

    private readonly IServiceScopeFactory _scopes;
    private readonly IMatchSessionRegistry _sessions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HeadlessMatchPlayer> _logger;

    public HeadlessMatchPlayer(
        IServiceScopeFactory scopes,
        IMatchSessionRegistry sessions,
        ILoggerFactory loggerFactory,
        ILogger<HeadlessMatchPlayer> logger)
    {
        _scopes = scopes;
        _sessions = sessions;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<HeadlessMatchResult> PlayAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default)
    {
        // One scope for the whole match, and one more for the kick-off. They are not the same
        // scope on purpose: the kick-off is refused more often than anything else here — a
        // match somebody else is playing, a match already over — and a refusal should not
        // leave thirty-two fixtures' worth of change tracker behind it.
        using var kickOffScope = _scopes.CreateScope();
        var started = await kickOffScope.ServiceProvider
            .GetRequiredService<MatchService>()
            .StartAsync(fixtureId, headless: true, cancellationToken: cancellationToken);

        if (!started.Accepted)
        {
            _logger.LogInformation(
                "Fixture {FixtureId} was not kicked off: {Reason} {Message}",
                fixtureId,
                started.Reason,
                started.ErrorMessage);

            return new HeadlessMatchResult(
                false,
                started.MatchId,
                started.Reason,
                started.ErrorMessage);
        }

        // The walk's claim on the clock, before the first tick and for as long as it walks.
        // The loop walks every live session once a second, so without this the two of them
        // would move the same match at the same time — a match at double speed, and a second
        // half asked of a match that never stopped.
        Claim(started.MatchId, MatchDriver.WalkedByTheWorld);

        // The match plays in a scope of its own, so the events of this fixture are committed
        // and released before the next one starts reading anything.
        using var scope = _scopes.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
        var matchLog = _loggerFactory.CreateLogger<HeadlessMatchPlayer>();

        // The match is played to full time or given up on. Both of those are the caller's
        // business and neither of them may leave a match on the pitch: a match this process
        // started and then stopped driving stays on the registry and the next run of the
        // window is told it is already being played — for ever, by nothing. So the outcome of
        // the walk is asked for first, and a match that did not finish is closed here and its
        // fixture reopened, which is the difference between a window that is retried and a
        // window that never closes.
        //
        // The walk itself is inside the try, because an exception is the same outcome as a
        // walk that gave up: the match is on the pitch, this process is not driving it, and
        // leaving it there is the failure either way. Abandoning is what puts the fixture
        // back on the schedule, and a fixture that is on the schedule is one the next run
        // will play.
        var walk = WalkOutcome.GaveUp;

        try
        {
            walk = await PlayToFullTimeAsync(
                matchService,
                _sessions,
                started.MatchId,
                matchLog,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            matchLog.LogError(
                exception,
                "Match {MatchId} could not be played to the end and is being given up on.",
                started.MatchId);
        }

        // Somebody else's match now. It is not abandoned — abandoning it would take the
        // football away from whoever claimed it — and the fixture is left owed, which is what
        // the next run of the window reconciles from the match that finishes under the other
        // driver's hand.
        if (walk is WalkOutcome.HandedOver)
        {
            return new HeadlessMatchResult(
                false,
                started.MatchId,
                MatchRefusal.AlreadyRunningHere,
                "A partida foi assumida por outro condutor e será concluída por ele.");
        }

        if (walk is not WalkOutcome.ReachedFullTime)
        {
            await matchService.AbandonAsync(started.MatchId, cancellationToken);

            _logger.LogWarning(
                "Fixture {FixtureId} was given up on: match {MatchId} did not reach full time. " +
                "It is abandoned and the fixture is playable again.",
                fixtureId,
                started.MatchId);

            return new HeadlessMatchResult(
                false,
                started.MatchId,
                MatchRefusal.LostTheKickOff,
                "A partida não chegou ao fim do tempo e foi abandonada.");
        }

        GiveBack(started.MatchId);

        var result = await matchService.GetResultAsync(started.MatchId, cancellationToken);

        _logger.LogInformation(
            "Match finished. Fixture {FixtureId}, match {MatchId}, {Home} x {Away}.",
            fixtureId,
            started.MatchId,
            result.HomeScore,
            result.AwayScore);

        return new HeadlessMatchResult(
            true,
            started.MatchId,
            MatchRefusal.None,
            null,
            result.HomeScore,
            result.AwayScore);
    }

    /// <summary>
    /// Starts the manager's own match and leaves it to the loop.
    ///
    /// <para>
    /// The kick-off is the same kick-off a match nobody watches gets — same lineup, same
    /// bench, same engine, same opening events — and then this walk stops. The session is left
    /// with the loop as its driver and nobody's claim on it, so the background loop plays it
    /// out in the time a match takes and the window closes on the final whistle, whether the
    /// manager is at his screen or asleep. That is the whole difference between his own game
    /// and the other thirty-one: it is played live rather than walked through in one go, and
    /// he can take the keyboard whenever he arrives.
    /// </para>
    ///
    /// <para>
    /// There is no third possibility. A session left with no driver at all is a match on a
    /// scoreboard at 0 x 0 for ever: the loop keeps off a match it does not own, no walk comes
    /// back for it, the fixture holds its window and the season behind it stops. Every process
    /// that opens a match therefore runs the loop that finishes it, which is why this is a
    /// method that says <c>let the loop run</c> rather than one that says <c>leave it</c>.
    /// </para>
    /// </summary>
    /// <param name="fixtureId">The fixture of the manager's own club.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The kick-off's own answer.</returns>
    public async Task<HeadlessMatchResult> StartAndLetTheLoopRunAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default)
    {
        using var kickOffScope = _scopes.CreateScope();
        var started = await kickOffScope.ServiceProvider
            .GetRequiredService<MatchService>()
            .StartAsync(fixtureId, headless: true, cancellationToken: cancellationToken);

        if (!started.Accepted)
        {
            _logger.LogInformation(
                "Fixture {FixtureId} was not kicked off: {Reason} {Message}",
                fixtureId,
                started.Reason,
                started.ErrorMessage);

            return new HeadlessMatchResult(
                false,
                started.MatchId,
                started.Reason,
                started.ErrorMessage);
        }

        _logger.LogInformation(
            "Match {MatchId} is the manager's own and was started for him. It is being played " +
            "live by this process's loop, and his screen claims it if he arrives.",
            started.MatchId);

        return new HeadlessMatchResult(
            true,
            started.MatchId,
            MatchRefusal.None,
            null);
    }

    /// <summary>Says who is moving this match's clock.</summary>
    private void Claim(Guid matchId, MatchDriver driver)
    {
        if (_sessions.TryGet(matchId, out var session))
        {
            session.Driver = driver;
        }
    }

    /// <summary>
    /// Hands the clock back when the walk is done, so a match the loop may drive — one a
    /// manager has since claimed, or one that outlived its walk — is not held by a walk that
    /// has already stopped.
    /// </summary>
    private void GiveBack(Guid matchId)
    {
        if (_sessions.TryGet(matchId, out var session))
        {
            session.Driver = MatchDriver.None;
        }
    }

    /// <summary>
    /// Advances one match until the final whistle, and says whether it got there.
    ///
    /// Every step asks the state first and only then decides what the next beat is: a tick, the
    /// interval, or somebody else taking the match over. That ordering is the whole of it — a
    /// headless match has nobody to press "second half", so the interval is passed on its own,
    /// a match that has finished is noticed before it is ticked, and a match a manager has laid
    /// claim to is left alone rather than played twice as fast.
    /// </summary>
    /// <param name="matchService">The service that moves this match's clock.</param>
    /// <param name="sessions">The registry, which says whether this match is still the world's.</param>
    /// <param name="matchId">The match being walked.</param>
    /// <param name="logger">Where the walk says what it did.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How the walk ended.</returns>
    private static async Task<WalkOutcome> PlayToFullTimeAsync(
        MatchService matchService,
        IMatchSessionRegistry sessions,
        Guid matchId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var tick = 0; tick < MaxTicksPerMatch; tick++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Somebody else is driving this one. A manager who lays claim to a match the
            // world is playing takes it over — the claim flag is what says so — and from that
            // moment the clock is his. Two drivers on one match is a match played at double
            // speed and then asked for a second half it does not have, so the walk stops
            // here and leaves the fixture owed: the next run of the window reconciles it from
            // the match that finishes under the manager's own hand.
            if (sessions.TryGet(matchId, out var session)
                && (!session.AutoContinue || session.Driver is not MatchDriver.WalkedByTheWorld))
            {
                logger.LogInformation(
                    "Match {MatchId} was laid claim to while the world was playing it. " +
                    "It is left to whoever claimed it.",
                    matchId);

                return WalkOutcome.HandedOver;
            }

            var state = await matchService.GetStateAsync(matchId, cancellationToken);

            if (state.IsFinished)
            {
                return WalkOutcome.ReachedFullTime;
            }

            if (state.IsHalfTime)
            {
                var resumed = await matchService.ContinueSecondHalfAsync(matchId, cancellationToken);
                if (!resumed.Accepted)
                {
                    logger.LogWarning(
                        "Match {MatchId} would not leave the interval: {Reason}",
                        matchId,
                        resumed.ErrorMessage);
                    return WalkOutcome.GaveUp;
                }

                continue;
            }

            // A tie going to penalties is a kick a tick, and the engine kicks it: it names its
            // own order for a match nobody is managing, exactly as it names its own taker.
            var atTheSpot = state.Shootout is not null;
            var kicked = await matchService.TickAsync(matchId, cancellationToken);

            if (!kicked.Accepted)
            {
                // A match waiting for a manager's decision is a match this path does not own.
                // Stopping here leaves the match where it is rather than moving its clock over
                // a decision nobody has made; the caller abandons it and the window is retried.
                logger.LogWarning(
                    "Match {MatchId} stopped being advanced{AtTheSpot}: {Reason}",
                    matchId,
                    atTheSpot ? " at the spot" : string.Empty,
                    kicked.ErrorMessage);
                return WalkOutcome.GaveUp;
            }
        }

        logger.LogWarning("Match {MatchId} did not reach full time while it was being played.", matchId);
        return WalkOutcome.GaveUp;
    }

    /// <summary>
    /// How a walk of one match ended. They are three because they are three different things
    /// to the caller: football that happened, football somebody else is playing now, and a
    /// match that has to be closed and given back.
    /// </summary>
    private enum WalkOutcome
    {
        /// <summary>The match reached the final whistle under this walk.</summary>
        ReachedFullTime = 0,

        /// <summary>A manager laid claim to it, and the clock is his from here.</summary>
        HandedOver = 1,

        /// <summary>It did not get there, and this process is not driving it any more.</summary>
        GaveUp = 2
    }
}
