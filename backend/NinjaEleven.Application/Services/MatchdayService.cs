using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Application.Services;

/// <summary>
/// A matchday, and which of its windows is playing.
/// </summary>
/// <param name="MatchDayId">The day.</param>
/// <param name="Number">Which day of the season it is, counted from one.</param>
/// <param name="OpenWave">
/// The wave that is playing now, or null when the day is finished. It is the first wave that
/// still has a fixture nobody has played, which is the whole of what "open" means.
/// </param>
/// <param name="Complete">Whether every fixture of every window has been played.</param>
public record MatchdayProgress(Guid MatchDayId, int Number, CompetitionType? OpenWave, bool Complete);

/// <summary>
/// A matchday is a sequence of waves, and this is the service that knows which one is playing.
///
/// **A matchday is the unit of football, not a round.** The bug this exists to fix is that a
/// round of one division was the unit: the manager started his match, the engine played the
/// other five clubs of his division, and the other two divisions and the cup sat there
/// unplayed until somebody went and asked for them. A pyramid where the first division has
/// played nineteen games and the third has played one is not a pyramid, it is three leagues
/// that happen to share a calendar — and a manager reading a table has no way of knowing that
/// the table beside it was built from a different number of games.
///
/// So: starting any fixture of a day starts the day. Every division's round plays in the same
/// wave, all at once, and the cup follows when the day has finished playing its championship.
/// A day is only over when every window of it is over.
///
/// **The waves are in order, and the order is the rule.** The Supercup, then the championship
/// of all divisions, then the cup. Each wave opens when the one before it is complete, which
/// is what makes "the day is finished" a fact rather than an intention: the cup cannot start
/// while a league game of the same day is still running, so a club plays its cup leg tired,
/// and a stronger club that went further in the cup is a stronger club that went further
/// tired, which is the whole reason the cup is worth entering.
/// </summary>
public class MatchdayService
{
    private readonly IMatchDayRepository _matchDayRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ScorerPrizeService _scorerPrizes;
    private readonly TransferService _transfers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MatchdayService> _logger;

    /// <summary>
    /// The seam that starts a fixture, so this service can start a wave without knowing how
    /// a match is kicked off. It is the same starting path the manager's own match takes, and
    /// that is the point: a headless match of another club is a match the engine started
    /// exactly as it would have started one the manager was watching.
    /// </summary>
    public delegate Task<Guid?> StartFixtureAsync(Guid fixtureId, CancellationToken cancellationToken);

    private StartFixtureAsync? _startFixture;

    /// <summary>
    /// Wires the seam. It is set once by the composition root because the starter needs the
    /// match service, which needs this one to decide whether a match may start at all — two
    /// constructors that need each other is a cycle, and a cycle resolved by a property is
    /// better than one resolved by a lazy lookup in a hot path.
    /// </summary>
    public void UseStarter(StartFixtureAsync starter) => _startFixture = starter;

