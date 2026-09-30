using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Moves the world. This is the service the Scheduler asks when the calendar says something
/// is due, and it is also the service a person asks when they want a round played by hand.
///
/// <para>
/// It knows three things and nothing else: what the calendar says is due, which fixtures of
/// that window are still to be played, and how to ask the match service to play one. It does
/// not know what a goal is, how a round robin is drawn or when the artilharia is paid — those
/// belong to the engine, to the rules and to the services that already do them. A scheduler
/// job that called a match service directly would be one rule away from being a second
/// opinion about football, and the whole point of the split is that it is not.
/// </para>
///
/// <para>
/// <b>The window moves first and the fixtures move second.</b> A window of a matchday is
/// claimed before anything in it is played, so two processes that fire in the same minute
/// cannot both play it, and the claim is taken in the database rather than in this process —
/// see <see cref="IRoundExecutionStore"/>. The claim carries a lease, so a process that dies
/// holding a window does not keep it for ever.
/// </para>
///
/// <para>
/// <b>A failure is kept.</b> Every fixture is played in its own unit of work, and one that
/// throws is reported and stepped over: the sixteen that were played before it keep their
/// results, and the window is left uncompleted so the next run picks up the fixtures that are
/// left rather than starting the window again. The fixture that failed is finished, not
/// retried in a loop — a window that cannot be played is a window a person has to look at.
/// </para>
/// </summary>
public class CompetitionExecutionService
{
    private readonly IClock _clock;
    private readonly IMatchHost _host;
    private readonly IMatchDayRepository _matchDays;
    private readonly ICompetitionRepository _competitions;
    private readonly IRoundRepository _rounds;
    private readonly IFixtureRepository _fixtures;
    private readonly IMatchRepository _matches;
    private readonly ISeasonRepository _seasons;
    private readonly IRoundExecutionStore _claims;
    private readonly IHeadlessMatchPlayer _player;
    private readonly IMatchCleaner _cleaner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISeasonCloser _seasonCloser;
    private readonly IManagedClubReader _managedClubs;
    private readonly ISeasonCalendarBuilder _calendar;
    private readonly IOptions<WorldExecutionOptions> _options;
    private readonly ILogger<CompetitionExecutionService> _logger;

