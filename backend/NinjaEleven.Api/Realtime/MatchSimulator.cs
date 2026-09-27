using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Realtime;

/// <summary>
/// Plays the matches nobody is watching. The engine is the same one a manager watches
/// and every step goes through <see cref="MatchService"/>, so a headless match produces
/// the same events, the same statistics and the same final row as a live one.
/// It is how the rest of the league keeps playing while the manager is in another
/// match, and how a round is closed so the next one becomes the current one.
/// </summary>
public sealed class MatchSimulator
{
    /// <summary>
    /// The engine needs about a hundred ticks to play a full match. The guard only
    /// exists so a state that never reaches full time cannot spin for ever.
    /// </summary>
    private const int MaxTicksPerMatch = 400;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MatchSimulator> _logger;

    public MatchSimulator(IServiceScopeFactory scopeFactory, ILogger<MatchSimulator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts the match of the manager and, at the same time, every other match of the
    /// matchday — the whole wave, which is every division of the championship at once and
    /// not the other five clubs of one division.
    ///
    /// This is the difference between a matchday and a round, and it is the difference between
    /// a pyramid whose divisions are comparable and a set of three leagues that share a
    /// calendar. Before, a match of the first division played the other five of that
    /// division and left the second and the third divisions where they were, so a manager
    /// reading the table beside his own was reading a table built from a different number of
    /// games.
    ///
    /// Everything is then driven by the one simulation loop, so the matches of a wave run
    /// together and each client sees its own in full and the others on a scoreboard. The next
    /// wave — the cup, on a day that has one — starts on its own when this one is over.
    /// </summary>
    public async Task<Guid?> KickOffMatchdayAsync(
        Guid fixtureId,
        Guid? userTeamId,
        IReadOnlyCollection<Guid>? starterIds,
        IReadOnlyCollection<Guid>? benchIds,
        int? seed = null,
        string? tacticCode = null,
        CancellationToken cancellationToken = default)
    {
        Guid matchId;

        using (var scope = _scopeFactory.CreateScope())
        {
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
            var started = await matchService.StartAsync(
                fixtureId, seed, userTeamId, starterIds, benchIds, tacticCode: tacticCode, cancellationToken: cancellationToken);

            if (!started.Accepted)
            {
                return started.MatchId == Guid.Empty ? null : started.MatchId;
            }

            matchId = started.MatchId;
        }

        await StartTheDayAsync(fixtureId, cancellationToken);
        return matchId;
    }

    /// <summary>
    /// Starts every other fixture of the matchday that is playing now, except the one the
    /// caller has already started.
    /// </summary>
    private async Task StartTheDayAsync(Guid fixtureId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var matchday = scope.ServiceProvider.GetRequiredService<MatchdayService>();
        await matchday.StartTheDayAsync(fixtureId, fixtureId, cancellationToken);
    }

    /// <summary>
    /// Starts every fixture of a round that is still scheduled, except one. Kept for the
    /// round-level calls (finishing a round by hand from the calendar); starting a match from
    /// the app goes through <see cref="KickOffMatchdayAsync"/>, which plays the whole day.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> StartTheRestOfTheRoundAsync(
        Guid roundId,
        Guid exceptFixtureId,
        CancellationToken cancellationToken = default)
    {
        List<Guid> pending;

        using (var scope = _scopeFactory.CreateScope())
        {
            var fixtureService = scope.ServiceProvider.GetRequiredService<FixtureService>();
            var fixtures = await fixtureService.GetByRoundAsync(roundId, cancellationToken);
            pending = fixtures
                .Where(fixture => fixture.Fixture.Status == FixtureStatus.Scheduled && fixture.Fixture.Id != exceptFixtureId)
                .Select(fixture => fixture.Fixture.Id)
                .ToList();
        }

        // All of them start together: the round is a matchday, not a sequence of games.
        var started = await Task.WhenAll(pending.Select(fixtureId => StartHeadlessAsync(fixtureId, cancellationToken)));

        return started.Where(matchId => matchId.HasValue).Select(matchId => matchId!.Value).ToList();
    }

    private async Task<Guid?> StartHeadlessAsync(Guid fixtureId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
            var started = await matchService.StartAsync(fixtureId, headless: true, cancellationToken: cancellationToken);
            return started.Accepted ? started.MatchId : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Failed to start the match of fixture {FixtureId}", fixtureId);
            return null;
        }
    }

    /// <summary>
    /// Plays one fixture from kick-off to full time. The fixture must still be
    /// scheduled: a match that is being played or already finished is not touched.
    /// </summary>
    public async Task<Guid?> SimulateFixtureAsync(Guid fixtureId, CancellationToken cancellationToken = default)
    {
        Guid matchId;

        // Kick-off runs in its own scope, exactly like a match started from the app.
        using (var scope = _scopeFactory.CreateScope())
        {
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
            var started = await matchService.StartAsync(fixtureId, headless: true, cancellationToken: cancellationToken);

            if (!started.Accepted)
            {
                return null;
            }

            matchId = started.MatchId;
        }

        for (var tick = 0; tick < MaxTicksPerMatch; tick++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return matchId;
            }

            // Each tick gets its own scope, the same rule the live loop follows, so a
            // long headless match never grows one giant unit of work.
            using var scope = _scopeFactory.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
            var state = await matchService.GetStateAsync(matchId, cancellationToken);

            if (state.IsFinished)
            {
                return matchId;
            }

            if (state.IsHalfTime)
            {
                // Nobody is watching to press "second half".
                var resumed = await matchService.ContinueSecondHalfAsync(matchId, cancellationToken);
                if (!resumed.Accepted)
                {
                    break;
                }

                continue;
            }

            var result = await matchService.TickAsync(matchId, cancellationToken);
            if (!result.Accepted)
            {
                break;
            }
        }

        _logger.LogWarning("Match {MatchId} did not reach full time while being simulated.", matchId);
        return matchId;
    }

    /// <summary>
    /// Resolves every fixture of a round that is still scheduled, which is what makes
    /// the round end and the next one the one being played.
    /// </summary>
    public async Task<RoundSimulationDto> SimulateRoundAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        List<Guid> scheduled;

        using (var scope = _scopeFactory.CreateScope())
        {
            var fixtureService = scope.ServiceProvider.GetRequiredService<FixtureService>();
            var fixtures = await fixtureService.GetByRoundAsync(roundId, cancellationToken);
            scheduled = fixtures
                .Where(fixture => fixture.Fixture.Status == FixtureStatus.Scheduled)
                .Select(fixture => fixture.Fixture.Id)
                .ToList();
        }

        var played = new List<Guid>();

        foreach (var fixtureId in scheduled)
        {
            var matchId = await SimulateFixtureAsync(fixtureId, cancellationToken);
            if (matchId.HasValue)
            {
                played.Add(matchId.Value);
            }
        }

        return new RoundSimulationDto
        {
            RoundId = roundId,
            PlayedMatchIds = played
        };
    }
}