    public MatchdayService(
        IMatchDayRepository matchDayRepository,
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        ICompetitionRepository competitionRepository,
        IMatchRepository matchRepository,
        ScorerPrizeService scorerPrizes,
        TransferService transfers,
        IUnitOfWork unitOfWork,
        ILogger<MatchdayService> logger)
    {
        _matchDayRepository = matchDayRepository;
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _competitionRepository = competitionRepository;
        _matchRepository = matchRepository;
        _scorerPrizes = scorerPrizes;
        _transfers = transfers;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// What is playing on a day, and whether the day is over.
    ///
    /// The open wave is the first wave that is **not over**, which is a definition rather than
    /// a stored flag: a day with a cup window that has not been drawn yet is not a day whose
    /// cup wave is waiting to start, it is a day whose next fixture is in the championship. The
    /// bracket is drawn when the round before it is decided, so a cup window that is not there
    /// yet has no fixtures and simply does not exist for this day.
    ///
    /// **Over, not started.** A wave whose eighteen matches are all in progress is not a wave
    /// the next one may follow, and calling it finished is how a cup window comes to be played
    /// in the middle of a league matchday: the wave before it is still going, so the day is
    /// still in the championship, and the answer to "what is playing" is the championship even
    /// though everything in it has already kicked off. Started is the question "is there
    /// anything left to kick off" and it is a different question — the one a restart leaves
    /// behind, where a day has three matches unstarted in its cup and should be offered them.
    /// </summary>
    public async Task<MatchdayProgress> GetProgressAsync(
        Guid matchDayId,
        CancellationToken cancellationToken = default)
    {
        var matchDay = await _matchDayRepository.GetAsync(matchDayId, cancellationToken)
            ?? throw new EntityNotFoundException("MatchDay", matchDayId);

        var waves = await ReadWavesAsync(matchDayId, cancellationToken);

        CompetitionType? open = null;
        var complete = true;

        foreach (var type in CompetitionRules.MatchdayWaves)
        {
            if (!waves.TryGetValue(type, out var fixtures) || fixtures.Count == 0)
            {
                // A window the day does not have does not hold the day up: the bracket is
                // drawn when the round before it is decided, so a cup window that has not been
                // drawn yet has no fixtures and is not a wave waiting for anything.
                continue;
            }

            if (await IsFinishedAsync(fixtures, cancellationToken))
            {
                continue;
            }

            // The first window that is not over is the one the day is in, and everything after
            // it is waiting. A later wave with fixtures still to play is also what makes the
            // day unfinished, however far along the earlier waves are.
            open ??= type;
            complete = false;
        }

        return new MatchdayProgress(matchDayId, matchDay.Number, open, complete);
    }

    /// <summary>
    /// Whether a fixture's wave is the one playing. A manager may only start a match of the
    /// open wave, and the refusal is a domain answer rather than a silent no-op: a cup leg
    /// asked for while the championship of the same day is still running is a cup leg that
    /// would be played by a side that has not played its legs yet.
    /// </summary>
    public async Task EnsureTheWaveIsOpenAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        var round = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken);
        if (round?.MatchDayId is null)
        {
            // A fixture outside any matchday — a friendly, a replayed fixture whose day has
            // gone. There is no wave to be out of, so it is playable whenever it is asked for.
            return;
        }

        var type = await TypeOfAsync(round, cancellationToken);
        var progress = await GetProgressAsync(round.MatchDayId.Value, cancellationToken);

        if (progress.OpenWave is null || progress.OpenWave == type)
        {
            return;
        }