    public CompetitionExecutionService(
        IClock clock,
        IMatchHost host,
        IMatchDayRepository matchDays,
        ICompetitionRepository competitions,
        IRoundRepository rounds,
        IFixtureRepository fixtures,
        IMatchRepository matches,
        ISeasonRepository seasons,
        IRoundExecutionStore claims,
        IHeadlessMatchPlayer player,
        IMatchCleaner cleaner,
        IUnitOfWork unitOfWork,
        ISeasonCloser seasonCloser,
        IManagedClubReader managedClubs,
        ISeasonCalendarBuilder calendar,
        IOptions<WorldExecutionOptions> options,
        ILogger<CompetitionExecutionService> logger)    {
        _clock = clock;
        _host = host;
        _matchDays = matchDays;
        _competitions = competitions;
        _rounds = rounds;
        _fixtures = fixtures;
        _matches = matches;
        _seasons = seasons;
        _claims = claims;
        _managedClubs = managedClubs;
        _calendar = calendar;
        _player = player;
        _cleaner = cleaner;
        _unitOfWork = unitOfWork;
        _seasonCloser = seasonCloser;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// The windows of one kind of competition that the calendar says are due and have not
    /// been played.
    ///
    /// It is asked of the calendar rather than of a clock in this process: a day is due
    /// because of its own date and the hour its competition goes out, and both of those are
    /// rules of the world. That is why a matchday nobody started is picked up rather than
    /// skipped — a Scheduler that was down over a weekend comes back and owes two matchdays.
    /// </summary>
    /// <param name="wave">Which kind of window to look for: the championship, the cup or the Supercup.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<DueRound>> GetDueRoundsAsync(
        CompetitionType wave,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var options = _options.Value;

        // From the beginning of the calendar up to today, and not from yesterday. A window
        // that went out four hours ago and was missed has to be findable, and so does one that
        // went out two months ago: the world was down, and a world that is owed a matchday is
        // a world that plays it when it comes back. Reading the whole calendar costs a range
        // scan of a few dozen rows, and it is filtered down to the windows that still have
        // something left in them before a single fixture is touched.
        var matchDays = await _matchDays.ListUntilAsync(
            DateOnly.FromDateTime(now.UtcDateTime),
            cancellationToken);

        if (matchDays.Count == 0)
        {
            return Array.Empty<DueRound>();
        }

        var views = await _competitions.ListSeasonViewsAsync(
            matchDays.Select(day => day.SeasonId).Distinct().ToList(),
            cancellationToken);

        // One dictionary of every edition of every season on the calendar, so the wave a
        // window belongs to is read rather than looked up a round at a time.
        var waveByEdition = views
            .SelectMany(seasonViews => seasonViews.Value)
            .Where(view => view.Type == wave)
            .ToDictionary(view => view.Id, view => view.SeasonId);

        if (waveByEdition.Count == 0)
        {
            return Array.Empty<DueRound>();
        }

        var rounds = await _rounds.ListByCompetitionSeasonIdsAsync(
            waveByEdition.Keys.ToList(),
            cancellationToken);

        var dayById = matchDays.ToDictionary(day => day.Id);

        // A window that is being played right now is not offered, because somebody is playing
        // it. A window that says it has been played *is* offered, because the two columns that
        // say so are a record of it having been closed and not the fact of the fixtures having
        // been played: a window closed over a fixture whose match was given up on would
        // otherwise never be offered again, and a matchday the world will not offer again is a
        // matchday that is simply gone. The fixtures have the last word on this, and they say
        // it at the end of this walk.
        var candidates = rounds
            .Where(round =>
                round.MatchDayId is not null
                && dayById.ContainsKey(round.MatchDayId.Value)
                && !IsBeingPlayedRightNow(round, now, options.RoundClaimLease)
                && dayById[round.MatchDayId.Value].IsDue(
                    dayById[round.MatchDayId.Value].KickOffAt(wave), now, options.DueTolerance))
            .OrderBy(round => dayById[round.MatchDayId!.Value].Number)
            .ThenBy(round => round.Number)
            .ToList();

        if (candidates.Count == 0)
        {
            return Array.Empty<DueRound>();
        }

        // A window with nothing left in it is a window the world has already played, and one
        // with something left in it is a window the world still owes — whatever its own row
        // says. Asking about the fixtures is what makes the answer that rather than a guess
        // from the window's status, which is what a window closed by a person pressing a
        // button, or a window closed over a hole, has got.
        var fixtures = await _fixtures.ListByRoundIdsAsync(
            candidates.Select(round => round.Id).ToList(),
            cancellationToken);

        return candidates
            .Where(round => fixtures
                .Where(fixture => fixture.RoundId == round.Id)
                .Any(fixture => fixture.Status is not FixtureStatus.Finished))
            .Select(round =>
            {
                var day = dayById[round.MatchDayId!.Value];
                return new DueRound(
                    day.Id, day.Number, round.Id, round.Number, wave, day.KickOffAt(wave));
            })
            .ToList();
    }

    /// <summary>
    /// Whether a window is in the middle of being played by somebody, right now.
    ///
    /// A claim that has gone stale is not somebody playing it any more — that process is gone
    /// and the window is waiting to be taken over — so it is offered like any other.
    /// </summary>
    private static bool IsBeingPlayedRightNow(Round round, DateTimeOffset now, TimeSpan lease) =>
        round.ExecutionStatus is RoundExecutionStatus.Running
        && !round.HasTheClaimExpired(now, lease);

