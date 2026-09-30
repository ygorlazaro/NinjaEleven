using Microsoft.Extensions.DependencyInjection;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Realtime;

/// <summary>
/// Plays the matches nobody is watching.
///
/// <para>
/// This used to be where the world moved itself: it started a whole matchday when a manager
/// started his own match, and it had a loop of its own for finishing a round by hand. Both
/// of those now live in the Application layer, in
/// <see cref="CompetitionExecutionService"/> and <see cref="IHeadlessMatchPlayer"/>, and
/// this is the thin shell the controllers still call. That is the whole point of the
/// scheduler arriving: there is one way to play a match without a manager, and the API, the
/// Scheduler and a person at a terminal all go through it, so a headless match produces the
/// same events, the same statistics and the same books whichever door it came in by.
/// </para>
///
/// <para>
/// What is left here is the part that is genuinely about the API: starting a manager's own
/// match and letting the rest of the day start around it.
/// </para>
/// </summary>
public sealed class MatchSimulator
{
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
    /// Plays one fixture from kick-off to full time, through the same service the Scheduler
    /// uses. The fixture must still be scheduled: a match that is being played or already
    /// finished is not touched.
    /// </summary>
    public async Task<Guid?> SimulateFixtureAsync(Guid fixtureId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var player = scope.ServiceProvider.GetRequiredService<IHeadlessMatchPlayer>();

        var played = await player.PlayAsync(fixtureId, cancellationToken);

        return played.Started ? played.MatchId : null;
    }

    /// <summary>
    /// Resolves every fixture of a round that is still scheduled, which is what makes
    /// the round end and the next one the one being played.
    ///
    /// It goes through the world service rather than walking the fixtures itself: the claim
    /// that stops two processes playing one round is the same claim whether the round was
    /// asked for by a scheduled job or by a developer who does not want to wait for five
    /// o'clock.
    /// </summary>
    public async Task<RoundSimulationDto> SimulateRoundAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var execution = scope.ServiceProvider.GetRequiredService<CompetitionExecutionService>();

        var run = await execution.PlayRoundAsync(roundId, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Round {RoundId} simulated by hand: {Played} played, {Already} were already played, {Failed} failed.",
            roundId,
            run.Played,
            run.AlreadyPlayed,
            run.Failed);

        return new RoundSimulationDto
        {
            RoundId = roundId,
            PlayedMatchIds = run.Fixtures
                .Where(fixture => fixture.Status is FixtureRunStatus.Finished && fixture.MatchId is not null)
                .Select(fixture => fixture.MatchId!.Value)
                .ToList()
        };
    }
}