        throw new DomainValidationException(
            "MatchdayWaveNotOpen",
            $"A janela da {NameOf(type)} do dia {progress.Number} só é jogada depois do " +
            $"campeonato. Hoje o que está em campo é {NameOf(progress.OpenWave.Value)}.");
    }

    /// <summary>
    /// Starts the day: every fixture of the open wave, in the order the waves are played and
    /// every division's round inside a wave together.
    /// </summary>
    /// <param name="fixtureId">A fixture of the day, which is how the caller found the day.</param>
    /// <param name="exceptFixtureId">
    /// The fixture the caller has already started — the manager's own match, or a match the
    /// engine was asked to start twice. It is not started again, and it is not what the day is
    /// waiting on: a fixture that is already in progress counts as played for the purpose of
    /// opening the next wave.
    /// </param>
    /// <returns>The matches that were started by this call.</returns>
    public async Task<IReadOnlyList<Guid>> StartTheDayAsync(
        Guid fixtureId,
        Guid? exceptFixtureId = null,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        var round = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken)
            ?? throw new EntityNotFoundException("Round", fixture.RoundId);

        if (round.MatchDayId is null)
        {
            // No day: this is the caller's own match and nobody else's business.
            return Array.Empty<Guid>();
        }

        var progress = await GetProgressAsync(round.MatchDayId.Value, cancellationToken);
        if (progress.OpenWave is not { } openWave || _startFixture is null)
        {
            return Array.Empty<Guid>();
        }

        var waves = await ReadWavesAsync(round.MatchDayId.Value, cancellationToken);
        if (!waves.TryGetValue(openWave, out var fixtures))
        {
            return Array.Empty<Guid>();
        }

        var started = new List<Guid>();

        foreach (var toStart in fixtures.Where(item => item.Status == FixtureStatus.Scheduled))
        {
            if (toStart.Id == exceptFixtureId)
            {
                continue;
            }

            var matchId = await _startFixture(toStart.Id, cancellationToken);
            if (matchId is not null)
            {
                started.Add(matchId.Value);
            }
        }

        return started;
    }

    /// <summary>
    /// Opens the next wave when the one that just finished is over, and reports whether the
    /// whole day is done.
    ///
    /// It is called after every match that finishes, from the same place that settles the
    /// books, because that is the only moment the world knows a match is over. It is
    /// idempotent: a wave whose fixtures are all played is not started twice, and a day that
    /// is over is over however many times it is asked about.
    /// </summary>
    public async Task<MatchdayProgress> AdvanceAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken);
        if (fixture is null)
        {
            return new MatchdayProgress(Guid.Empty, 0, null, true);
        }

        var round = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken);
        if (round?.MatchDayId is null)
        {
            return new MatchdayProgress(Guid.Empty, 0, null, true);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Before the question of the next window, and not after it: a window is closed by the
        // finish of its last match, and the last match of the day leaves the day complete —
        // so a check that returned as soon as the day was over would return before closing the
        // very window whose last match it was answering for, and the last window of the day
        // would be the one window that stayed open.
        await CloseTheWindowsThatAreOverAsync(round.MatchDayId.Value, cancellationToken);

        var before = await GetProgressAsync(round.MatchDayId.Value, cancellationToken);
        if (before.Complete || _startFixture is null || before.OpenWave is not { } openWave)
        {
            return before;
        }

        var waves = await ReadWavesAsync(round.MatchDayId.Value, cancellationToken);
        if (!waves.TryGetValue(openWave, out var fixtures))
        {
            return before;
        }

        // The open wave is the first one that is not over, so this starts the next window only
        // when the window before it is finished — including the matches of the other two
        // divisions, which is what "the championship of the day" means here.
        //
        // Over, and not started. A match in progress is a match the day has begun, which is the
        // right answer to "is there anything left to kick off" and the wrong answer to "is this
        // window over": the manager's own game ending at twenty minutes must not open the cup
        // window while seventeen other clubs are still on the pitch, because a cup leg taken by
        // a side that has not finished playing is a leg taken by a team that is not there yet.
        // The two questions have different answers, so they are not asked with the same method.
        //
        // A fixture that is already in progress is left alone — the window is open and running
        // — and the state is read from the fixtures rather than kept in a flag, so a day left
        // half played by a restart waits for a person to finish it rather than starting itself
        // twice.
        foreach (var toStart in fixtures.Where(item => item.Status == FixtureStatus.Scheduled))
        {
            await _startFixture(toStart.Id, cancellationToken);
        }

        return await GetProgressAsync(round.MatchDayId.Value, cancellationToken);
    }

    /// <summary>
    /// Closes every window of the day whose every fixture has been played, and is the only
    /// thing in here that writes <see cref="Round.CompletedAt"/>.
    ///
    /// A window is closed on the finish of its last match, for the same reason the cup
    /// advances on the finish of a match rather than on a timer: the round of the first
    /// division is over when its sixth match ends, and not one moment before. It is asked
    /// about here rather than only for the round the finished fixture belongs to, because a
    /// day is three divisions' championship and a window that closed a match after another
    /// is a day whose calendar disagrees with itself.
    ///
    /// Without it a round is never closed at all: a season would play its twenty-two
    /// matchdays and every one of them would still read as open, which is what a window's
    /// <see cref="Round.IsCompleted"/> is asked — by the calendar a manager reads, and by
    /// the recovery that is applied when a window ends.
    /// </summary>
    private async Task CloseTheWindowsThatAreOverAsync(
        Guid matchDayId,
        CancellationToken cancellationToken)
    {
        var rounds = await _roundRepository.ListByMatchDayAsync(matchDayId, cancellationToken);

        if (rounds.Count == 0)
        {
            return;
        }

        var fixtures = await _fixtureRepository.ListByRoundIdsAsync(
            rounds.Select(item => item.Id), cancellationToken);

        await CloseTheOverWindowsAsync(rounds, fixtures, cancellationToken);
    }

    /// <summary>
    /// Closes every window of the whole calendar that is over, and is what a restart asks for.
    ///
    /// A window is normally closed by the finish of its last match, but the finish is an event
    /// and an event that happened while the process was down is not one anybody saw: a season
    /// left running over a weekend comes back with its last days played and their windows open,
    /// which is a calendar that disagrees with the fixtures under it. Nothing about the repair
    /// is special — it is the same question asked once at startup instead of after every goal.
    ///
    /// It closes windows and nothing else. A day whose window was never filled — a cup round
    /// the bracket has not reached — has no fixtures to be over, and is left as it is.
    /// </summary>
    public async Task CloseTheWindowsThatWereLeftOpenAsync(
        CancellationToken cancellationToken = default)
    {
        var rounds = (await _roundRepository.ListAsync(cancellationToken))
            .Where(round => !round.IsCompleted)
            .ToList();

        if (rounds.Count == 0)
        {
            return;
        }

        var fixtures = await _fixtureRepository.ListByRoundIdsAsync(
            rounds.Select(round => round.Id), cancellationToken);

        await CloseTheOverWindowsAsync(rounds, fixtures, cancellationToken);
    }

    /// <summary>
    /// Writes the closing date on each of the windows whose every fixture has been played, and
    /// saves once if it wrote anything.
    /// </summary>
    private async Task CloseTheOverWindowsAsync(
        IReadOnlyCollection<Round> rounds,
        IReadOnlyCollection<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        var closed = false;
        var justClosed = new List<Round>();

        foreach (var window in rounds.Where(item => !item.IsCompleted))
        {
            var own = fixtures.Where(fixture => fixture.RoundId == window.Id).ToList();

            // A window with nothing in it is not a window that was played: a cup round that
            // has not been drawn yet has no fixtures, and closing it would write a date on a
            // tournament that has not started.
            if (own.Count == 0 || own.Any(fixture => fixture.Status is not FixtureStatus.Finished))
            {
                continue;
            }

            window.Complete();
            _roundRepository.Update(window);
            justClosed.Add(window);
            closed = true;
        }

        if (!closed)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PayTheArtilleriasThatWereWonAsync(justClosed, cancellationToken);
        await RunTransfersOnWindowCloseAsync(justClosed, cancellationToken);
    }

    /// <summary>
    /// Runs the NPC transfer market when championship windows that are over include a round
    /// the transfer window recognises as open (eleven and above). NPC business is settled when
    /// the round closes, not on a timer, so a matchday left running over a weekend picks up
    /// exactly the transfers it would have made the moment the day finished in real time.
    ///
    /// Two things happen: proposals are generated for the season after this one (accepted
    /// NPC-to-NPC deals go straight to accepted), and accepted deals arriving this season —
    /// proposed in the season before — are completed (contracts change, fees move).
    /// </summary>
    private async Task RunTransfersOnWindowCloseAsync(
        IReadOnlyCollection<Round> justClosed,
        CancellationToken cancellationToken)
    {
        var seasonRounds = new HashSet<Guid>();

        foreach (var round in justClosed)
        {
            if (!TransferWindowRules.IsOpen(round.Number))
            {
                continue;
            }

            var view = await _competitionRepository.GetSeasonViewByIdAsync(
                round.CompetitionSeasonId, cancellationToken);

            if (view?.Type is CompetitionType.League)
            {
                seasonRounds.Add(view.SeasonId);
            }
        }

        foreach (var seasonId in seasonRounds)
        {
            try
            {
                await _transfers.CalculateNpcTransfersAsync(seasonId, cancellationToken);
                var currentRound = await GetCurrentRoundAsync(seasonId, cancellationToken);
                await _transfers.CompletePendingTransfersAsync(
                    seasonId, currentRound, cancellationToken);
            }
            catch (Exception error)
            {
                _logger.LogError(
                    error,
                    "Could not run the NPC transfer market for season {SeasonId} when windows closed. The market is keyed by season, so asking again is safe.",
                    seasonId);
            }
        }
    }

    /// <summary>
    /// The round of the championship for a season, which is what the transfer window is
    /// measured against. Read from the calendar rather than counted, because a season's
    /// rounds are a fact about the fixtures and not a number this service may invent.
    /// </summary>
    private async Task<int> GetCurrentRoundAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var matchDays = await _matchRepository.GetMatchDaysAsync(seasonId, cancellationToken);
        var current = matchDays
            .Where(matchDay => matchDay.Date <= DateOnly.FromDateTime(DateTime.Now))
            .OrderByDescending(matchDay => matchDay.Number)
            .FirstOrDefault();

        return current?.Number ?? 1;
    }

    /// <summary>
    /// Pays the artilharia of every edition whose football is now all played, and of no other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A window closing is the only moment the world knows a competition is over: a division's
    /// last matchday, the cup's final going to penalties. So the artilharia is paid there rather
    /// than at the season close, which would hold a cup that was decided in June until
    /// December. It is asked of the editions whose window just closed, and only of those whose
    /// every window is now closed — the first division finishing its season while the third is
    /// two matchdays short pays its own artilharia and the third's stays unwon, which is the
    /// whole point of paying it per competition instead of per season.
    /// </para>
    /// <para>
    /// The Supercup is not on the list: it is one match, and an artilharia of one evening would
    /// be a striker's single goal paid a season's prize. A prize that cannot be paid is not
    /// allowed to take the money down with it either — a failure here is logged and the season
    /// goes on, because a ledger line that is written wrong is repairable and a calendar that
    /// will not close because of it is not.
    /// </para>
    /// </remarks>
    private async Task PayTheArtilleriasThatWereWonAsync(
        IReadOnlyCollection<Round> justClosed,
        CancellationToken cancellationToken)
    {
        var editions = justClosed
            .Select(window => window.CompetitionSeasonId)
            .Distinct()
            .ToList();

        foreach (var editionId in editions)
        {
            try
            {
                if (await IsTheEditionOverAsync(editionId, cancellationToken))
                {
                    await _scorerPrizes.PayAsync(editionId, cancellationToken);
                }
            }
            catch (Exception error)
            {
                _logger.LogError(
                    error,
                    "Could not pay the artilharia of edition {EditionId} whose last window just closed. The prize is keyed by player, so asking again pays it once.",
                    editionId);
            }
        }
    }

    /// <summary>
    /// Whether an edition has played all of its football: every window of it is closed, and it
    /// runs an artilharia at all.
    /// </summary>
    private async Task<bool> IsTheEditionOverAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var view = await _competitionRepository.GetSeasonViewByIdAsync(competitionSeasonId, cancellationToken);
        if (view is null || view.Type is not (CompetitionType.League or CompetitionType.Cup))
        {
            return false;
        }

        var windows = await _roundRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);

        return windows.Count > 0 && windows.All(window => window.IsCompleted);
    }

    /// <summary>
    /// Whether a window is over, which is what the window after it waits for.
    ///
    /// A fixture holds the window open until it is **finished** and nothing less counts: a
    /// match in progress, and even a match that exists but has not been kicked off, is a game
    /// that has not been played. The match row is asked about as well, because a fixture that
    /// was never moved to <c>Finished</c> — a match finished by a path that did not write the
    /// fixture back, a match from a process that was killed half way through it — has its own
    /// row as the only record of the truth.
    /// </summary>
    private async Task<bool> IsFinishedAsync(
        IReadOnlyCollection<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        foreach (var fixture in fixtures)
        {
            if (fixture.Status is FixtureStatus.Finished)
            {
                continue;
            }

            if (await _matchRepository.GetByFixtureAsync(fixture.Id, cancellationToken)
                is not { Status: MatchStatus.Finished })
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The fixtures of a day, grouped by the competition they are matches of, in the order
    /// the waves are played.
    /// </summary>
    private async Task<Dictionary<CompetitionType, List<Fixture>>> ReadWavesAsync(
        Guid matchDayId,
        CancellationToken cancellationToken = default)
    {
        var rounds = await _roundRepository.ListByMatchDayAsync(matchDayId, cancellationToken);
        var waves = new Dictionary<CompetitionType, List<Fixture>>();

        if (rounds.Count == 0)
        {
            return waves;
        }

        var fixtures = await _fixtureRepository.ListByRoundIdsAsync(
            rounds.Select(round => round.Id), cancellationToken);

        foreach (var round in rounds)
        {
            var type = await TypeOfAsync(round, cancellationToken);

            if (!waves.TryGetValue(type, out var bucket))
            {
                bucket = new List<Fixture>();
                waves[type] = bucket;
            }

            bucket.AddRange(fixtures.Where(fixture => fixture.RoundId == round.Id));
        }

        return waves;
    }

    private async Task<CompetitionType> TypeOfAsync(Round round, CancellationToken cancellationToken)
    {
        var season = await _competitionRepository.GetSeasonByIdAsync(
            round.CompetitionSeasonId, cancellationToken);

        if (season is null)
        {
            return CompetitionType.League;
        }

        var competition = await _competitionRepository.GetAsync(season.CompetitionId, cancellationToken);

        return competition?.Type ?? CompetitionType.League;
    }

    private static string NameOf(CompetitionType type) => type switch
    {
        CompetitionType.SuperCup => "supercopa",
        CompetitionType.Cup => "copa",
        _ => "campeonato"
    };
}