    /// <summary>
    /// Closes the matches that are still on the pitch of fixtures the world has already
    /// decided, and says so when there were any.
    ///
    /// A live match holds its fixture — the database refuses a second one — so an orphan is
    /// not a row nobody looks at: it is a fixture the world can never play again, held by a
    /// match that is never ticked, and no walk asks about a fixture it has already finished
    /// with. The world does not skip a matchday over that; it owes it for ever.
    /// </summary>
    private async Task ReleaseTheFixturesThatMatchesAreHoldingAsync(CancellationToken cancellationToken)
    {
        var released = await _cleaner.AbandonMatchesOnDecidedFixturesAsync(cancellationToken);

        if (released > 0)
        {
            _logger.LogWarning(
                "{Count} match(es) were still running on fixtures the world had already decided " +
                "and have been abandoned.",
                released);
        }
    }

    /// <summary>
    /// Plays every window of one kind of competition that the calendar says is due.
    ///
    /// This is what a scheduled job calls, and it is the whole of what a scheduled job is: a
    /// question to the calendar and a walk over the answer.
    /// </summary>
    public async Task<IReadOnlyList<RoundRun>> PlayDueRoundsAsync(
        CompetitionType wave,
        Guid? managerTeamId = null,
        CancellationToken cancellationToken = default)
    {
        // The sweep runs on every poll and not only on one that has a window to walk, because a
        // match left on the pitch of a decided fixture holds a fixture for ever and the season
        // that produced it may be over: a world whose last matchday is played and whose
        // orphans are still on the pitch has nothing left to walk, so a sweep that ran only
        // inside a walk would never run again.
        await ReleaseTheFixturesThatMatchesAreHoldingAsync(cancellationToken);

        var due = await GetDueRoundsAsync(wave, cancellationToken);

        if (due.Count == 0)
        {
            _logger.LogDebug("No {Wave} window is due. The world is where the calendar says it is.", wave);
            return Array.Empty<RoundRun>();
        }

        _logger.LogInformation(
            "{Count} {Wave} window(s) are due. The oldest goes out at {KickOff:o}.",
            due.Count,
            wave,
            due[0].KickOffAt);

        var runs = new List<RoundRun>(due.Count);

        foreach (var round in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runs.Add(await PlayRoundAsync(round.RoundId, managerTeamId, cancellationToken));
        }

        return runs;
    }

    /// <summary>
    /// Moves the world one step by hand: the next window of its calendar, now, whatever the date
    /// says, and the close of the season when that window was the last one in it.
    ///
    /// <para>
    /// This is the door a person opens when they are watching a season rather than living in
    /// one. The Scheduler asks the calendar what is due and is told nothing until the hour
    /// arrives; here the calendar is walked in the order it is drawn and one window at a time,
    /// so a developer can watch the first round of a season, then the second, then the day the
    /// cup's 32-avos are drawn into — without a season lasting thirty-four days to get to the
    /// part they are looking at.
    /// </para>
    ///
    /// <para>
    /// It plays the same windows in the same claim, with the same idempotency, as the Scheduler
    /// does, and it asks the calendar about the same thing the Scheduler asks about: which
    /// windows have not been played. The only difference is that the answer is not filtered by
    /// the date, because a hand is not a clock — and that difference is deliberately the only
    /// one. A window that is due and a window that a person asked for are the same window, and a
    /// person cannot make the world play a fixture that is not on its calendar.
    /// </para>
    ///
    /// <para>
    /// A day is one step and a day with a cup leg in it is two, because its football is six
    /// hours apart: the championship's window is played before the cup's, whichever of them the
    /// day holds. The first step of a first season is day one with no football in it, because a
    /// Supercup needs a season before it and there is none.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>What the step played, or that there was nothing left to play.</returns>
    public async Task<WorldAdvance> AdvanceTheWorldAsync(
        Guid? managerTeamId = null,
        CancellationToken cancellationToken = default)
    {
        await ReleaseTheFixturesThatMatchesAreHoldingAsync(cancellationToken);

        var season = await TheNewestSeasonAsync(cancellationToken);

        if (season is null)
        {
            _logger.LogWarning("There is no season to advance: the world has not been written yet.");
            return WorldAdvance.Nothing();
        }

        // A season that has never been drawn owes its calendar, not its matchdays, and a hand
        // walking the world should not have to know that: the calendar is the shape of the
        // season and the walk is what plays it. Without this a world answered "nothing to do"
        // about a season that had thirty-four days of football in it — and the season a close
        // opens was the worst case of it, because the close draws the next pyramid and leaves
        // the calendar of the new season to whoever noticed. The Supercup lives on that first
        // day's calendar, so a world that never drew it had no Supercup at all.
        await _calendar.DrawAsync(season.Id, cancellationToken);

        var next = await NextWindowOfTheSeasonAsync(season, cancellationToken);

        if (next is null)
        {
            return await CloseTheSeasonIfItIsOverAsync(season, cancellationToken);
        }

        var (day, wave, rounds) = next.Value;
        var runs = new List<RoundRun>(rounds.Count);

        foreach (var round in rounds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runs.Add(await PlayRoundAsync(round.Id, managerTeamId, cancellationToken));
        }

        _logger.LogInformation(
            "Advanced by hand: day {Number} ({Date}), the {Wave} window, {Rounds} window(s) played.",
            day.Number,
            day.Date,
            wave,
            runs.Count);

        var closed = await CloseTheSeasonIfItIsOverAsync(season, cancellationToken);

        return new WorldAdvance(
            closed.SeasonClosed ? WorldAdvanceKind.SeasonClosed : WorldAdvanceKind.Window,
            season.Id,
            season.Name,
            day.Number,
            day.Date,
            wave,
            runs,
            closed.SeasonClosed,
            closed.SeasonOpened);
    }

    /// <summary>
    /// The season the world is in: the newest one there is.
    ///
    /// It is the newest rather than the one whose dates contain today because a hand is not a
    /// clock here either: a world whose season was opened by hand has a season whose days are
    /// wherever the season before it left them, and asking for the season whose calendar
    /// contains today would answer "none" for a season that is waiting to be played.
    /// </summary>
    private async Task<Season?> TheNewestSeasonAsync(CancellationToken cancellationToken) =>
        (await _seasons.ListAsync(cancellationToken))
            .OrderByDescending(season => season.Number)
            .FirstOrDefault();

    /// <summary>
    /// The next window of the season that has not been played: its day, the wave of that day and
    /// every division's window in it.
    ///
    /// <para>
    /// The whole day is one window and not one round per division, because that is what a
    /// matchday is: every division plays in the same wave and a manager must never see one
    /// division a day ahead of another. The day is read first and the wave inside it second, in
    /// the order the rules play them — the championship, then the cup — and a window with
    /// nothing left in it is not offered whatever its own row says, for the same reason the
    /// due walk does not offer it.
    /// </para>
    /// </summary>
    private async Task<(MatchDay Day, CompetitionType Wave, List<Round> Rounds)?> NextWindowOfTheSeasonAsync(
        Season season,
        CancellationToken cancellationToken)
    {
        var days = await _matchDays.ListBySeasonAsync(season.Id, cancellationToken);

        if (days.Count == 0)
        {
            return null;
        }

        var dayById = days.ToDictionary(day => day.Id);
        var views = await _competitions.ListSeasonViewsAsync(season.Id, cancellationToken);
        var waveByEdition = views.ToDictionary(view => view.Id, view => view.Type);

        if (waveByEdition.Count == 0)
        {
            return null;
        }

        var rounds = await _rounds.ListByCompetitionSeasonIdsAsync(
            waveByEdition.Keys.ToList(),
            cancellationToken);

        var pending = rounds
            .Where(round =>
                round.MatchDayId is not null
                && dayById.ContainsKey(round.MatchDayId.Value)
                && !round.HasBeenExecuted
                && waveByEdition.ContainsKey(round.CompetitionSeasonId))
            .ToList();

        if (pending.Count == 0)
        {
            return null;
        }

        var fixtures = await _fixtures.ListByRoundIdsAsync(
            pending.Select(round => round.Id).ToList(),
            cancellationToken);

        var owed = pending
            .Where(round => fixtures
                .Where(fixture => fixture.RoundId == round.Id)
                .Any(fixture => fixture.Status is not FixtureStatus.Finished))
            .GroupBy(round => new
            {
                Day = dayById[round.MatchDayId!.Value],
                Wave = waveByEdition[round.CompetitionSeasonId]
            })
            .Select(group => new
            {
                group.Key.Day,
                group.Key.Wave,
                Order = PositionInTheDay(group.Key.Wave),
                Rounds = group
                    .OrderBy(round => round.Number)
                    .ToList()
            })
            .OrderBy(step => step.Day.Number)
            .ThenBy(step => step.Order)
            .FirstOrDefault();

        return owed is null ? null : (owed.Day, owed.Wave, owed.Rounds);
    }

    /// <summary>
    /// Where a window falls in the order its matchday is played in.
    /// </summary>
    private static int PositionInTheDay(CompetitionType wave)
    {
        for (var index = 0; index < CompetitionRules.MatchdayWaves.Count; index++)
        {
            if (CompetitionRules.MatchdayWaves[index] == wave)
            {
                return index;
            }
        }

        return CompetitionRules.MatchdayWaves.Count;
    }

    /// <summary>
    /// Closes the season and opens the next one when its last window has been played, and says so
    /// rather than doing it silently.
    ///
    /// <para>
    /// The last football of a season is the final's second leg, so the call that plays it is the
    /// call that pays the championship purses, writes the trophies, moves the pyramid between
    /// the divisions and draws the calendar of the season after — which is where the Supercup of
    /// its first day comes from. A season that is still owed a fixture says nothing here and is
    /// not closed, because a season closed over a hole is a season whose prizes were paid before
    /// its last matchday was played.
    /// </para>
    /// </summary>
    private async Task<WorldAdvance> CloseTheSeasonIfItIsOverAsync(
        Season season,
        CancellationToken cancellationToken)
    {
        if (season.Status is SeasonStatus.Finished)
        {
            return WorldAdvance.Nothing();
        }

        if (!await _seasonCloser.IsFinishedAsync(season.Id, cancellationToken))
        {
            return WorldAdvance.Nothing();
        }

        var closed = await _seasonCloser.CloseAsync(season.Id, openTheNextSeason: true, cancellationToken);

        _logger.LogInformation(
            "Season {Name} is over: {Prizes} prizes paid, {Champions} champions. Season {Next} opened.",
            season.Name,
            closed.PrizesPaid,
            closed.Champions.Count,
            closed.NextSeasonId);

        return new WorldAdvance(
            WorldAdvanceKind.SeasonClosed,
            season.Id,
            season.Name,
            null,
            null,
            null,
            Array.Empty<RoundRun>(),
            true,
            closed.NextSeasonId);
    }

    /// <summary>
    /// Plays one window of football, from wherever it happens to be.
    ///
    /// The claim is taken first and the window is only completed when every fixture of it is
    /// finished, which is the whole of the idempotency contract:
    ///
    /// - a window already played is refused before anything is read, so a second attempt
    ///   costs one row and no football;
    /// - a window being played by another process is left to it;
    /// - a window this process claimed but a previous run left half played is finished by
    ///   playing what is left, because the fixtures that were played are already
    ///   <see cref="FixtureStatus.Finished"/> and are not touched.
    /// </summary>
    /// <param name="roundId">The window to play.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<RoundRun> PlayRoundAsync(
        Guid roundId,
        Guid? managerTeamId = null,
        CancellationToken cancellationToken = default)
    {
        var managersTeams = await TheClubsWhoseMatchesAreNotMineToPlayAsync(managerTeamId, cancellationToken);

        var startedAt = _clock.UtcNow;
        var options = _options.Value;

        var claim = await _claims.TryClaimAsync(
            roundId,
            options.RoundClaimLease,
            startedAt,
            cancellationToken);

        if (claim is not RoundClaim.Claimed)
        {
            _logger.LogInformation(
                "Round {RoundId} was not taken: {Claim}. Nothing to do.",
                roundId,
                claim);

            return new RoundRun(roundId, claim, Array.Empty<FixtureRun>(), TimeSpan.Zero);
        }

        _logger.LogInformation("Round {RoundId} taken by {Host}.", roundId, _host.HostId);

        try
        {
            // Before a single fixture of this window is touched. A match left running on a
            // fixture the world has already decided holds that fixture — the database refuses
            // a second match for it — and nothing else would ever ask about it, because every
            // walk has finished with a decided fixture. So the hold is released here, where
            // the world is about to need a fixture, rather than by a person noticing a season
            // that stopped.
            await ReleaseTheFixturesThatMatchesAreHoldingAsync(cancellationToken);

            var round = await _rounds.GetAsync(roundId, cancellationToken);
            var fixtures = await _fixtures.ListByRoundAsync(roundId, cancellationToken);

            _logger.LogInformation(
                "Playing round {RoundId} (round {Number}) with {Count} fixture(s).",
                roundId,
                round?.Number,
                fixtures.Count);

            var settled = await ReconcileAsync(fixtures, cancellationToken);
            var runs = await PlayTheFixturesAsync(
                settled,
                options.MaxParallelFixtures,
                managersTeams,
                cancellationToken);
            var report = new RoundRun(roundId, RoundClaim.Claimed, runs, _clock.UtcNow - startedAt);

            if (report.IsComplete)
            {
                var completed = await _claims.TryCompleteAsync(
                    roundId,
                    options.RoundClaimLease,
                    _clock.UtcNow,
                    cancellationToken);

                if (!completed)
                {
                    // The window is owed and nobody may close it: a fixture of it is still on
                    // the schedule, or the claim belongs to somebody else. Either way it is not
                    // a window the world has finished with, and the next run picks it up.
                    _logger.LogWarning(
                        "Round {RoundId} was played out but not written up: a fixture of it is still " +
                        "on the schedule, or the claim is no longer this process's. It stays owed.",
                        roundId);

                    return report;
                }

                _logger.LogInformation(
                    "Round {RoundId} completed: {Played} played, {Already} were already played. {Duration}.",
                    roundId,
                    report.Played,
                    report.AlreadyPlayed,
                    report.Duration);
            }
            else
            {
                // The claim goes back rather than being held: a window this process could not
                // finish is a window the next run has to be allowed to take, and holding it
                // until the lease expired would add half an hour of nothing to every failure.
                await _claims.ReleaseAsync(roundId, cancellationToken);

                _logger.LogWarning(
                    "Round {RoundId} is not complete: {Failed} failed, {Elsewhere} held by another process, " +
                    "out of {Count} fixture(s). It stays open for the next run.",
                    roundId,
                    report.Failed,
                    report.PlayedElsewhere,
                    report.Fixtures.Count);
            }

            return report;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _claims.ReleaseAsync(roundId, CancellationToken.None);
            _logger.LogError(exception, "Failed to play round {RoundId}.", roundId);

            return new RoundRun(
                roundId,
                RoundClaim.Claimed,
                Array.Empty<FixtureRun>(),
                _clock.UtcNow - startedAt);
        }
    }

    /// <summary>
    /// Brings a window's fixtures back in line with the matches under them.
    ///
    /// A window is written in several commits — the match is saved, the books are settled, the
    /// fixture is marked, the window is closed — and a process that dies between any two of
    /// them leaves a fixture that says "in progress" above a match that has been playing for
    /// ninety minutes. The match row is the record of what happened, so the fixture is
    /// brought back to it here. Without this a window is one bad crash away from never
    /// closing, and a window that never closes is a season that stops.
    ///
    /// <para>
    /// Only a match that reached the final whistle decides its fixture. <see cref="Match.IsFinished"/>
    /// is not that question — it is also true of an abandoned match, which is precisely a match
    /// that did <i>not</i> reach full time — and reading it here closed a window over football
    /// that was never played: the fixture was marked finished over an abandoned match, the window
    /// counted itself complete, and the calendar carried on with a matchday that had no match in
    /// it. An abandoned match leaves its fixture owed, because that is the whole difference
    /// between a window that is retried and a window that is closed over a hole.
    /// </para>
    /// </summary>
    /// <param name="fixtures">The window's fixtures, as read.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The same fixtures, with the played ones marked finished.</returns>
    private async Task<IReadOnlyList<Fixture>> ReconcileAsync(
        IReadOnlyList<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        var pending = fixtures
            .Where(fixture => fixture.Status is not FixtureStatus.Finished)
            .Select(fixture => fixture.Id)
            .ToList();

        if (pending.Count == 0)
        {
            return fixtures;
        }

        var played = (await _matches.ListByFixtureIdsAsync(pending, cancellationToken))
            .Where(match => match.Status is MatchStatus.Finished)
            .Select(match => match.FixtureId)
            .ToHashSet();

        if (played.Count == 0)
        {
            return fixtures;
        }

        foreach (var fixture in fixtures.Where(fixture => played.Contains(fixture.Id)))
        {
            fixture.MarkFinished();
            _fixtures.Update(fixture);

            _logger.LogInformation(
                "Fixture {FixtureId} was already decided by its match; it is not played again.",
                fixture.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return fixtures;
    }

    /// <summary>
    /// Plays a window's fixtures, up to <paramref name="parallel"/> at a time.
    ///
    /// A window is a wave rather than a queue: every division plays in the same one, so this
    /// is not a loop for readability, it is a loop for the width of the day. Each fixture
    /// plays in its own unit of work, which is what keeps one that throws from taking the
    /// sixteen that were played before it down with it.
    /// </summary>
    private async Task<IReadOnlyList<FixtureRun>> PlayTheFixturesAsync(
        IReadOnlyList<Fixture> fixtures,
        int parallel,
        IReadOnlyList<Guid> managersTeams,
        CancellationToken cancellationToken)
    {
        if (fixtures.Count == 0)
        {
            return Array.Empty<FixtureRun>();
        }

        var width = Math.Max(1, parallel);
        var results = new FixtureRun[fixtures.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, fixtures.Count),
            new ParallelOptions { MaxDegreeOfParallelism = width, CancellationToken = cancellationToken },
            async (index, _) =>
            {
                var fixture = fixtures[index];
                results[index] = await PlayFixtureAsync(fixture, managersTeams, cancellationToken);
            });

        return results;
    }

    /// <summary>
    /// The clubs whose matches this walk may not play for them.
    ///
    /// <para>
    /// It is asked of the world and not only of the request. A caller may name a club — that
    /// is how a hand on a command line says whose match it is — but a club somebody is
    /// managing is a fact about the world, and the scheduler walking the same season is
    /// walking it for the same man. So the named club is added to the world's own rather than
    /// replacing it, and a world with nobody in it is walked exactly as before.
    /// </para>
    /// </summary>
    /// <param name="namedTeam">A club the caller named, when it named one.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The clubs whose fixtures are started and left rather than simulated.</returns>
    private async Task<IReadOnlyList<Guid>> TheClubsWhoseMatchesAreNotMineToPlayAsync(
        Guid? namedTeam,
        CancellationToken cancellationToken)
    {
        var managed = await _managedClubs.ListManagedClubsAsync(cancellationToken);

        if (namedTeam is null)
        {
            return managed;
        }

        return managed.Contains(namedTeam.Value)
            ? managed
            : managed.Append(namedTeam.Value).ToList();
    }

    /// <summary>
    /// Whether this fixture is one of the manager's own club's. It is the fixture's two clubs
    /// and nothing else: the question is about the football, not about the session, so a
    /// window the world is playing for the whole country still has exactly one fixture that
    /// belongs to the man watching it.
    /// </summary>
    private static bool IsTheManagersFixture(Fixture fixture, IReadOnlyList<Guid> managersTeams) =>
        managersTeams.Contains(fixture.HomeTeamId) || managersTeams.Contains(fixture.AwayTeamId);

    /// <summary>
    /// Plays one fixture from kick-off to the final whistle, or says why it was not played.
    ///
    /// The answers are the whole of the idempotency contract at fixture level. A fixture that
    /// is already finished is never started; a fixture that somebody is already playing is
    /// left to them; a fixture of the manager's own club is started and handed over rather
    /// than simulated; and a fixture that throws is reported and stepped over, keeping
    /// whatever state the failure left it in so the next run of the window comes back to this
    /// one rather than starting the window again.
    /// </summary>
    private async Task<FixtureRun> PlayFixtureAsync(
        Fixture fixture,
        IReadOnlyList<Guid> managersTeams,
        CancellationToken cancellationToken)
    {
        // Already played. A finished fixture is never started again, and this is the line
        // every second attempt and every restart after the first half of a window stops at.
        if (fixture.Status is FixtureStatus.Finished)
        {
            return new FixtureRun(fixture.Id, null, null, null, FixtureRunStatus.AlreadyFinished);
        }

        // Somebody is playing it right now. A window the day opened a moment ago — the cup
        // window, which kicks every fixture of it off at once — is already on the pitch by
        // the time the walk arrives, and the walk used to take it over: the match is abandoned
        // and begun again under whoever is watching. One match, one driver: a match in
        // progress is left to whoever started it, and the window stays owed until it is over.
        //
        // Except a match nobody ever touched. The world leaves the manager's own match on the
        // touchline and waits for him, and a world that waited for ever would say so by
        // playing nothing at all — the same answer, over and over, indistinguishable from a
        // broken route. So a match that is still at minute zero with nobody behind it is
        // nobody's match any more, the world takes the fixture back and plays it.
        if (fixture.Status is FixtureStatus.InProgress)
        {
            if (!await _cleaner.ReleaseTheUntouchedMatchAsync(fixture.Id, cancellationToken))
            {
                _logger.LogInformation(
                    "Fixture {FixtureId} is already being played. It is left to whoever is playing it.",
                    fixture.Id);

                return new FixtureRun(fixture.Id, null, null, null, FixtureRunStatus.PlayedElsewhere);
            }
        }

        // The manager's own match is his to play. The world starts it — a lineup, a bench, an
        // engine and a set of events, exactly as any other — and then stops: nobody ticks it,
        // the loop leaves it alone, and the window waits for the manager to take it. A world
        // that simulates the game its manager is waiting to watch is a game he never played.
        if (managersTeams.Count > 0 && IsTheManagersFixture(fixture, managersTeams))
        {
            var opened = await _player.StartAndLeaveAsync(fixture.Id, cancellationToken);

            _logger.LogInformation(
                "Fixture {FixtureId} is the manager's own. It was started and left for him: match {MatchId}.",
                fixture.Id,
                opened.MatchId);

            return new FixtureRun(
                fixture.Id,
                opened.MatchId == Guid.Empty ? null : opened.MatchId,
                null,
                null,
                FixtureRunStatus.LeftForTheManager);
        }

        try
        {
            var played = await _player.PlayAsync(fixture.Id, cancellationToken);

            if (played.Started)
            {
                return new FixtureRun(
                    fixture.Id,
                    played.MatchId,
                    played.HomeScore,
                    played.AwayScore,
                    FixtureRunStatus.Finished);
            }

            // Somebody else is in the middle of this one. Its working memory is in another
            // process, so this one steps out of the way: the window stays open and the process
            // that owns the match finishes it. Anything else is a refusal the world has to be
            // told about, and the window does not close over it.
            var elsewhere = played.Refusal is MatchRefusal.PlayedByAnotherProcess
                or MatchRefusal.LostTheKickOff
                or MatchRefusal.AlreadyRunningHere;

            return new FixtureRun(
                fixture.Id,
                played.MatchId == Guid.Empty ? null : played.MatchId,
                null,
                null,
                elsewhere ? FixtureRunStatus.PlayedElsewhere : FixtureRunStatus.Failed,
                played.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failed fixture is reported and stepped over. The sixteen that were played
            // before it keep their results, and the window is left uncompleted, so the next run
            // finishes what is left rather than the round being declared played over a hole.
            _logger.LogError(exception, "Failed to execute fixture {FixtureId}.", fixture.Id);

            return new FixtureRun(fixture.Id, null, null, null, FixtureRunStatus.Failed, exception.Message);
        }
    }
}
