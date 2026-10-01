using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Mappings;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Match use cases. The persisted row stays the source of truth for history while a
/// match is running; the engine's working memory lives in the session registry and is
/// rebuilt from the snapshot when a client reconnects.
/// </summary>
public class MatchService : IMatchCleaner
{
    private const int SquadSize = MatchRules.SquadSize;
    private const int BenchSize = MatchRules.BenchSize;
    private const string DefaultFormation = "4-3-3";

    /// <summary>
    /// The engine advances one minute per tick and the match loop ticks several times
    /// per second, so the match row is only rewritten every few minutes. Everything
    /// worth replaying — the events — is always persisted.
    /// </summary>
    private const int SnapshotIntervalMinutes = 5;

    private static readonly HashSet<MatchEventType> SnapshotForcingEvents =
    [
        MatchEventType.GoalScored,
        MatchEventType.OwnGoalScored,
        MatchEventType.YellowCardShown,
        MatchEventType.RedCardShown,
        MatchEventType.SubstitutionMade,
        MatchEventType.HalfTimeReached,
        MatchEventType.SecondHalfStarted,
        MatchEventType.MatchFinished
    ];

    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IMatchSessionRegistry _sessions;
    private readonly AttendanceContextFactory _attendanceContextFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CupProgressionService _cupProgression;

    /// <summary>
    /// Which process this is, and how long a match's working memory is honoured after this
    /// process last touched it.
    ///
    /// The world is played by more than one process now: the Scheduler plays the matches
    /// nobody is watching and this one plays the match somebody is. Both write to the same
    /// rows, so a match has to say whose memory it belongs to and for how long — otherwise
    /// a process that restarts takes every open match it can find, including the one the
    /// other process is in the middle of.
    /// </summary>
    private readonly IMatchHost _host;
    private readonly IOptions<WorldExecutionOptions> _world;
    private readonly IClock _clock;
    private readonly ILogger<MatchService> _logger;

    /// <summary>
    /// Which window of the matchday is playing. It is here because the rule it enforces is
    /// about starting a match, and a match is started from this service.
    /// </summary>
    private readonly MatchdayService _matchday;
    private readonly TacticsService _tactics;

    /// <summary>
    /// A club's money. It is here rather than inside the season progress because the gate is
    /// not a season's business: it belongs to both clubs, the one the manager follows and
    /// the one the engine played without him.
    /// </summary>
    private readonly FinanceService _financeService;
    private readonly SponsorOfferService _sponsorService;

    /// <summary>
    /// The manager's box, and the two readers a report is written with.
    ///
    /// A finished match is the one moment the world has something to say, and the service
    /// that settles the books is the service that settles the news: the same call that pays
    /// the gate and the wage bill is the call that tells the manager the result, the goals,
    /// the eleven and where he plays next.
    /// </summary>
    private readonly InboxService _inbox;
    private readonly MatchContextService _matchContext;

    /// <summary>
    /// The table a result is read against, so that a report can say where the result left
    /// the club rather than only what it was. It is the same reader the standings screen
    /// uses, deliberately: a report saying "sétimo" beside a table saying "décimo" would be
    /// two opinions about one division.
    /// </summary>
    private readonly StandingsService _standings;

    public MatchService(
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        IFixtureRepository fixtureRepository,
        IRoundRepository roundRepository,
        ICompetitionRepository competitionRepository,
        IMatchSessionRegistry sessions,
        AttendanceContextFactory attendanceContextFactory,
        IUnitOfWork unitOfWork,
        CupProgressionService cupProgression,
        MatchdayService matchday,
        TacticsService tactics,
        FinanceService financeService,
        SponsorOfferService sponsorService,
        InboxService inbox,
        MatchContextService matchContext,
        StandingsService standings,
        IMatchHost host,
        IOptions<WorldExecutionOptions> world,
        IClock clock,
        ILogger<MatchService> logger)
    {
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _fixtureRepository = fixtureRepository;
        _roundRepository = roundRepository;
        _competitionRepository = competitionRepository;
        _sessions = sessions;
        _attendanceContextFactory = attendanceContextFactory;
        _unitOfWork = unitOfWork;
        _cupProgression = cupProgression;
        _matchday = matchday;
        _tactics = tactics;
        _financeService = financeService;
        _sponsorService = sponsorService;
        _inbox = inbox;
        _matchContext = matchContext;
        _standings = standings;
        _host = host;
        _world = world;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _matchRepository.ListAsync(cancellationToken);

    /// <summary>
    /// Snapshot of a match: state plus the full event log. A client that reconnects
    /// calls this first, then resumes following the SignalR stream from
    /// <c>sequence + 1</c>.
    /// </summary>
    public async Task<MatchSnapshot> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(id, cancellationToken);

        var events = await _matchRepository.ListEventsAsync(id, cancellationToken);
        var homeTeam = await _teamRepository.GetAsync(match.HomeTeamId, cancellationToken);
        var awayTeam = await _teamRepository.GetAsync(match.AwayTeamId, cancellationToken);

        return new MatchSnapshot
        {
            Match = match,
            Events = events,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam
        };
    }

    public async Task<IReadOnlyList<MatchEvent>> GetEventsAsync(
        Guid id,
        int? afterSequence = null,
        CancellationToken cancellationToken = default)
    {
        if (await _matchRepository.GetAsync(id, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Match", id);
        }

        var events = await _matchRepository.ListEventsAsync(id, cancellationToken);

        var wanted = afterSequence.HasValue
            ? events.Where(matchEvent => matchEvent.Sequence > afterSequence.Value).ToList()
            : events.ToList();

        // Named in one read for the whole match. A client that has to ask who scored has to
        // hold the squad he belongs to, and a finished match is answered with the squad as it
        // is now — so a striker who has since left the club was a goal with nobody beside it.
        var playerIds = wanted
            .Select(matchEvent => matchEvent.PlayerId)
            .Where(playerId => playerId is not null)
            .Select(playerId => playerId!.Value)
            .Distinct()
            .ToList();

        var names = (await _playerRepository.ListByIdsAsync(playerIds, cancellationToken))
            .ToDictionary(player => player.Id, player => player.Name);

        foreach (var matchEvent in wanted)
        {
            if (matchEvent.PlayerId is { } playerId && names.TryGetValue(playerId, out var name))
            {
                matchEvent.PlayerName = name;
            }
        }

        return wanted;
    }

    /// <summary>
    /// Creates the playable session for a fixture and locks the starting eleven of
    /// both clubs. Starting a fixture that is already being played is not an error: the
    /// existing match is returned so the caller can watch it. A match whose working
    /// memory was lost, for example by a restart, is abandoned and the fixture is put
    /// back on the schedule instead of being left in a state nobody can reach.
    /// </summary>
    public async Task<MatchCommandResult> StartAsync(
        Guid fixtureId,
        int? seed = null,
        Guid? userTeamId = null,
        IReadOnlyCollection<Guid>? starterIds = null,
        IReadOnlyCollection<Guid>? benchIds = null,
        bool headless = false,
        string? tacticCode = null,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        // A manager may only start a match whose window of the matchday is the one playing.
        // The engine's own starts are exempt: they come from the matchday service, which only
        // ever starts the open window, so asking it again would be asking it to open the day
        // twice.
        if (!headless)
        {
            await _matchday.EnsureTheWaveIsOpenAsync(fixtureId, cancellationToken);
        }

        var existing = await _matchRepository.GetByFixtureAsync(fixtureId, cancellationToken);
        if (existing is not null)
        {
            if (_sessions.TryGet(existing.Id, out _))
            {
                // Already being played in this process. A manager is told the match to watch
                // so they join it rather than start a second one; a headless caller is told
                // no, because the match is already on the registry and something in this
                // process is driving it, and a second driver would play it twice as fast.
                //
                // Except when nothing is driving it. The registry holds working memory, and
                // working memory is not a claim: a match can be in it because a run started,
                // created the match and then stopped for any reason at all, and the next run
                // of the window finds the very same session and refuses forever. A fixture
                // like that is a matchday that can never be played and a season that stops
                // over it, so a headless caller takes the match over rather than believing a
                // registry entry that nothing is driving. The two cannot be talking about the
                // same match: a headless match is played to the end by the one run that
                // started it, inside the one round, and a second start of the same fixture
                // cannot be that run.
                if (!headless)
                {
                    return new MatchCommandResult
                    {
                        Accepted = true,
                        MatchId = existing.Id,
                        ErrorMessage = "Esta partida já está em andamento.",
                        Reason = MatchRefusal.AlreadyRunningHere
                    };
                }
            }
            else if (existing.IsFinished)
            {
                return new MatchCommandResult
                {
                    Accepted = false,
                    MatchId = existing.Id,
                    ErrorMessage = "Esta partida já foi disputada.",
                    Reason = MatchRefusal.AlreadyFinished
                };
            }

            // A match that is on the pitch and is not ours. Somebody else's process is
            // playing it with its working memory in hand, and this one leaves it alone: an
            // abandoned match is a match that stops halfway through, and a world that abandons
            // another process's football every time it wakes up is a world that never finishes
            // a round. It becomes reclaimable when its owner's lease runs out.
            if (!existing.CanBeReclaimedBy(_host.HostId, _clock.UtcNow, _world.Value.SessionLease))
            {
                return new MatchCommandResult
                {
                    Accepted = false,
                    MatchId = existing.Id,
                    ErrorMessage = "Esta partida está sendo jogada por outro processo.",
                    Reason = MatchRefusal.PlayedByAnotherProcess
                };
            }

            await AbandonAsync(existing, fixture, cancellationToken);
        }

        var seasonId = await ResolveSeasonIdAsync(fixture, cancellationToken);
        var homeTeam = await _teamRepository.GetAsync(fixture.HomeTeamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", fixture.HomeTeamId);
        var awayTeam = await _teamRepository.GetAsync(fixture.AwayTeamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", fixture.AwayTeamId);

        // A tactic nobody recognises is a request the catalogue cannot answer, and the
        // manager is told which codes exist rather than quietly given the club's own shape.
        var tactic = Tactics.Find(tacticCode);
        if (!string.IsNullOrWhiteSpace(tacticCode) && tactic is null)
        {
            throw new DomainValidationException(
                "UnknownTactic",
                $"Tática desconhecida: '{tacticCode}'. Use um código de {string.Join(", ", Tactics.All.Select(option => option.Code))}.");
        }

        // The kick-off is no longer the manager's: the world's own schedule opens a match, not
        // a hand opening a screen. So the order left on the board is read here, for each club,
        // and it is what that club goes out in whether anybody is watching or not. It is read
        // per club rather than for "the manager's club" because the world walk starts a
        // manager's match with nobody named at all — which is exactly the case the plan exists
        // for, and a plan applied only when a person was present would be a plan that only ever
        // worked while the manager was online.
        //
        // An order that arrives with the request still wins, because a manager standing at the
        // touchline is answering a question about this match rather than about the season.
        var orderHome = await _tactics.TheOrderAtTheKickOffAsync(homeTeam.Id, seasonId, cancellationToken);
        var orderAway = await _tactics.TheOrderAtTheKickOffAsync(awayTeam.Id, seasonId, cancellationToken);

        var askedForHome = starterIds is { Count: > 0 } && userTeamId == homeTeam.Id;
        var askedForAway = starterIds is { Count: > 0 } && userTeamId == awayTeam.Id;

        // The shape each club goes out in. A club with neither an order of its own nor a
        // manager to ask is given the shape read off its squad, which is what the engine would
        // have done on its own — the same answer, arrived at by asking.
        var homeTactic = askedForHome ? tactic : Tactics.Find(orderHome?.TacticCode);
        var awayTactic = askedForAway ? tactic : Tactics.Find(orderAway?.TacticCode);

        var homeSquad = await BuildSquadAsync(homeTeam, seasonId, homeTactic, cancellationToken);
        var awaySquad = await BuildSquadAsync(awayTeam, seasonId, awayTactic, cancellationToken);

        if (homeSquad.Lineup.Count < SquadSize || awaySquad.Lineup.Count < SquadSize)
        {
            throw new DomainValidationException(
                "SquadTooSmall",
                $"Os dois clubes precisam de ao menos {SquadSize} jogadores disponíveis para começar.");
        }

        // The manager's eleven is validated by the backend; the opponent is picked by
        // the same rules the engine would use on its own.
        if (askedForHome)
        {
            // Only pass benchIds if explicitly provided; otherwise let SelectStartingEleven use automatic bench
            var explicitBenchIds = benchIds is { Count: > 0 } ? benchIds : null;

            homeSquad = SelectStartingEleven(homeSquad, starterIds!, explicitBenchIds);
        }
        else if (askedForAway)
        {
            var explicitBenchIds = benchIds is { Count: > 0 } ? benchIds : null;

            awaySquad = SelectStartingEleven(awaySquad, starterIds!, explicitBenchIds);
        }
        else if (starterIds is { Count: > 0 } && userTeamId is not null)
        {
            // A command that names a club, and the club is one of the other four hundred in the
            // country. This is a manager who followed the wrong fixture, and being told so is
            // the whole answer — there is nothing to apply it to and nothing to guess. An
            // eleven that arrived naming no club at all is not a command and is applied to
            // neither side, and a club named with no eleven behind it is not one either: both
            // are walked over rather than refused, because the walk that opens a match carries
            // none of these arguments and a bug in one caller must not be able to stop the
            // world's football.
            throw new DomainValidationException(
                "TeamNotInMatch",
                "O time não está disputando esta partida.");
        }
        else
        {
            // A standing order, answered against the squad as it is on the day. A man who was
            // available when the manager wrote it and is not today is replaced rather than the
            // whole order refused: the plan was not wrong, the world moved under it, and a
            // match the world could not start because of a suspension is a match nobody sees.
            if (orderHome?.StarterIds is { Count: > 0 } homeStarterIds)
            {
                homeSquad = ApplyTheStandingOrder(homeSquad, homeStarterIds, orderHome.BenchIds);
            }

            if (orderAway?.StarterIds is { Count: > 0 } awayStarterIds)
            {
                awaySquad = ApplyTheStandingOrder(awaySquad, awayStarterIds, orderAway.BenchIds);
            }
        }

        // The absences of this matchday are served at its kick-off: a player who was
        // suspended or injured stays out of the squad built above, and his counter is one
        // match closer to being available again.
        await ServeAbsencesAsync(homeTeam, awayTeam, seasonId, cancellationToken);

        var matchSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);

        // The competition a fixture belongs to decides whether it is a league game or a cup
        // leg, and the whole of a fixture's identity — the window it is played in, the crowd
        // that turns up for it — follows from that and is read off the fixture's round rather
        // than being assumed to be a league.
        var competition = await ResolveCompetitionAsync(fixture, cancellationToken);
        var competitionType = competition.Type;
        var match = Match.Create(fixtureId, homeTeam.Id, awayTeam.Id, competitionType, competition.Window);

        // The shape each club actually went out in, recorded on the match rather than in the
        // plan. A plan is what somebody intends; this is what happened, and it is the only one
        // of the two that can be read back as the club's football a fortnight later when the
        // plan has been restated twice since.
        match.RecordTactic(homeTactic?.Code ?? string.Empty);
        match.RecordAwayTactic(awayTactic?.Code ?? string.Empty);

        // The two shirts, drawn once from the match's own seed and stamped on the match. It is
        // here rather than in the engine because it is not football: the same eleven is the same
        // eleven whichever shirt it is in, and a screen that redrew the draw on every read would
        // change a visitor's colours under the manager in the middle of the second half.
        var (homeKitSide, awayKitSide) = KitClash.Decide(
            homeTeam.KitInUse(KitSide.Home),
            homeTeam.AwayKit,
            awayTeam.KitInUse(KitSide.Home),
            awayTeam.AwayKit,
            matchSeed);
        match.RecordKits(homeKitSide, awayKitSide);

        // The squad strength behind the crowd is the eleven that is about to play and the bench
        // behind it, because that is the team the supporters are coming to watch on the day.
        var homeStars = PlayerRating.CalculateTeamStarsFromSnapshots(
            homeSquad.Lineup.Concat(homeSquad.Bench).ToList());
        var awayStars = PlayerRating.CalculateTeamStarsFromSnapshots(
            awaySquad.Lineup.Concat(awaySquad.Bench).ToList());

        // The crowd is a fact about the fixture rather than about how the game turns out: a
        // 5-0 does not empty a stand that had already filled it. It is worked out once, here,
        // and the noise in it is drawn from the match's own seed so a replayed match has the
        // same crowd in the same ground.
        var attendanceContext = await _attendanceContextFactory.ForFixtureAsync(
            fixtureId,
            homeTeam.Id,
            awayTeam.Id,
            competitionType,
            homeStars,
            awayStars,
            cancellationToken);

        match.KickOff(matchSeed, homeTeam.Stadium, attendanceContext);
        match.StartFirstHalf();

        var context = new MatchContext(
            match.Id,
            ToTeamInfo(homeTeam, homeStars),
            ToTeamInfo(awayTeam, awayStars),
            homeSquad.Lineup,
            awaySquad.Lineup,
            homeSquad.Bench,
            awaySquad.Bench,
            new DeterministicRandomSource(matchSeed),
            userTeamId,
            await ResolveCupTieAsync(fixtureId, cancellationToken));

        var state = new MatchState(context);
        var engine = new MatchEngine(context.Random);

        // The match belongs to this process from the whistle. The row says so, and the lease
        // it starts here is what lets another process take the match over if this one dies
        // in the middle of it — a fixture left in progress for ever is a round that can
        // never be completed, and one that is never completed is a season that stops.
        match.ClaimSession(_host.HostId, _clock.UtcNow);

        fixture.MarkInProgress();
        _fixtureRepository.Update(fixture);

        await _matchRepository.AddAsync(match, cancellationToken);

        var events = engine.Initialize(state, 0).ToList();
        ApplyToMatch(match, state, events);
        await PersistEventsAsync(match, events, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception error)
        {
            // The write did not go through. The refusal the database raises here is the one
            // that says another process got this fixture a moment earlier — a fixture can
            // hold one live match and no more, so the second of two processes that woke at
            // the same instant is the one that loses, and it says so rather than starting a
            // second copy of the same evening. It is caught as a plain exception because the
            // Application layer does not name EF Core, and the exception is logged in full so
            // a refusal that was something else is still visible.
            _logger.LogError(
                error,
                "The kick-off of fixture {FixtureId} was refused by the database. Another process is playing it.",
                fixtureId);

            return new MatchCommandResult
            {
                Accepted = false,
                MatchId = Guid.Empty,
                ErrorMessage = "Esta partida já foi iniciada por outro processo.",
                Reason = MatchRefusal.LostTheKickOff
            };
        }

        _sessions.Register(new LiveMatch(match.Id, fixture.RoundId, headless, engine, state));

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = match.Id,
            Events = events
        };
    }

    /// <summary>
    /// The round a fixture belongs to, needed to kick off the rest of the matchday.
    /// </summary>
    public async Task<Guid> GetRoundIdAsync(Guid fixtureId, CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        return fixture.RoundId;
    }

    /// <summary>
    /// Closes the match of a fixture that somebody started and nobody ever touched, so the
    /// world can play the fixture again instead of waiting for ever for somebody who is not
    /// coming.
    ///
    /// <para>
    /// A match the world left on the touchline is the manager's to play, and he is given the
    /// chance: the world opens it, leaves the clock to nobody and waits. What it cannot do is
    /// wait for ever — a match that was left and never once ticked has been nobody's business
    /// since the moment it was opened, and a window that holds it is a window the world can
    /// never walk past. It would say so by doing nothing at all, over and over, which is
    /// indistinguishable from a broken route.
    /// </para>
    ///
    /// <para>
    /// So the question is asked of the match itself, not of a clock. Untouched means minute
    /// zero, unfinished, and either no session in this process or a session still marked as
    /// left for the manager: a match the loop is driving, a match somebody claimed and a match
    /// that has been played for a while are all refused, because those belong to somebody.
    /// </para>
    /// </summary>
    /// <param name="fixtureId">The fixture whose match is sitting on the touchline.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Whether a match was closed, so the fixture could be played again.</returns>
    public async Task<bool> ReleaseTheUntouchedMatchAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetByFixtureAsync(fixtureId, cancellationToken);

        if (match is null || match.IsFinished || match.CurrentMinute != 0)
        {
            return false;
        }

        // A session in this process that is not waiting for a manager is somebody's match:
        // the loop is on it, or the manager claimed it and is watching it now.
        if (_sessions.TryGet(match.Id, out var session)
            && session.Driver is not MatchDriver.LeftForTheManager)
        {
            return false;
        }

        _logger.LogInformation(
            "Match {MatchId} was left on the touchline and nobody touched it. The world takes the fixture back.",
            match.Id);

        return await AbandonAsync(match.Id, cancellationToken);
    }

    /// <summary>
    /// Gives up on a match this process was playing and could not finish, and reopens its
    /// fixture so the next run of the window plays it again from the whistle.
    ///
    /// <para>
    /// This is the counterpart to <see cref="RecoverInterruptedMatchesAsync"/> for a process
    /// that is very much alive. A headless match that stops advancing — because the engine
    /// would not tick it, or because it never reached full time — leaves two things behind that
    /// must not be left behind: the match, still on the pitch and still holding a claim, and
    /// its session, still in this process's memory. The next run of the window asks to start
    /// the match, finds the session on the registry, and is told it is already being played —
    /// for ever, by a driver that stopped. A fixture stuck that way is a window that never
    /// closes, and a season with a window that never closes is a season that stops.
    /// </para>
    ///
    /// <para>
    /// So the match is closed and the fixture reopened here rather than left for a restart to
    /// clean up. The match ends <see cref="MatchStatus.Abandoned"/> rather than
    /// <see cref="MatchStatus.Finished"/>: it did not reach full time, and a match recorded as
    /// finished with a score it never actually played is a lie in the results table.
    /// </para>
    /// </summary>
    /// <param name="matchId">The match to give up on.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Whether the match was this process's and has been closed.</returns>
    public async Task<bool> AbandonAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetAsync(matchId, cancellationToken);

        // Somebody else's football is not this process's to close, and a match that has
        // already been decided is not a match to reopen.
        if (match is null || match.IsFinished)
        {
            return false;
        }

        if (!match.CanBeReclaimedBy(_host.HostId, _clock.UtcNow, _world.Value.SessionLease))
        {
            _logger.LogDebug(
                "Match {MatchId} belongs to {Host} and is left alone.",
                match.Id,
                match.SessionHost);

            return false;
        }

        // The session first. It is the reason the next run would be refused, and it is the
        // only part of this that lives in memory rather than in a column.
        _sessions.Remove(match.Id);

        var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);

        if (fixture is null)
        {
            match.Abandon();
            _matchRepository.Update(match);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        await AbandonAsync(match, fixture, cancellationToken);
        return true;
    }

    /// <summary>
    /// Abandons a match whose working memory is gone and reopens its fixture, so the
    /// fixture is playable again instead of being stuck for ever.
    /// </summary>
    private async Task AbandonAsync(Match match, Fixture fixture, CancellationToken cancellationToken)
    {
        // The session first, on every path and not only the public one. A match left in the
        // registry is a match the next run of its fixture is refused at the door — "already
        // running here" — while nothing is running it, and a fixture in that state can never
        // be played again by this process at all. Reopening the fixture without dropping the
        // session is what turns a match that was given up on into a fixture that is stranded.
        _sessions.Remove(match.Id);

        match.Abandon();
        _matchRepository.Update(match);

        fixture.Reopen();
        _fixtureRepository.Update(fixture);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Abandons every match this process was playing and has lost, and every match whose
    /// owner has stopped renewing its lease. The live sessions live in memory, so a match
    /// this process was playing can never be resumed; closing it is what keeps a fixture
    /// from being stranded in progress.
    ///
    /// <para>
    /// A match belonging to <i>another</i> process is not touched. That is the whole of what
    /// makes it safe for the API and the Scheduler to be running at the same time: the
    /// Scheduler plays the matches nobody is watching and the API plays the one somebody is,
    /// and a restart of either must not take the other's football away mid-match. A match
    /// whose owner died anyway is still reclaimed, because its lease runs out and the next
    /// run of its window finds it.
    /// </para>
    ///
    /// It also asks the matchday to close the windows that were played and never closed.
    /// A match that finished while the process was down was finished by nobody's call, and
    /// the window it belonged to is the last thing that would have closed it — so a season
    /// left running comes back with its calendar a day behind its own results.
    /// </summary>
    public async Task<int> RecoverInterruptedMatchesAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = await _matchRepository.ListUnfinishedAsync(cancellationToken);
        var now = _clock.UtcNow;
        var lease = _world.Value.SessionLease;
        var recovered = 0;

        foreach (var match in unfinished)
        {
            if (!match.CanBeReclaimedBy(_host.HostId, now, lease))
            {
                // Somebody else is playing it and their lease is alive. Not ours to close.
                _logger.LogDebug(
                    "Match {MatchId} is being played by {Host}; it is left alone.",
                    match.Id,
                    match.SessionHost);
                continue;
            }

            var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);
            if (fixture is null)
            {
                match.Abandon();
                _matchRepository.Update(match);
                recovered++;
                continue;
            }

            await AbandonAsync(match, fixture, cancellationToken);
            recovered++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _matchday.CloseTheWindowsThatWereLeftOpenAsync(cancellationToken);

        return recovered;
    }

    /// <summary>
    /// Closes the matches that are still on the pitch on a fixture the world has already
    /// decided, so they stop holding their fixtures.
    ///
    /// <para>
    /// A live match and its fixture are exclusive: the database refuses a second match for a
    /// fixture while the first one is running. So a match left running on a decided fixture is
    /// not a leftover nobody looks at — it is a claim on a fixture that can never be played
    /// again, held by a match that is never ticked, and no window will ever ask about it
    /// because every walk has already finished with that fixture. The world does not skip a
    /// matchday over that; it owes it for ever.
    /// </para>
    ///
    /// <para>
    /// The fixture is <b>not</b> reopened. It was decided, the result is in the table, and
    /// reopening it would put a played match back on the schedule and have the world play it
    /// twice. Only the match is closed, and it is closed as <c>Abandoned</c> because it never
    /// reached full time — a row recording a score that was never played is a lie in the
    /// results table.
    /// </para>
    ///
    /// <para>
    /// Whoever owns the match closes it, lease or not: the match is on a decided fixture, so
    /// there is no football anybody could be watching, and the lease is about a claim on a
    /// fixture that has already been settled.
    /// </para>
    /// </summary>
    /// <returns>How many matches were closed.</returns>
    public async Task<int> AbandonMatchesOnDecidedFixturesAsync(
        CancellationToken cancellationToken = default)
    {
        var orphans = await _matchRepository.ListUnfinishedOnFinishedFixturesAsync(cancellationToken);

        if (orphans.Count == 0)
        {
            return 0;
        }

        foreach (var match in orphans)
        {
            _sessions.Remove(match.Id);

            match.Abandon();
            _matchRepository.Update(match);

            _logger.LogDebug(
                "Match {MatchId} was left running on fixture {FixtureId}, which was already " +
                "decided. It is abandoned so the fixture is not held for ever.",
                match.Id,
                match.FixtureId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return orphans.Count;
    }

    /// <summary>
    /// The score of a match as the rest of the matchday sees it. Read from the live
    /// session while the match is being played and from the persisted row once it ends,
    /// so a scoreboard never freezes on a stale number.
    /// </summary>
    public async Task<MatchScoreRow> GetScoreAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);
        var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", match.FixtureId);

        var homeTeam = ToTeam(await _teamRepository.GetAsync(match.HomeTeamId, cancellationToken));
        var awayTeam = ToTeam(await _teamRepository.GetAsync(match.AwayTeamId, cancellationToken));

        // Read once, for both branches: a finished match is answered from the statistics
        // row rather than from zeros, and asking for it only after the live branch has
        // failed would make the cost depend on which branch ran.
        var played = await _matchRepository.GetStatisticsAsync(matchId, cancellationToken);

        if (_sessions.TryGet(matchId, out var session))
        {
            var homeStars = PlayerRating.CalculateTeamStarsFromSnapshots(
                session.State.HomeLineup.Concat(session.State.HomeBench).ToList());
            var awayStars = PlayerRating.CalculateTeamStarsFromSnapshots(
                session.State.AwayLineup.Concat(session.State.AwayBench).ToList());

            return new MatchScoreRow
            {
                RoundId = fixture.RoundId,
                MatchId = match.Id,
                FixtureId = match.FixtureId,
                HomeTeamId = homeTeam.Id,
                HomeTeamName = homeTeam.Name,
                HomeShortName = homeTeam.ShortName,
                AwayTeamId = awayTeam.Id,
                AwayTeamName = awayTeam.Name,
                AwayShortName = awayTeam.ShortName,
                HomePrimaryColor = homeTeam.PrimaryColor,
                HomeSecondaryColor = homeTeam.SecondaryColor,
                AwayPrimaryColor = awayTeam.PrimaryColor,
                AwaySecondaryColor = awayTeam.SecondaryColor,
                HomeGoals = session.State.HomeScore,
                AwayGoals = session.State.AwayScore,
                Minute = session.State.Minute,
                Half = HalfOf(session.State).ToString(),
                Status = session.State.MatchFinished ? nameof(MatchStatus.Finished) : nameof(MatchStatus.InProgress),
                IsFinished = session.State.MatchFinished,
                HomeOwnGoals = OwnGoalsOf(session.State.HomeLineup.Concat(session.State.HomeBench)),
                AwayOwnGoals = OwnGoalsOf(session.State.AwayLineup.Concat(session.State.AwayBench)),
                HomeYellowCards = YellowOf(session.State.HomeLineup.Concat(session.State.HomeBench)),
                AwayYellowCards = YellowOf(session.State.AwayLineup.Concat(session.State.AwayBench)),
                HomeRedCards = RedOf(session.State.HomeLineup.Concat(session.State.HomeBench)),
                AwayRedCards = RedOf(session.State.AwayLineup.Concat(session.State.AwayBench)),
                HomeInjuries = InjuredOf(session.State.HomeLineup.Concat(session.State.HomeBench)),
                AwayInjuries = InjuredOf(session.State.AwayLineup.Concat(session.State.AwayBench))
            };
        }

        // Finished match: use default stars for display (2.5 = 3 stars)
        const double defaultStars = 2.5;
        var homeInfo = ToTeamInfo(homeTeam, defaultStars);
        var awayInfo = ToTeamInfo(awayTeam, defaultStars);

        return new MatchScoreRow
        {
            RoundId = fixture.RoundId,
            MatchId = match.Id,
            FixtureId = match.FixtureId,
            HomeTeamId = homeInfo.Id,
            HomeTeamName = homeInfo.Name,
            HomeShortName = homeInfo.ShortName,
            AwayTeamId = awayInfo.Id,
            AwayTeamName = awayInfo.Name,
            AwayShortName = awayInfo.ShortName,
            HomePrimaryColor = homeInfo.PrimaryColor,
            HomeSecondaryColor = homeInfo.SecondaryColor,
            AwayPrimaryColor = awayInfo.PrimaryColor,
            AwaySecondaryColor = awayInfo.SecondaryColor,
            HomeGoals = match.HomeScore,
            AwayGoals = match.AwayScore,
            Minute = match.CurrentMinute,
            Half = match.Half.ToString(),
            Status = match.Status.ToString(),
            IsFinished = match.IsFinished,
            HomeOwnGoals = played?.HomeOwnGoals ?? 0,
            AwayOwnGoals = played?.AwayOwnGoals ?? 0,
            HomeYellowCards = played?.HomeYellowCards ?? 0,
            AwayYellowCards = played?.AwayYellowCards ?? 0,
            HomeRedCards = played?.HomeRedCards ?? 0,
            AwayRedCards = played?.AwayRedCards ?? 0,
            HomeInjuries = played?.HomeInjuries ?? 0,
            AwayInjuries = played?.AwayInjuries ?? 0
        };
    }

    /// <summary>
    /// The live match a club is playing right now, if there is one.
    ///
    /// **The answer comes off the registry, not out of the fixtures.** A club plays at most
    /// one match at a time, so the whole question is a walk of the matches being played —
    /// a handful on a matchday and none at all between them — rather than a read of every
    /// fixture of the season to find one that happens to be in progress. The registry holds
    /// the live working memory and nothing else, so a match that is not in it is not being
    /// played, whatever the persisted row still says.
    ///
    /// The answer carries enough to draw a badge: which match it is, who it is against, the
    /// score and the minute. A navbar that says "ao vivo" without saying against whom and at
    /// what score is a badge the manager has to click to learn anything.
    /// </summary>
    public async Task<LiveMatchSummary?> GetLiveMatchForTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        foreach (var matchId in _sessions.ActiveMatchIds)
        {
            if (!_sessions.TryGet(matchId, out var session))
            {
                continue;
            }

            var state = session.State;
            var isHome = state.HomeTeam.Id == teamId;
            if (!isHome && state.AwayTeam.Id != teamId)
            {
                continue;
            }

            if (state.MatchFinished)
            {
                continue;
            }

            return new LiveMatchSummary
            {
                MatchId = matchId,
                RoundId = session.RoundId,
                HomeTeamId = state.HomeTeam.Id,
                HomeTeamName = state.HomeTeam.Name,
                HomeShortName = state.HomeTeam.ShortName,
                HomePrimaryColor = state.HomeTeam.PrimaryColor,
                HomeSecondaryColor = state.HomeTeam.SecondaryColor,
                AwayTeamId = state.AwayTeam.Id,
                AwayTeamName = state.AwayTeam.Name,
                AwayShortName = state.AwayTeam.ShortName,
                AwayPrimaryColor = state.AwayTeam.PrimaryColor,
                AwaySecondaryColor = state.AwayTeam.SecondaryColor,
                HomeGoals = state.HomeScore,
                AwayGoals = state.AwayScore,
                Minute = state.Minute,
                IsHome = isHome,
                AtHalfTime = state.HalfTimePauseActive
            };
        }

        return await LiveMatchAnotherProcessIsPlayingAsync(teamId, cancellationToken);
    }

    /// <summary>
    /// The same answer, asked of the matches another process is playing.
    ///
    /// <para>
    /// A club's live working memory is in the process that kicked the match off, and there are
    /// two of them: the one that holds the sockets and the Scheduler, which plays the matches
    /// nobody is watching. A badge answered from this process's registry alone would say a club
    /// is not playing while the Scheduler is playing its match in front of it — the badge goes
    /// quiet exactly when there is a match to watch, which is the one moment it exists for.
    /// </para>
    ///
    /// <para>
    /// So the walk of the matches being played is a walk of the registry and then of the live
    /// rows, and both are a handful: a club plays at most one match at a time, and the whole
    /// world is rarely more than a matchday's worth. What this process does not own, it reads
    /// from the row rather than the session, which is the same answer the loop republishes
    /// from and the same one the client would be sent.
    /// </para>
    ///
    /// <para>
    /// The two clubs are read in one call. A badge that asked for each club in turn would be a
    /// badge that costs a round trip per field it draws.
    /// </para>
    /// </summary>
    private async Task<LiveMatchSummary?> LiveMatchAnotherProcessIsPlayingAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var live = await _matchRepository.ListLiveExceptHostAsync(_host.HostId, cancellationToken);
        var mine = live.FirstOrDefault(row => row.Match.HomeTeamId == teamId || row.Match.AwayTeamId == teamId);

        if (mine is null)
        {
            return null;
        }

        var match = mine.Match;
        var isHome = match.HomeTeamId == teamId;
        var clubs = (await _teamRepository.ListByIdsAsync(
            new[] { match.HomeTeamId, match.AwayTeamId },
            cancellationToken)).ToDictionary(team => team.Id);

        if (!clubs.TryGetValue(match.HomeTeamId, out var home) || !clubs.TryGetValue(match.AwayTeamId, out var away))
        {
            return null;
        }

        return new LiveMatchSummary
        {
            MatchId = match.Id,
            RoundId = mine.RoundId,
            HomeTeamId = home.Id,
            HomeTeamName = home.Name,
            HomeShortName = home.ShortName,
            HomePrimaryColor = home.PrimaryColor,
            HomeSecondaryColor = home.SecondaryColor,
            AwayTeamId = away.Id,
            AwayTeamName = away.Name,
            AwayShortName = away.ShortName,
            AwayPrimaryColor = away.PrimaryColor,
            AwaySecondaryColor = away.SecondaryColor,
            HomeGoals = match.HomeScore,
            AwayGoals = match.AwayScore,
            Minute = match.CurrentMinute,
            IsHome = isHome,
            AtHalfTime = mine.IsHalfTime
        };
    }

    /// <summary>
    /// The own goals on one side, counted from the players rather than kept as a running
    /// total: the number on a matchday is the number the eleven actually produced, and a
    /// counter that disagreed with them would be a second source of truth.
    /// </summary>
    private static int OwnGoalsOf(IEnumerable<MatchPlayerSnapshot> players) =>
        players.Sum(player => player.MatchOwnGoals);

    private static int InjuredOf(IEnumerable<MatchPlayerSnapshot> players) =>
        players.Count(player => player.InjuredOff);

    /// <summary>
    /// Yellow and red counted the way the statistics row counts them: off the players
    /// rather than off a running total, so a live match and a finished one cannot disagree
    /// about how many cards a side has.
    /// </summary>
    private static int YellowOf(IEnumerable<MatchPlayerSnapshot> players) =>
        players.Sum(player => player.MatchYellowCards);

    private static int RedOf(IEnumerable<MatchPlayerSnapshot> players) =>
        players.Count(player => player.RedCard);

    /// <summary>
    /// The eleven and the bench of both clubs, as locked in at kick-off.
    /// </summary>
    public async Task<MatchLineup> GetLineupAsync(
        Guid matchId,
        Guid? userTeamId = null,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (_sessions.TryGet(matchId, out var session))
        {
            return new MatchLineup
            {
                MatchId = match.Id,
                UserTeamIndex = ResolveUserTeamIndex(match, userTeamId),
                HomeTeam = ToTeam(await _teamRepository.GetAsync(match.HomeTeamId, cancellationToken)),
                AwayTeam = ToTeam(await _teamRepository.GetAsync(match.AwayTeamId, cancellationToken)),
                HomeKitSide = match.HomeKitSide,
                AwayKitSide = match.AwayKitSide,
                HomeLineup = session.State.HomeLineup,
                AwayLineup = session.State.AwayLineup,
                HomeBench = session.State.HomeBench,
                AwayBench = session.State.AwayBench
            };
        }

        var seasonId = await ResolveSeasonIdAsync(match.FixtureId, cancellationToken);
        var homeSquad = await BuildSquadAsync(
            ToTeam(await _teamRepository.GetAsync(match.HomeTeamId, cancellationToken)), seasonId,
            cancellationToken: cancellationToken);
        var awaySquad = await BuildSquadAsync(
            ToTeam(await _teamRepository.GetAsync(match.AwayTeamId, cancellationToken)), seasonId,
            cancellationToken: cancellationToken);

        return new MatchLineup
        {
            MatchId = match.Id,
            UserTeamIndex = ResolveUserTeamIndex(match, userTeamId),
            HomeTeam = homeSquad.Team,
            AwayTeam = awaySquad.Team,
            HomeKitSide = match.HomeKitSide,
            AwayKitSide = match.AwayKitSide,
            HomeLineup = homeSquad.Lineup,
            AwayLineup = awaySquad.Lineup,
            HomeBench = homeSquad.Bench,
            AwayBench = awaySquad.Bench
        };
    }

    /// <summary>
    /// Live state of a match. A finished match is answered from the persisted row and its
    /// statistics, because its working memory is no longer in the registry — and it is
    /// answered from them rather than from zeros, so the statistics a manager reads after
    /// the whistle are the ones of the match he just watched.
    /// </summary>
    public async Task<MatchStateView> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            var played = await _matchRepository.GetStatisticsAsync(matchId, cancellationToken);

            return new MatchStateView
            {
                MatchId = match.Id,
                HomeScore = match.HomeScore,
                AwayScore = match.AwayScore,
                Minute = match.CurrentMinute,
                Second = 0,
                Half = (int)match.Half,
                Sequence = match.Sequence,
                StoppageTimeMinutes = 0,
                Status = match.Status.ToString(),
                IsPaused = false,
                IsHalfTime = match.Status == MatchStatus.HalfTime,
                IsFinished = match.IsFinished,
                Speed = 1,
                HomeShots = played?.HomeShots ?? 0,
                AwayShots = played?.AwayShots ?? 0,
                HomeShotsOnTarget = played?.HomeShotsOnTarget ?? 0,
                AwayShotsOnTarget = played?.AwayShotsOnTarget ?? 0,
                HomeCorners = played?.HomeCorners ?? 0,
                AwayCorners = played?.AwayCorners ?? 0,
                HomeCards = (played?.HomeYellowCards ?? 0) + (played?.HomeRedCards ?? 0),
                AwayCards = (played?.AwayYellowCards ?? 0) + (played?.AwayRedCards ?? 0),
                HomeFouls = played?.HomeFouls ?? 0,
                AwayFouls = played?.AwayFouls ?? 0,
                HomeSaves = played?.HomeSaves ?? 0,
                AwaySaves = played?.AwaySaves ?? 0,
                HomePossession = played?.HomePossession ?? 50,
                AwayPossession = played?.AwayPossession ?? 50,
                SubstitutionsUsedHome = played?.HomeSubstitutions ?? 0,
                SubstitutionsUsedAway = played?.AwaySubstitutions ?? 0,
                PenaltyAwaitingSelection = false,
                PossessionTeam = 0,
                PossessionPlayerId = null,
                FormationHome = Played(played?.HomeFormation) ?? DefaultFormation,
                FormationAway = Played(played?.AwayFormation) ?? DefaultFormation,
                Attendance = match.Attendance,
                GateRevenue = match.Gate.GrossRevenue,
                UserTeamId = null
            };
        }

        lock (session.Gate)
        {
            return ToView(session.State, match.Status, match);
        }
    }

    /// <summary>
    /// A formation stored by a match played before this column existed, read as nothing
    /// rather than as an empty shape on a team sheet.
    /// </summary>
    private static string? Played(string? formation) =>
        string.IsNullOrWhiteSpace(formation) ? null : formation;

    /// <summary>
    /// Advances the match by one tick and persists whatever the engine produced. When
    /// the clock runs out the result is written back to the match and the fixture.
    /// </summary>
    public async Task<MatchCommandResult> TickAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return new MatchCommandResult
            {
                Accepted = false,
                MatchId = matchId,
                ErrorMessage = "Esta partida não está em andamento."
            };
        }

        List<MatchEngineEvent> produced;

        lock (session.Gate)
        {
            if (session.State.Paused)
            {
                return new MatchCommandResult
                {
                    Accepted = false,
                    MatchId = matchId,
                    ErrorMessage = "A partida está pausada."
                };
            }

            if (session.State.HalfTimePauseActive)
            {
                return new MatchCommandResult
                {
                    Accepted = false,
                    MatchId = matchId,
                    ErrorMessage = "A partida está no intervalo."
                };
            }

            session.Engine.Tick(session.State);
            produced = DrainFeed(session.State);
        }

        var finished = session.State.MatchFinished;
        var dueForSnapshot = finished
                             || produced.Any(e => SnapshotForcingEvents.Contains(e.Type))
                             || match.CurrentMinute % SnapshotIntervalMinutes == 0;

        if (dueForSnapshot)
        {
            ApplyToMatch(match, session.State, produced);
        }

        await PersistEventsAsync(match, produced, cancellationToken);

        // The events are the part a reconnecting client replays, so they are always
        // committed here. The match row is only rewritten on the snapshot cadence.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The lease on this match's working memory, renewed by the only thing that can play
        // it. It is what tells another process that this match is being looked after, and it
        // is renewed on the snapshot cadence rather than on every tick because the row is
        // only written on that cadence anyway.
        if (dueForSnapshot)
        {
            match.TouchSession(_clock.UtcNow);
        }

        if (finished)
        {
            match.Finish();
            _sessions.Remove(matchId);
            await SaveStatisticsAsync(match.Id, session.State, cancellationToken);
            await SavePlayerStatisticsAsync(
                match.Id,
                await ResolveSeasonIdAsync(match.FixtureId, cancellationToken),
                session.State,
                cancellationToken);
            await ApplySeasonProgressAsync(match.FixtureId, session.State, match.Seed, cancellationToken);
            await SettleTheBooksAsync(match, cancellationToken);

            var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);
            fixture?.MarkFinished();
            if (fixture is not null)
            {
                _fixtureRepository.Update(fixture);
            }

            // A match can be half of a cup tie, and only the second leg decides one. The cup
            // is told about every finish and works out for itself which of the two this was:
            // a championship match is not a cup match, and a first leg is not a decision.
            await _cupProgression.AdvanceAsync(
                CupLegOutcomeFactory.From(match, session.State),
                cancellationToken);

            // The day moves on when a match finishes and not before, because a matchday is a
            // sequence: the cup window opens when the last championship game of the day is
            // over, and the day is over when the last window of it is. Nobody has to ask
            // whether the day is finished; it is worked out here, from the matches.
            await _matchday.AdvanceAsync(match.FixtureId, cancellationToken);
        }

        if (dueForSnapshot)
        {
            await CommitAsync(match, cancellationToken);
        }

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = matchId,
            Events = produced
        };
    }

    public async Task<MatchCommandResult> PauseAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        lock (session.Gate)
        {
            if (session.State.Paused)
            {
                return Refused(matchId, "A partida já está pausada.");
            }

            session.State.Paused = true;
        }

        return new MatchCommandResult { Accepted = true, MatchId = matchId };
    }

    public async Task<MatchCommandResult> ResumeAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        lock (session.Gate)
        {
            if (!session.State.Paused)
            {
                return Refused(matchId, "A partida não está pausada.");
            }

            session.State.Paused = false;
        }

        return new MatchCommandResult { Accepted = true, MatchId = matchId };
    }

    public async Task<MatchCommandResult> ChangeSpeedAsync(
        Guid matchId,
        int speed,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (speed is < 1 or > 8)
        {
            throw new DomainValidationException("InvalidSpeed", "A velocidade deve estar entre 1 e 8.");
        }

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        lock (session.Gate)
        {
            session.State.Speed = speed;
        }

        return new MatchCommandResult { Accepted = true, MatchId = matchId };
    }

    /// <summary>
    /// Swaps a player for a substitute. The outgoing player leaves the eleven and the
    /// incoming one takes his place, keeping the substitution counters the engine
    /// uses to enforce the limit.
    /// </summary>
    public async Task<MatchCommandResult> SubstituteAsync(
        Guid matchId,
        Guid teamId,
        Guid playerOutId,
        Guid playerInId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        if (playerOutId == playerInId)
        {
            throw new DomainValidationException("InvalidSubstitution", "Um jogador não pode substituir a si mesmo.");
        }

        List<MatchEngineEvent> substitutionEvents;

        lock (session.Gate)
        {
            // A manager's command is a command for his own club. The route carries a team id
            // the client chose, and a client can be on a match of two other clubs — the round
            // group carries those to his screen — so the id is checked against the match's own
            // idea of whose club he is before it is checked against the two teams playing.
            //
            // The engine's own substitutions do not come through here, so a match still runs
            // the same way: this refuses a command, not football.
            if (!session.State.ManagerCommandsTeam(teamId))
            {
                throw new DomainValidationException(
                    "NotYourTeam",
                    "Só é possível fazer substituições no seu próprio time.");
            }

            var isHome = teamId == match.HomeTeamId;
            if (!isHome && teamId != match.AwayTeamId)
            {
                throw new DomainValidationException("TeamNotInMatch", "O time não está disputando esta partida.");
            }

            var lineup = isHome ? session.State.HomeLineup : session.State.AwayLineup;
            var bench = isHome ? session.State.HomeBench : session.State.AwayBench;

            var outgoing = lineup.FirstOrDefault(player => player.PlayerId == playerOutId);
            if (outgoing is null || !outgoing.IsOnPitch)
            {
                throw new DomainValidationException("PlayerNotOnPitch", "O jogador que sai não está em campo.");
            }

            var incoming = bench.FirstOrDefault(player => player.PlayerId == playerInId);
            if (incoming is null)
            {
                throw new DomainValidationException("PlayerNotOnBench", "O jogador que entra não está no banco.");
            }

            // A substitute is spent. He came off the pitch, he is not in the match any
            // more, and putting him back on is the same change twice.
            if (incoming.SubbedOff)
            {
                throw new DomainValidationException(
                    "PlayerAlreadySubstituted",
                    "Esse jogador já saiu de campo e não pode voltar a entrar.");
            }

            // The rules of a swap live in the domain, because the engine has to obey the
            // same ones when it substitutes on its own: a manager who cannot do it must not
            // be told he can, and the engine must not hand the opposition something the
            // manager is not allowed to.
            if (!MatchSubstitution.CanSwap(lineup, outgoing, incoming))
            {
                throw new DomainValidationException(
                    "GoalkeeperRequired",
                    "O único goleiro em campo só pode ser trocado por outro goleiro.");
            }

            var used = isHome ? session.State.SubstitutionsHome : session.State.SubstitutionsAway;
            if (used >= MatchRules.MaxSubstitutions)
            {
                throw new DomainValidationException(
                    "SubstitutionLimitReached",
                    "Este clube já usou todas as substituições.");
            }

            MatchSubstitution.Swap(session.State, isHome, outgoing, incoming);

            // A man who cannot carry on is off the pitch because this change happened, and
            // the season absence is written from the same instant. Settling it here rather
            // than in the engine keeps the hold the engine opened from outliving the
            // decision that closes it.
            session.State.ResolvePendingInjury(outgoing);

            var events = new List<MatchEngineEvent>
            {
                EmitEvent(session.State, MatchEventType.SubstitutionMade, teamId, incoming.PlayerId,
                    // The club is named because the feed does not belong to one club: a manager
                    // following a whole matchday sees the changes of every fixture on it, and
                    // a substitution that does not say whose it was is a sentence about
                    // nobody. The engine's own wordings name their club for the same reason.
                    $"{TeamNameOf(session.State, isHome)}: {incoming.Name} entra no lugar de {outgoing.Name}")
            };

            DrainFeed(session.State);
            substitutionEvents = events;
        }

        await PersistEventsAsync(match, substitutionEvents, cancellationToken);
        await CommitAsync(match, cancellationToken);

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = matchId,
            Events = substitutionEvents
        };
    }

    /// <summary>
    /// Chooses the player who will take a penalty the engine awarded. The manager names
    /// the taker, but he does not decide the shot: the engine resolves it with the same
    /// rolls it would have used had the taker been chosen by it.
    /// </summary>
    public async Task<MatchCommandResult> SelectPenaltyTakerAsync(
        Guid matchId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        List<MatchEngineEvent> penaltyEvents;

        lock (session.Gate)
        {
            if (!session.State.PenaltyAwaitingSelection)
            {
                return Refused(matchId, "Não há pênalti para cobrar nesta partida.");
            }

            // The same rule the substitution above obeys, for the same reason: naming who
            // takes a penalty is a decision about a club, and it is the manager's own.
            if (!session.State.ManagerCommandsTeam(teamId))
            {
                throw new DomainValidationException(
                    "NotYourTeam",
                    "Só é possível escolher o cobrador do seu próprio time.");
            }

            var isHome = teamId == match.HomeTeamId;
            if (!isHome && teamId != match.AwayTeamId)
            {
                throw new DomainValidationException("TeamNotInMatch", "O time não está disputando esta partida.");
            }

            if (isHome ? session.State.PenaltyTeam != 1 : session.State.PenaltyTeam != 2)
            {
                throw new DomainValidationException("PenaltyNotForTeam", "O pênalti não é deste time.");
            }

            var lineup = isHome ? session.State.HomeLineup : session.State.AwayLineup;
            var taker = lineup.FirstOrDefault(player => player.PlayerId == playerId);

            if (taker is null || !taker.IsOnPitch)
            {
                throw new DomainValidationException("PlayerNotOnPitch", "O cobrador de pênalti não está em campo.");
            }

            penaltyEvents = session.Engine.TakePenalty(session.State, isHome, taker).ToList();
            DrainFeed(session.State);
        }

        await PersistEventsAsync(match, penaltyEvents, cancellationToken);
        await CommitAsync(match, cancellationToken);

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = matchId,
            Events = penaltyEvents
        };
    }

    /// <summary>
    /// Names the order the manager's club will take the penalties in, and lets the shootout
    /// start.
    ///
    /// It is a command like the taker of a penalty and the replacement of a man who cannot
    /// continue: the engine decides that the moment has come and the manager decides who
    /// walks to the spot. The men are validated by the engine, which is also the only thing
    /// that knows who may take — so a client cannot put a reserve goalkeeper on the spot or
    /// name the same man twice, whichever it would like to do.
    /// </summary>
    /// <param name="matchId">The match standing at ninety minutes with the shootout open.</param>
    /// <param name="teamId">The club whose manager is naming the order.</param>
    /// <param name="takers">The men, in the order they will walk to the spot.</param>
    public async Task<MatchCommandResult> NameShootoutOrderAsync(
        Guid matchId,
        Guid teamId,
        IReadOnlyList<Guid> takers,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        List<MatchEngineEvent> produced;

        lock (session.Gate)
        {
            if (session.State.Shootout is null)
            {
                return Refused(matchId, "Esta partida não está indo para disputa de pênaltis.");
            }

            if (teamId != match.HomeTeamId && teamId != match.AwayTeamId)
            {
                throw new DomainValidationException("TeamNotInMatch", "O time não está disputando esta partida.");
            }

            session.Engine.NameShootoutOrder(session.State, teamId, takers);

            // The order is not an event anybody is told about — it is a decision, and the
            // shootout itself is what the feed carries. The feed is drained anyway, because a
            // manager who has been standing at the spot has a state that is now a tick behind
            // the one the other managers are watching.
            produced = DrainFeed(session.State);
        }

        await PersistEventsAsync(match, produced, cancellationToken);
        await CommitAsync(match, cancellationToken);

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = matchId,
            Events = produced
        };
    }

    /// <summary>
    /// Leaves the half-time pause and starts the second half.
    /// </summary>
    public async Task<MatchCommandResult> ContinueSecondHalfAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "Esta partida não está em andamento.");
        }

        List<MatchEngineEvent> produced;

        lock (session.Gate)
        {
            if (!session.State.HalfTimePauseActive)
            {
                return Refused(matchId, "A partida não está no intervalo.");
            }

            session.Engine.ContinueSecondHalf(session.State);
            match.ReachHalfTime();
            match.StartSecondHalf();

            produced = DrainFeed(session.State);
        }

        await PersistEventsAsync(match, produced, cancellationToken);
        await CommitAsync(match, cancellationToken);

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = matchId,
            Events = produced
        };
    }

    /// <summary>
    /// The name of the club playing on that side, read off the match's own state so a
    /// sentence written about one of its players can say who they play for.
    /// </summary>
    private static string TeamNameOf(MatchState state, bool home) =>
        home ? state.HomeTeam.Name : state.AwayTeam.Name;

    /// <summary>
    /// Hands a headless match over to the manager of one of its clubs.
    ///
    /// A cup window opens every fixture of it at once — the manager's club included — so a
    /// manager who reaches his own club's match on a day it has already kicked off finds a
    /// match with no manager behind it: the engine is playing both sides, the interval is
    /// left to itself and a substitution is refused because nobody is attached to answer it.
    /// This is the seam for that: the moment the manager shows up with his club's id, the
    /// keyboard is handed over. The score and the eleven are left exactly where they are;
    /// only the control plane moves from the loop to the manager, so a matchday that has
    /// already seen the kick-off keeps on looking like one.
    ///
    /// A claim for a match that already has a manager, that is not headless, or that is not
    /// the manager's own side, is refused and is a no-op — the manager only ever takes his
    /// own club, and a screen that sent the opposition's id is a screen that never had the
    /// right to claim it. A match that has already finished by the time the claim lands
    /// (its session is gone) refuses too: there is nothing left to hand over.
    /// </summary>
    public bool AttachManager(Guid matchId, Guid userTeamId)
    {
        if (!_sessions.TryGet(matchId, out var session))
        {
            return false;
        }

        // A match the engine is not auto-continuing is already watched by somebody. The
        // keyboard is not taken twice: a second claim for a managed match is refused.
        if (!session.AutoContinue || session.State.ManagerTeamId is not null)
        {
            return false;
        }

        // The manager claims the side he is acting for. A claim for any other club is a
        // command without authority, and it is refused here instead of being trusted, the
        // same rule a substitution obeys.
        if (session.State.HomeTeam.Id != userTeamId && session.State.AwayTeam.Id != userTeamId)
        {
            return false;
        }

        lock (session.Gate)
        {
            // The clock is moving while the claim is made, so the test is repeated inside
            // the lock: a match that was claimed by another tab, or that finished in the
            // meantime, is left alone instead of being overwritten.
            if (!session.AutoContinue || session.State.ManagerTeamId is not null)
            {
                return false;
            }

            session.State.ManagerTeamId = userTeamId;
            session.AutoContinue = false;
            session.ManagerClaimedAt = _clock.UtcNow;

            // And it is his match now: the world left it on the touchline for him and the
            // background loop was told to keep off it, so the claim is what hands the clock
            // back. Without this the loop would go on leaving a match alone that the manager
            // is watching — a match that never moves while he watches it.
            session.Driver = MatchDriver.None;
        }

        return true;
    }

    /// <summary>
    /// Gives a claim that nobody is answering back to the engine, and answers it.
    ///
    /// <para>
    /// A manager's claim stops the engine substituting for his side and gives him the two
    /// decisions worth giving a person: who takes a penalty, and who comes on for a man who
    /// cannot carry on. Both hold the clock. A claim that is never answered therefore holds
    /// the match there for ever, and the world behind it stops with it: the fixture is owed
    /// and the window never closes.
    /// </para>
    ///
    /// <para>
    /// So a claim is honoured while it is being answered and expires after
    /// <see cref="MatchRules.ManagerDecisionTimeoutSeconds"/> of silence. The match then goes
    /// on being a match nobody is managing: the engine picks the taker and the replacement,
    /// the loop is handed the clock back, and a manager who comes back afterwards finds a
    /// game that kept moving rather than one frozen since the afternoon.
    /// </para>
    ///
    /// <para>
    /// It only ever fires while a decision is actually open. A manager who is watching a
    /// match that is not waiting on him is left alone, because the whole point of the claim
    /// was that he is there.
    /// </para>
    /// </summary>
    public async Task<bool> ReleaseAStaleClaimAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGet(matchId, out var session))
        {
            return false;
        }

        var waitingOnTheManager =
            session.State.ManagerTeamId is not null
            && (session.State.PenaltyAwaitingSelection || session.State.InjuryAwaitingSubstitution);

        if (!waitingOnTheManager || session.ManagerClaimedAt is not { } claimedAt)
        {
            return false;
        }

        if (_clock.UtcNow - claimedAt < TimeSpan.FromSeconds(MatchRules.ManagerDecisionTimeoutSeconds))
        {
            return false;
        }

        var events = new List<MatchEngineEvent>();

        lock (session.Gate)
        {
            if (session.State.PenaltyAwaitingSelection || session.State.InjuryAwaitingSubstitution)
            {
                session.State.ManagerTeamId = null;
                session.AutoContinue = true;
                session.ManagerClaimedAt = null;
                session.Driver = MatchDriver.WalkedByTheWorld;

                events.AddRange(session.Engine.ReleaseTheManager(session.State));
            }
        }

        if (events.Count == 0)
        {
            return false;
        }

        DrainFeed(session.State);

        var match = await GetMatchAsync(matchId, cancellationToken);
        await PersistEventsAsync(match, events, cancellationToken);

        return true;
    }

    /// <summary>
    /// Result of a match, as a results screen needs it. Served from the live session
    /// while it still exists and from the persisted row afterwards.
    /// </summary>
    public async Task<MatchResultView> GetResultAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (_sessions.TryGet(matchId, out var session))
        {
            lock (session.Gate)
            {
                return ToResult(session.State);
            }
        }

        var statistics = await _matchRepository.GetStatisticsAsync(matchId, cancellationToken);

        return new MatchResultView
        {
            MatchId = matchId,
            HomeScore = match.HomeScore,
            AwayScore = match.AwayScore,
            HomeShots = statistics?.HomeShots ?? 0,
            AwayShots = statistics?.AwayShots ?? 0,
            HomeShotsOnTarget = statistics?.HomeShotsOnTarget ?? 0,
            AwayShotsOnTarget = statistics?.AwayShotsOnTarget ?? 0,
            HomeCorners = statistics?.HomeCorners ?? 0,
            AwayCorners = statistics?.AwayCorners ?? 0,
            HomeCards = (statistics?.HomeYellowCards ?? 0) + (statistics?.HomeRedCards ?? 0),
            AwayCards = (statistics?.AwayYellowCards ?? 0) + (statistics?.AwayRedCards ?? 0),
            HomeFouls = statistics?.HomeFouls ?? 0,
            AwayFouls = statistics?.AwayFouls ?? 0,
            HomePossession = statistics?.HomePossession ?? 50,
            AwayPossession = statistics?.AwayPossession ?? 50,
            FormationHome = Played(statistics?.HomeFormation) ?? DefaultFormation,
            FormationAway = Played(statistics?.AwayFormation) ?? DefaultFormation,
            SubstitutionsHome = statistics?.HomeSubstitutions ?? 0,
            SubstitutionsAway = statistics?.AwaySubstitutions ?? 0
        };
    }

    /// <summary>
    /// Freezes the engine's working memory into the structured statistics row, so the
    /// result survives the end of the live session.
    /// </summary>
    private async Task SaveStatisticsAsync(
        Guid matchId,
        MatchState state,
        CancellationToken cancellationToken)
    {
        var statistics = MatchStatistics.Create(matchId);
        statistics.SetPossession(state.HomePossession);
        statistics.SetFormations(state.HomeFormation, state.AwayFormation);

        for (var i = 0; i < state.HomeShots; i++) statistics.AddShot(true, i < state.HomeShotsOnTarget);
        for (var i = 0; i < state.AwayShots; i++) statistics.AddShot(false, i < state.AwayShotsOnTarget);
        for (var i = 0; i < state.HomeCorners; i++) statistics.AddCorner(true);
        for (var i = 0; i < state.AwayCorners; i++) statistics.AddCorner(false);
        for (var i = 0; i < state.HomeFouls; i++) statistics.AddFoul(true);
        for (var i = 0; i < state.AwayFouls; i++) statistics.AddFoul(false);
        for (var i = 0; i < state.SubstitutionsHome; i++) statistics.AddSubstitution(true);
        for (var i = 0; i < state.SubstitutionsAway; i++) statistics.AddSubstitution(false);
        for (var i = 0; i < state.HomeSaves; i++) statistics.AddSave(true);
        for (var i = 0; i < state.AwaySaves; i++) statistics.AddSave(false);
        for (var i = 0; i < state.HomeScore; i++) statistics.AddGoal(true);
        for (var i = 0; i < state.AwayScore; i++) statistics.AddGoal(false);

        AddCards(statistics, state, state.HomeLineup.Concat(state.HomeBench), true);
        AddCards(statistics, state, state.AwayLineup.Concat(state.AwayBench), false);

        // The two numbers a matchday shows that are not in the score: an own goal is scored
        // against the side that made it, and a knock is a fact about a side rather than
        // about the ball.
        AddDetail(statistics, state.HomeLineup.Concat(state.HomeBench), isHome: true);
        AddDetail(statistics, state.AwayLineup.Concat(state.AwayBench), isHome: false);

        await _matchRepository.AddStatisticsAsync(statistics, cancellationToken);
    }

    /// <summary>
    /// Writes one line per player who took part, because the session they are copied from
    /// is about to be thrown away.
    ///
    /// Who started is read off the substitution flags rather than off a list of eleven that
    /// no longer exists: a man who came on says so, a man who went off says so, and anybody
    /// still on the pitch says neither — which is exactly the eleven who kicked off, plus
    /// whoever replaced them. A player who was on the bench and never used has no flag set
    /// and is not in the eleven, so he does not get a line at all: an unused substitute
    /// did not appear, and a history that counted him would be counting a man in a suit.
    /// </summary>
    private async Task SavePlayerStatisticsAsync(
        Guid matchId,
        Guid? seasonId,
        MatchState state,
        CancellationToken cancellationToken)
    {
        var lines = new List<MatchPlayerStatistics>();

        void Collect(
            IEnumerable<MatchPlayerSnapshot> onPitch,
            IEnumerable<MatchPlayerSnapshot> bench,
            Guid teamId)
        {
            var pitch = onPitch.ToList();
            var names = pitch.Select(player => player.PlayerId).ToHashSet();

            // The minute the match ended, which is the only thing the snapshot cannot know for
            // itself: a man who never came off has no LeftAtMinute, and his ninety minutes are
            // measured to the whistle rather than to a stamp nobody wrote.
            var finalMinute = state.Minute;

            foreach (var player in pitch)
            {
                var line = MatchPlayerStatistics.Create(matchId, player.PlayerId, teamId, seasonId);
                line.ApplyFrom(player, started: !player.SubbedIn, finalMinute: finalMinute);
                lines.Add(line);
            }

            // Bench players who came on or were subbed off
            foreach (var player in bench.Where(player => player.SubbedIn || player.SubbedOff))
            {
                if (names.Contains(player.PlayerId))
                {
                    continue;
                }

                var line = MatchPlayerStatistics.Create(matchId, player.PlayerId, teamId, seasonId);
                line.ApplyFrom(player, started: !player.SubbedIn, finalMinute: finalMinute);
                lines.Add(line);
            }

            // Bench players who never played (unused substitutes)
            foreach (var player in bench.Where(player => !player.SubbedIn && !player.SubbedOff))
            {
                if (names.Contains(player.PlayerId))
                {
                    continue;
                }

                var line = MatchPlayerStatistics.Create(matchId, player.PlayerId, teamId, seasonId);
                line.ApplyFrom(player, started: false, wasOnBenchUnused: true, finalMinute: finalMinute);
                lines.Add(line);
            }
        }

        Collect(state.HomeLineup, state.HomeBench, state.HomeTeam.Id);
        Collect(state.AwayLineup, state.AwayBench, state.AwayTeam.Id);

        if (lines.Count > 0)
        {
            await _matchRepository.AddPlayerStatisticsAsync(lines, cancellationToken);
        }
    }

    /// <summary>
    /// Counts the own goals and the injuries on one side, from the players themselves rather
    /// than from a counter, so the number cannot drift from the eleven that produced it.
    /// </summary>
    private static void AddDetail(
        MatchStatistics statistics,
        IEnumerable<MatchPlayerSnapshot> players,
        bool isHome)
    {
        foreach (var player in players)
        {
            for (var i = 0; i < player.MatchOwnGoals; i++)
            {
                // An own goal is charged to the side that made the mistake, which is why it
                // is the side of the man and not the side that was attacking.
                statistics.AddOwnGoal(isHome);
            }
        }

        for (var i = 0; i < players.Count(player => player.InjuredOff); i++)
        {
            statistics.AddInjury(isHome);
        }
    }

    private static void AddCards(
        MatchStatistics statistics,
        MatchState state,
        IEnumerable<MatchPlayerSnapshot> players,
        bool isHome)
    {
        foreach (var player in players)
        {
            for (var i = 0; i < player.MatchYellowCards; i++)
            {
                statistics.AddCard(isHome, isRed: false);
            }

            if (player.RedCard)
            {
                statistics.AddCard(isHome, isRed: true);
            }
        }
    }

    /// <summary>
    /// Writes what the match did to each player back into his season state. The engine
    /// only works on snapshots, so this is the single point where goals, cards and the
    /// energy spent become season totals: without it the scorers table and the
    /// suspensions of a season would stay empty no matter how many matches were played.
    /// </summary>
    /// <summary>
    /// What the match was worth to the two clubs that played it, written into their books.
    ///
    /// It happens here, in the one place a match is ever finished, and the same place for a
    /// match a manager is watching and for one the engine plays with an empty stand: a ticket
    /// sold is a ticket sold, and the thirty-five clubs nobody manages are clubs too. The
    /// split of the gate and the season's first wage bill are the finance service's decision
    /// to make — this only tells it that a match is over.
    ///
    /// The gate is read off the match row rather than the live state, because the state is
    /// this tick's state and the row is the match's own record: a match that is replayed
    /// pays the same gate both times.
    ///
    /// The wage bill is paid for the championship and for nothing else. A cup tie and a
    /// supercup are competitions a club enters for the prize, and neither is a matchday of
    /// the league whose calendar the bill is spread over — so a cup run costs a club nothing
    /// in wages, which is what makes the cup a gamble a manager takes with his own money.
    /// </summary>
    private async Task SettleTheBooksAsync(Match match, CancellationToken cancellationToken)
    {
        var seasonId = await ResolveSeasonIdAsync(match.FixtureId, cancellationToken);

        await _financeService.RecordMatchGateAsync(
            match.FixtureId,
            match.Id,
            seasonId,
            match.HomeRevenue,
            match.AwayRevenue,
            cancellationToken);

        var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);
        if (fixture is null)
        {
            return;
        }

        // The report is written for every match, not only for a matchday of the championship.
        // A cup tie and a Supercup settle no wages and no gate share, so the two returns below
        // would take the manager past the news of the match he came to watch.
        await PostTheReportAsync(match, seasonId, cancellationToken);

        if (await IsChampionshipMatchAsync(fixture, cancellationToken) is false)
        {
            return;
        }

        var day = await _financeService.ResolveMatchDayAsync(match.FixtureId, cancellationToken);

        await _financeService.RecordMatchWagesAsync(
            fixture.HomeTeamId,
            seasonId,
            day,
            match.Id,
            cancellationToken);

        await _financeService.RecordMatchWagesAsync(
            fixture.AwayTeamId,
            seasonId,
            day,
            match.Id,
            cancellationToken);

        await _sponsorService.PayPerMatchAsync(
            fixture.HomeTeamId,
            match.Id,
            seasonId,
            day,
            cancellationToken);

        await _sponsorService.PayPerMatchAsync(
            fixture.AwayTeamId,
            match.Id,
            seasonId,
            day,
            cancellationToken);
    }

    /// <summary>
    /// Writes up the match for the club the manager is running.
    ///
    /// It is called from the settling of the books because that is where the match stops
    /// being something that is happening: the books are what say it is over. It is written
    /// here rather than in the inbox service because this is the one place that knows the
    /// eleven, the goals and the next fixture, and a report composed by somebody who had to
    /// go and look all of it up would be a second account of a match that only happened once.
    ///
    /// Only the manager's club is written to. The other side of the same fixture is a club
    /// with no manager, so it has nobody to deliver the report to, and the engine's own
    /// simulation of a match nobody is watching is not something a screen needs to be told.
    /// </summary>
    private async Task PostTheReportAsync(
        Match match,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        // The two clubs of the fixture are read together and the one somebody is running is
        // picked out of them. Asking the world which club has a manager would be a question
        // about the whole world to answer one about two clubs.
        var sides = await _teamRepository.ListByIdsAsync(
            [match.HomeTeamId, match.AwayTeamId], cancellationToken) ?? [];

        var club = sides.FirstOrDefault(side => side.IsManagerClub);
        if (club is null)
        {
            return;
        }

        var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);
        if (fixture is null)
        {
            return;
        }

        var context = await _matchContext.GetAsync(match.Id, cancellationToken);
        var isHome = match.HomeTeamId == club.Id;
        var opponent = sides.FirstOrDefault(side => side.Id != club.Id);

        // The club's own two numbers, once, because everything below reads them from the
        // club's side rather than from home and away: a report that mixed the two would put
        // a 0 x 2 next to the word "venceu" and mean it.
        var clubGoals = isHome ? match.HomeScore : match.AwayScore;
        var opponentGoals = isHome ? match.AwayScore : match.HomeScore;

        if (context is null || opponent is null)
        {
            return;
        }

        var lines = (await _matchRepository.ListPlayerStatisticsByMatchIdsAsync(
                [match.Id], cancellationToken))
            .Where(line => line.TeamId == club.Id)
            .ToList();

        // The names of every man the report can name, read in one go. A report that asked the
        // database for a name at a time would be a report about a match that had already been
        // played asking twenty-two questions to say something that is in one table.
        var people = await _teamRepository.GetPlayersAsync(
            lines.Select(line => line.PlayerId), cancellationToken);

        var events = (await _matchRepository.ListEventsAsync(match.Id, cancellationToken))
            .OrderBy(goal => goal.Sequence)
            .ToList();

        var statistics = await _matchRepository.GetStatisticsAsync(match.Id, cancellationToken);

        InboxPersonDto Who(Guid playerId) => people.TryGetValue(playerId, out var player)
            ? new InboxPersonDto { Id = player.Id, Name = player.Name, Kind = InboxMentionKind.Player }
            : new InboxPersonDto { Id = playerId, Name = string.Empty, Kind = InboxMentionKind.Player };

        var eleven = lines.Where(line => line.Started).Select(line => Who(line.PlayerId)).ToList();
        var substitutes = lines
            .Where(line => !line.Started && !line.WasOnBenchUnused)
            .Select(line => Who(line.PlayerId))
            .ToList();
        var booked = lines
            .Where(line => line.YellowCards > 0 || line.RedCards > 0)
            .Select(line => Who(line.PlayerId))
            .ToList();
        var scorers = lines
            .Where(line => line.Goals > 0 || line.OwnGoals > 0)
            .Select(line => Who(line.PlayerId))
            .ToList();

        var next = await ReadTheNextCommitmentAsync(club.Id, seasonId, cancellationToken);

        // The goals, as facts rather than as sentences, so the report can say who scored what
        // minute and whether it came from eleven metres. The narration each goal was announced
        // with travels separately and is quoted separately: the list is what a manager scans
        // and the narration is what a match said about itself.
        var goalFacts = ReadTheGoalsOf(events, people, match);

        var absence = ReadTheAbsenceOf(lines);

        var competition = await ReadTheCompetitionContextAsync(
            match, context, club.Id, cancellationToken);

        var facts = new MatchReportFacts
        {
            MatchId = match.Id,
            RecipientTeamId = club.Id,
            ClubId = club.Id,
            ClubName = club.Name,
            OpponentId = opponent.Id,
            OpponentName = opponent.Name,
            IsHome = isHome,
            CompetitionName = context.CompetitionName,
            PhaseName = context.PhaseName,
            LegLabel = context.LegLabel,
            MatchDayNumber = context.MatchDayNumber > 0 ? context.MatchDayNumber : null,
            ClubGoals = clubGoals,
            OpponentGoals = opponentGoals,
            ClubFormation = isHome
                ? statistics?.HomeFormation ?? string.Empty
                : statistics?.AwayFormation ?? string.Empty,
            Shape = MatchShapeRules.ShapeOf(club.Id, clubGoals, opponentGoals, goalFacts),
            Goals = goalFacts,
            Lineup = eleven,
            Substitutes = substitutes,
            Absence = absence,
            Competition = competition,
            GoalLines = events
                .Where(IsGoal)
                .Select(NarrationOf)
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .Select(description => description!)
                .ToList(),
            GoalScorers = scorers,
            Booked = booked,
            NextFixtureId = next?.FixtureId,
            NextOpponentId = next?.OpponentId,
            NextOpponentName = next?.OpponentName,
            NextMatchDayNumber = next?.MatchDayNumber,
            NextCompetitionName = next?.CompetitionName,
            NextStadiumName = next?.StadiumName,
            NextIsHome = next?.IsHome ?? false
        };

        await _inbox.PostMatchReportAsync(facts, cancellationToken);
    }

    /// <summary>
    /// Every goal of the match, in the order it happened, as facts rather than as prose.
    ///
    /// <para>
    /// The minute, the man and how the ball went in are all already on the event — they are
    /// what the event is for. Reading them back here rather than re-deriving them is what
    /// lets a report say "Ciclano, aos 28, de pênalti" and be right: the eleven metres came
    /// from the engine's own decision at the time, not from the report noticing that a penalty
    /// event was nearby.
    /// </para>
    ///
    /// <para>
    /// Both sides are included. A list of only the manager's own goals is half a match, and a
    /// comeback cannot be described without knowing when the other side went in front.
    /// </para>
    /// </summary>
    private static IReadOnlyList<MatchGoal> ReadTheGoalsOf(
        IReadOnlyList<MatchEvent> events,
        IReadOnlyDictionary<Guid, Domain.Players.Player> people,
        Match match)
    {
        var goals = new List<MatchGoal>();

        foreach (var goal in events.Where(IsGoal))
        {
            if (goal.TeamId is not { } teamId || goal.PlayerId is not { } playerId)
            {
                continue;
            }

            var name = people.TryGetValue(playerId, out var player)
                ? player.Name
                : goal.PlayerName ?? string.Empty;

            goals.Add(new MatchGoal(
                teamId,
                playerId,
                name,
                goal.Minute,
                KindOf(goal)));
        }

        return goals;
    }

    /// <summary>
    /// How a goal was scored, read out of the event's own payload.
    ///
    /// <para>
    /// Three kinds and not four: an own goal is a defender's mistake and never the scorer's
    /// credit, a penalty is a decision about how the ball went in, and everything else is a
    /// goal. A rebound is not among them because the engine does not store it as a fact of its
    /// own — it is inside the sentence the goal was announced with, and that sentence is
    /// quoted separately rather than flattened into a flag here.
    /// </para>
    /// </summary>
    private static GoalKind KindOf(MatchEvent goal) => goal.Type switch
    {
        MatchEventType.OwnGoalScored => GoalKind.OwnGoal,
        _ when ReadPenaltyFlag(goal.Payload) => GoalKind.Penalty,
        _ => GoalKind.Goal
    };

    private static bool ReadPenaltyFlag(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("fromPenalty", out var flag)
                && flag.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch (System.Text.Json.JsonException)
        {
            // A payload that will not parse is a broken record, not a reason to refuse the
            // report. A goal read as an ordinary goal is a report that says slightly less.
            return false;
        }
    }

    /// <summary>
    /// Who is out of the next match and for how long, worked out from the lines of this one.
    ///
    /// <para>
    /// The three causes are kept apart because a manager acts on them differently. A red is
    /// two matches and is already decided. Three yellows is also two matches, and it is
    /// decided too — the counter the engine keeps resets when it fires, so the next card
    /// starts a new count rather than adding to an old one. An injury is the one a manager
    /// cannot do anything about except replace the man, which is why it is the one the report
    /// says "infelizmente" about.
    /// </para>
    ///
    /// <para>
    /// A light knock is not here: the man stayed on and will be there next week, and a report
    /// that listed him as an absence would be telling a manager to change a team over a
    /// knock that did not take anybody off the pitch.
    /// </para>
    /// </summary>
    private static MatchAbsence? ReadTheAbsenceOf(IReadOnlyList<MatchPlayerStatistics> lines)
    {
        var absences = new List<PlayerAbsence>();

        foreach (var line in lines)
        {
            // The cards first, and only one of them: a man sent off straight has not also
            // "accumulated three yellows", and a report naming both reasons for the same
            // two-match ban is a report that counted the ban twice.
            if (line.RedCards > 0)
            {
                absences.Add(new PlayerAbsence
                {
                    PlayerId = line.PlayerId,
                    Cause = AbsenceCause.RedCard,
                    Matches = 2
                });

                continue;
            }

            if (line.YellowCards >= 3)
            {
                absences.Add(new PlayerAbsence
                {
                    PlayerId = line.PlayerId,
                    Cause = AbsenceCause.AccumulatedYellows,
                    Matches = 2
                });

                continue;
            }

            // Only a knock that took him off. A man who was hurt and stayed on will be there
            // next week, and a report that listed him as missing would be sending a manager
            // to replace a player who is fit.
            if (line.InjuredOff)
            {
                absences.Add(new PlayerAbsence
                {
                    PlayerId = line.PlayerId,
                    Cause = AbsenceCause.Injury,
                    Matches = line.InjuryMatchesOut
                });
            }
        }

        return absences.Count == 0
            ? null
            : new MatchAbsence { Players = absences };
    }

    /// <summary>
    /// Where the result left the club: the two positions, the two gaps and the rounds left.
    ///
    /// <para>
    /// The table is read twice from one collection rather than read twice from the database:
    /// the "before" table is the same table with this match's own result row taken out, which
    /// is the only way to say "saiu de décimo para sétimo" without a second set of results
    /// being asked for and possibly disagreeing with the first. The subtraction is one row
    /// out of a list already in memory, not a query.
    /// </para>
    ///
    /// <para>
    /// A cup tie and a Supercup have no table, and neither has a competition the club's
    /// division is not in. All three answer with a context that says so rather than with
    /// nothing, because a report that printed no position at all about a quarter-final would
    /// look like a bug rather than like a competition without a league.
    /// </para>
    ///
    /// <para>
    /// A failure here is a report without a table, never a report about no match: the window
    /// is caught and the match is still written up. A result a manager reads is worth more
    /// than a position he also gets.
    /// </para>
    /// </summary>
    private async Task<CompetitionContext?> ReadTheCompetitionContextAsync(
        Match match,
        MatchContextView context,
        Guid clubId,
        CancellationToken cancellationToken)
    {
        var roundsRemaining = (int?)Math.Max(0, context.TotalRounds - context.RoundNumber);
        var isLeague = context.CompetitionType == CompetitionType.League && context.Tier is not null;

        if (!isLeague)
        {
            return new CompetitionContext
            {
                Kind = context.CompetitionType,
                HasTable = false,
                RoundNumber = context.RoundNumber,
                RoundsRemaining = roundsRemaining,
                Advanced = context.TieResolved
                    ? context.TieWinnerTeamId == clubId
                    : null
            };
        }

        try
        {
            var edition = await _competitionRepository.GetSeasonViewByIdAsync(
                context.CompetitionSeasonId, cancellationToken);

            var collection = await _standings.CollectAsync(
                context.CompetitionSeasonId, edition, cancellationToken);

            if (edition is null || collection.Seeds.Count == 0)
            {
                return new CompetitionContext
                {
                    Kind = context.CompetitionType,
                    HasTable = false,
                    RoundNumber = context.RoundNumber,
                    RoundsRemaining = roundsRemaining
                };
            }

            // The "after" table is the one the world already has; the "before" is the same
            // table with this fixture's result row put back, which is the only way to name a
            // move without asking the database to disagree with itself.
            var thisResult = new MatchResultRow(
                match.HomeTeamId,
                match.AwayTeamId,
                match.HomeScore,
                match.AwayScore);

            var after = StandingTable.Build(collection.Seeds, collection.Finished);
            var before = StandingTable.Build(
                collection.Seeds,
                collection.Finished.Where(row => row != thisResult).ToList());

            var rowBefore = before.FirstOrDefault(entry => entry.TeamId == clubId);
            var rowAfter = after.FirstOrDefault(entry => entry.TeamId == clubId);

            if (rowBefore is null || rowAfter is null)
            {
                return null;
            }

            var tier = edition.Tier!.Value;

            return new CompetitionContext
            {
                Kind = context.CompetitionType,
                HasTable = true,
                PositionBefore = rowBefore.Position,
                PositionAfter = rowAfter.Position,
                PositionChange = rowBefore.Position - rowAfter.Position,
                Points = rowAfter.Points,
                PointsToPromotion = GapTo(tier, rowAfter, after, promotion: true),
                PointsToRelegation = GapTo(tier, rowAfter, after, promotion: false),
                RoundNumber = context.RoundNumber,
                RoundsRemaining = roundsRemaining
            };
        }
        catch (Exception error)
        {
            _logger.LogWarning(
                error,
                "Could not read the table around match {MatchId}; the report will be written without one.",
                match.Id);

            return new CompetitionContext
            {
                Kind = context.CompetitionType,
                HasTable = false,
                RoundNumber = context.RoundNumber,
                RoundsRemaining = roundsRemaining
            };
        }
    }

    /// <summary>
    /// How many points separate a club from the edge of the promotion places, or from the
    /// edge of the relegation places.
    ///
    /// <para>
    /// The gap is read to the *last* club in the band rather than to the first: "três pontos
    /// da classificação" has to mean "três pontos e você empata com o pior clube que sobe",
    /// because a manager planning the last five rounds is planning to be that club's rival,
    /// not to overtake a team two places above him.
    /// </para>
    ///
    /// <para>
    /// Null when the club is already inside the band, and null when the band is empty at that
    /// end of the pyramid: the top division sends nobody up and the bottom sends nobody
    /// down. A gap to nowhere is not a small gap, it is no gap at all, and the report stays
    /// silent about it rather than inventing a promotion race the season does not contain.
    /// </para>
    /// </summary>
    private static int? GapTo(
        int tier,
        StandingEntry row,
        IReadOnlyList<StandingEntry> table,
        bool promotion)
    {
        var clubs = table.Count;
        var lowestTier = CompetitionRules.DivisionCount;

        var slots = promotion
            ? tier > 1 ? CompetitionRules.PromotionSlots : 0
            : tier < lowestTier ? CompetitionRules.RelegationSlots : 0;

        if (clubs == 0 || slots == 0 || slots >= clubs)
        {
            return null;
        }

        if (promotion ? row.Position <= slots : row.Position > clubs - slots)
        {
            return null;
        }

        var edge = promotion ? table[slots - 1] : table[clubs - slots - 1];
        return Math.Max(0, edge.Points - row.Points);
    }

    /// <summary>
    /// The club's next commitment, resolved down to the ground it is played on.
    ///
    /// The matchday service knows which fixture is next and when — it is the reader of the
    /// calendar's waves — and the fixture knows the other club, so the three are put together
    /// here. The ground is the host's and the host is whoever is at home, because an away match
    /// is played on somebody else's pitch and a report that printed the manager's own stadium
    /// for it would be telling him the one thing he already knows is wrong.
    /// </summary>
    private async Task<NextCommitment?> ReadTheNextCommitmentAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var window = await _matchday.GetNextFixtureWindowAsync(teamId, seasonId, cancellationToken);
        if (window is null)
        {
            return null;
        }

        var fixture = await _fixtureRepository.GetAsync(window.FixtureId, cancellationToken);
        if (fixture is null)
        {
            return null;
        }

        var isHome = fixture.HomeTeamId == teamId;
        var sides = await _teamRepository.ListByIdsAsync(
            [fixture.HomeTeamId, fixture.AwayTeamId], cancellationToken);

        var host = sides.FirstOrDefault(side => side.Id == fixture.HomeTeamId);
        var opponent = sides.FirstOrDefault(side => side.Id != teamId);

        if (opponent is null)
        {
            return null;
        }

        return new NextCommitment
        {
            FixtureId = fixture.Id,
            OpponentId = opponent.Id,
            OpponentName = opponent.Name,
            MatchDayNumber = window.MatchDayNumber,
            CompetitionName = window.CompetitionName,
            StadiumName = host?.Stadium?.Name,
            IsHome = isHome
        };
    }

    /// <summary>
    /// Everything a report says about the match after this one, resolved.
    /// </summary>
    private sealed class NextCommitment
    {
        public Guid FixtureId { get; init; }
        public Guid OpponentId { get; init; }
        public string OpponentName { get; init; } = string.Empty;
        public int? MatchDayNumber { get; init; }
        public string? CompetitionName { get; init; }
        public string? StadiumName { get; init; }
        public bool IsHome { get; init; }
    }

    private async Task ApplySeasonProgressAsync(
        Guid fixtureId,
        MatchState state,
        int seed,
        CancellationToken cancellationToken)
    {
        var seasonId = await ResolveSeasonIdAsync(fixtureId, cancellationToken);

        var players = state.HomeLineup
            .Concat(state.HomeBench)
            .Concat(state.AwayLineup)
            .Concat(state.AwayBench)
            .GroupBy(player => player.PlayerId)
            .Select(group => group.First())
            .ToList();

        // What a rest before the next match is worth is drawn from the match's own seed, so
        // a match that is replayed recovers exactly the same way the first time. Rotating a
        // squad is a decision, and a decision the engine makes for the opposition has to be
        // as reproducible as everything else it does.
        var recovery = new DeterministicRandomSource(seed);

        // The men who were not in the day's squad are recovered as well, and they are the
        // ones who recover most. A player who was not even on the bench had a day off, and a
        // day off is worth more than a cold evening in a suit — so a squad is not punished for
        // resting its third-choice centre back, which is the whole point of having a squad.
        var played = new HashSet<Guid>(players.Select(player => player.PlayerId));
        var rested = new List<Guid>();

        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken);

        if (fixture is not null)
        {
            foreach (var teamId in new[] { fixture.HomeTeamId, fixture.AwayTeamId })
            {
                var squad = await _teamRepository.GetSquadAsync(teamId, seasonId, cancellationToken);

                foreach (var membership in squad)
                {
                    if (played.Add(membership.PlayerId))
                    {
                        rested.Add(membership.PlayerId);
                    }
                }
            }
        }

        foreach (var playerId in rested)
        {
            var seasonState = await _playerRepository.GetSeasonStateForUpdateAsync(
                playerId, seasonId, cancellationToken);

            if (seasonState is null)
            {
                continue;
            }

            // A man who did not travel has no minutes to be paid for and no snapshot to read
            // an age or a stamina from, so his window is the band on its own. Both are body
            // facts the caller would have to query for, and a query per player at the end of
            // each of the thirty-four matches of a matchday moves a number nobody is reading.
            seasonState.RecoverEnergy(EnergyRecoveryRules.Recovery(
                WindowEffort.NoMatch, minutesPlayed: 0, age: null, recovery));
            _playerRepository.UpdateSeasonState(seasonState);
        }

        foreach (var player in players)
        {
            var seasonState = await _playerRepository.GetSeasonStateForUpdateAsync(player.PlayerId, seasonId, cancellationToken);
            if (seasonState is null)
            {
                continue;
            }

            for (var goal = 0; goal < player.MatchGoals; goal++)
            {
                seasonState.AddGoal();
            }

            for (var save = 0; save < player.MatchSaves; save++)
            {
                seasonState.AddSave();
            }

            for (var card = 0; card < player.MatchYellowCards; card++)
            {
                seasonState.AddYellowCard();
            }

            if (player.RedCard)
            {
                seasonState.AddRedCard();
            }

            // An injury that took him off the pitch becomes an absence the player carries
            // into the next ones, for as long as the engine decided it would. A light knock
            // he played through costs him energy and nothing else.
            if (player.InjuredOff)
            {
                seasonState.AddInjury(player.Injury, player.InjuryMatchesOut);
            }

            // The energy the match cost is what the player carries into the next one, plus
            // what resting until it is worth playing again. Playing a match is worth far
            // less than sitting one out, and the two bands do not overlap: that gap is the
            // whole reason a squad is rotated at all.
            // Three answers and not two. He played, he was in the squad and did not play, or
            // he was not in the squad at all — and the last is worth more than the second,
            // because a day off is not a bench. The bands do not overlap, which is the whole
            // reason a squad is rotated at all.
            var effort = player.PlayedInMatch
                ? WindowEffort.Played(clubPlayed: true)
                : WindowEffort.SatOut(clubPlayed: true);

            // The minutes he was actually out there, and his age: a recovery band is what a
            // full match earns, and he did not play a full match.
            var minutes = player.MinutesPlayed(state.Minute);

            seasonState.SetEnergy(player.Energy + EnergyRecoveryRules.Recovery(
                effort, minutes, player.Age, recovery, player.Stamina));

            _playerRepository.UpdateSeasonState(seasonState);
        }
    }

    private static MatchResultView ToResult(MatchState state) => new()    {
        MatchId = state.MatchId,
        HomeScore = state.HomeScore,
        AwayScore = state.AwayScore,
        HomeShots = state.HomeShots,
        AwayShots = state.AwayShots,
        HomeShotsOnTarget = state.HomeShotsOnTarget,
        AwayShotsOnTarget = state.AwayShotsOnTarget,
        HomeCorners = state.HomeCorners,
        AwayCorners = state.AwayCorners,
        HomeCards = state.HomeCards,
        AwayCards = state.AwayCards,
        HomeFouls = state.HomeFouls,
        AwayFouls = state.AwayFouls,
        HomePossession = state.HomePossession,
        AwayPossession = state.AwayPossession,
        FormationHome = state.HomeFormation.ToString(),
        FormationAway = state.AwayFormation.ToString(),
        SubstitutionsHome = state.SubstitutionsHome,
        SubstitutionsAway = state.SubstitutionsAway
    };

    private async Task<Match> GetMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        await _matchRepository.GetAsync(matchId, cancellationToken)
        ?? throw new EntityNotFoundException("Match", matchId);

    /// <summary>
    /// The match row is read without tracking, so every command has to mark it as
    /// modified before committing; otherwise the clock and the score would only live
    /// in the engine's memory and a reconnect would see a stale match.
    /// </summary>
    private async Task CommitAsync(Match match, CancellationToken cancellationToken)
    {
        _matchRepository.Update(match);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static MatchCommandResult Refused(Guid matchId, string reason) =>
        new() { Accepted = false, MatchId = matchId, ErrorMessage = reason };

    /// <summary>
    /// The tie this fixture is a leg of, when it is the second leg of one and the first leg
    /// has been played.
    ///
    /// The engine needs the first leg's goals because a second leg is decided by the
    /// aggregate, and the Laws of a knockout tie say the aggregate is level and nothing else:
    /// no extra time, and penalties. A match that is not told what it is trying to settle
    /// cannot settle it, so this is read here — once, at kick-off, from the first leg's own
    /// row — and handed to the engine as a fact rather than looked up again by the rules.
    ///
    /// Null for everything else: a championship match is not a cup match, and a first leg is
    /// half an answer that decides nothing.
    /// </summary>
    private async Task<CupTieFacts?> ResolveCupTieAsync(
        Guid fixtureId,
        CancellationToken cancellationToken)
    {
        var tie = await _cupProgression.GetTieForLegAsync(fixtureId, cancellationToken);

        if (tie is null
            || tie.SecondLegFixtureId != fixtureId
            || tie.FirstLegFixtureId is not { } firstLegFixtureId)
        {
            return null;
        }

        var firstLeg = await _matchRepository.GetByFixtureAsync(firstLegFixtureId, cancellationToken);

        if (firstLeg is null || !firstLeg.IsFinished)
        {
            // A second leg played before its first one is not a tie being decided, and the
            // engine must not be handed half of a question.
            return null;
        }

        return CupTieFacts.ForSecondLeg(
            tie.Id,
            tie.HomeTeamId,
            tie.AwayTeamId,
            firstLeg.HomeScore,
            firstLeg.AwayScore);
    }

    private async Task<Guid> ResolveSeasonIdAsync(Fixture fixture, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken)
            ?? throw new EntityNotFoundException("Round", fixture.RoundId);

        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(
            round.CompetitionSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", round.CompetitionSeasonId);

        return competitionSeason.SeasonId;
    }

    /// <summary>
    /// The competition a fixture is a match of, and the window of the matchday it is played
    /// in. Both are read off the round the fixture belongs to and are never assumed: a match
    /// that believed it was in a league because that is what a match usually is would put a
    /// cup leg into the table and take the gate at the wrong price.
    ///
    /// A fixture whose round names a window that does not exist is corrected to the window
    /// its own competition implies, because a round written before the cup existed still has
    /// to produce a match the cup's rules apply to.
    /// </summary>
    private async Task<(CompetitionType Type, int Window)> ResolveCompetitionAsync(
        Fixture fixture,
        CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken);
        if (round is null)
        {
            return (CompetitionType.League, CompetitionRules.ChampionshipWindow);
        }

        var season = await _competitionRepository.GetSeasonByIdAsync(round.CompetitionSeasonId, cancellationToken);
        if (season is null)
        {
            return (CompetitionType.League, CompetitionRules.ChampionshipWindow);
        }

        var competition = await _competitionRepository.GetAsync(season.CompetitionId, cancellationToken);
        if (competition is null)
        {
            return (CompetitionType.League, CompetitionRules.ChampionshipWindow);
        }

        var expected = competition.Type == CompetitionType.League
            ? CompetitionRules.ChampionshipWindow
            : CompetitionRules.CupWindow;

        return (competition.Type, round.Window == expected ? round.Window : expected);
    }

    /// <summary>
    /// Whether a fixture is a match of the championship, which is the only competition whose
    /// matchdays a club's wages are settled on.
    /// </summary>
    private async Task<bool> IsChampionshipMatchAsync(Fixture fixture, CancellationToken cancellationToken)
    {
        var competition = await ResolveCompetitionAsync(fixture, cancellationToken);

        return competition.Type == CompetitionType.League;
    }

    private async Task<Guid> ResolveSeasonIdAsync(Guid fixtureId, CancellationToken cancellationToken)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        return await ResolveSeasonIdAsync(fixture, cancellationToken);
    }

    /// <summary>
    /// Picks the starting eleven and the bench of a club: the strongest available
    /// players, one goalkeeper in the eleven and at least one on the bench if available.
    /// If no goalkeeper is available, an outfield player is designated as emergency GK.
    /// </summary>
    private async Task<SquadSelection> BuildSquadAsync(
        Team team,
        Guid seasonId,
        Tactic? tactic = null,
        CancellationToken cancellationToken = default)
    {
        var states = (await _playerRepository.ListSeasonStatesAsync(seasonId, team.Id, cancellationToken))
            .Where(state => state.IsAvailable)
            .ToList();

        if (states.Count == 0)
        {
            return SquadSelection.Empty(team);
        }

        var players = (await _playerRepository.ListAsync(cancellationToken))
            .ToDictionary(player => player.Id);

        var snapshots = new List<MatchPlayerSnapshot>(states.Count);
        foreach (var state in states)
        {
            if (players.TryGetValue(state.PlayerId, out var player))
            {
                snapshots.Add(MatchPlayerSnapshot.FromPlayerSeasonState(player, state));
            }
        }

        var starters = SelectAutomaticEleven(snapshots, tactic);

        // A club with fewer than eleven available players still needs eleven names, and a
        // reserve goalkeeper is better than an incomplete eleven.
        if (starters.Count < SquadSize)
        {
            starters.AddRange(snapshots
                .Where(player => !starters.Contains(player))
                .OrderByDescending(player => PlayerMetric.Metric(player))
                .Take(SquadSize - starters.Count));
        }

        var bench = SelectAutomaticBench(snapshots, starters);

        // Whatever the manager did not pick is what he gets to look at, so both lists are
        // read in the same order: position first, name inside the position.
        var orderedStarters = starters
            .OrderBy(player => PositionOrder.Of(player.Position))
            .ThenBy(player => player.EmergencyGK ? 1 : 0)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var orderedBench = bench
            .OrderBy(player => PositionOrder.Of(player.Position))
            .ThenBy(player => player.EmergencyGK ? 1 : 0)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SquadSelection(team, orderedStarters, orderedBench, snapshots);
    }

    /// <summary>
    /// Selects the bench of 7 players, ensuring at least one goalkeeper if available.
    /// </summary>
    private static List<MatchPlayerSnapshot> SelectAutomaticBench(
        List<MatchPlayerSnapshot> available,
        List<MatchPlayerSnapshot> starters)
    {
        var remaining = available.Where(player => !starters.Contains(player)).ToList();
        
        var bench = new List<MatchPlayerSnapshot>();

        // First, ensure at least one goalkeeper on bench if available
        var availableGoalkeepers = remaining.Where(p => p.Position == Position.GK).ToList();
        if (availableGoalkeepers.Count > 0)
        {
            var benchKeeper = availableGoalkeepers
                .OrderByDescending(p => PlayerMetric.KeeperAbility(p))
                .First();
            bench.Add(benchKeeper);
            remaining.Remove(benchKeeper);
        }

        // Fill the rest of the bench with best available players
        bench.AddRange(remaining
            .OrderByDescending(p => PlayerMetric.Metric(p))
            .Take(BenchSize - bench.Count));

        return bench;
    }

    /// <summary>
    /// The eleven a club puts out when nobody is choosing it: the best goalkeeper, and then
    /// the best players for the shape the club is actually made of.
    ///
    /// This used to be the eleven best men by a single sum of attributes, and it was wrong
    /// in two ways at once. It put reserve goalkeepers on the pitch, because a keeper's
    /// attributes are high by design; and it put the same eleven out whatever the club was
    /// made of, so a club of eight defenders and two forwards played exactly like its
    /// neighbour and the formation meant nothing. Reading the shape off the squad and then
    /// filling it by position is what makes a formation a fact about a team.
    /// </summary>
    private static List<MatchPlayerSnapshot> SelectAutomaticEleven(
        List<MatchPlayerSnapshot> available,
        Tactic? tactic)
    {
        var starters = new List<MatchPlayerSnapshot>(SquadSize);

        // The eleven has room for one goalkeeper. The other keepers of the roster are cover
        // for an injury or a red card.
        var keeper = available
            .Where(player => player.Position == Position.GK)
            .OrderByDescending(player => PlayerMetric.KeeperAbility(player))
            .FirstOrDefault();

        if (keeper is not null)
        {
            starters.Add(keeper);
        }
        else
        {
            // Emergency goalkeeper: no GK available, pick the best outfield player
            // with highest reflexes/goalkeeper power as emergency GK
            var emergencyGk = available
                .OrderByDescending(p => p.Reflexes + p.GoalkeeperPower)
                .FirstOrDefault();
            
            if (emergencyGk is not null)
            {
                emergencyGk.PromoteToGoalkeeper();
                starters.Add(emergencyGk);
            }
        }

        // The shape the club is made of, scaled to ten, is what it plays when nobody ordered
        // anything else. A tactic replaces it: the manager names the shape and each line is
        // filled with the men who are best at that line's job, which is why a 4-2-3-1 and
        // a 4-4-2 are not the same eleven drawn twice.
        var shape = tactic is { IsComplete: true } ? tactic : Tactics.FromSquad(available);

        var remaining = available
            .Where(player => player.Position != Position.GK && !starters.Contains(player))
            .ToList();

        // A club's roster does not always have four defenders. Asking for four and finding
        // three is not a reason to leave a seat empty, so each line falls back to any man
        // left rather than the eleven coming out with ten names in it.
        foreach (var line in shape.Lines.Where(candidate => candidate.Position != Position.GK))
        {
            var places = line.Count;

            starters.AddRange(remaining
                .Where(player => player.Position == line.Position)
                .OrderByDescending(player => PlayerMetric.TacticalMetric(player, line.Profile))
                .Take(places));

            remaining.RemoveAll(starters.Contains);
        }

        starters.AddRange(remaining
            .OrderByDescending(player => PlayerMetric.Metric(player))
            .Take(SquadSize - starters.Count));

        return starters;
    }

    /// <summary>
    /// Counts one match off the absence of every player of both clubs who could not be
    /// picked. Whoever has no counter left is available again, which is what makes a
    /// two match suspension a suspension and not a season long ban.
    /// </summary>
    private async Task ServeAbsencesAsync(
        Team homeTeam,
        Team awayTeam,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        foreach (var teamId in new[] { homeTeam.Id, awayTeam.Id })
        {
            var states = await _playerRepository.ListSeasonStatesAsync(seasonId, teamId, cancellationToken);

            foreach (var state in states.Where(state => !state.IsAvailable))
            {
                state.RecoverFromMatches();
                _playerRepository.UpdateSeasonState(state);
            }
        }
    }

    /// <summary>
    /// Applies the manager's chosen eleven and optionally bench. The backend is the authority on the lineup
    /// rules: exactly eleven players, all of them available for this club, and exactly
    /// one effective goalkeeper. If benchIds is provided and not empty, the bench must have
    /// at most 7 players, with at least one goalkeeper if available. Any other shape of
    /// defence, midfield or attack is deliberately allowed.
    /// </summary>
    private static SquadSelection SelectStartingEleven(
        SquadSelection squad,
        IReadOnlyCollection<Guid> starterIds,
        IReadOnlyCollection<Guid>? benchIds = null)
    {
        var chosen = starterIds.Distinct().ToList();

        if (chosen.Count != SquadSize)
        {
            throw new DomainValidationException(
                "InvalidLineup",
                $"O time titular precisa de exatamente {SquadSize} jogadores.");
        }

        var available = squad.All.ToDictionary(player => player.PlayerId);
        var eleven = new List<MatchPlayerSnapshot>(SquadSize);

        foreach (var playerId in chosen)
        {
            if (!available.TryGetValue(playerId, out var player))
            {
                throw new DomainValidationException(
                    "PlayerNotAvailable",
                    "Um dos jogadores selecionados não está disponível neste clube.");
            }

            eleven.Add(player);
        }

        var goalkeepers = eleven.Count(player => player.Position == Position.GK);
        if (goalkeepers != 1)
        {
            throw new DomainValidationException(
                "GoalkeeperRequired",
                goalkeepers == 0
                    ? "O time titular precisa de exatamente um goleiro."
                    : "O time titular não pode ter mais de um goleiro.");
        }

        var bench = new List<MatchPlayerSnapshot>();

        // If bench is explicitly provided, validate it
        if (benchIds is { Count: > 0 })
        {
            var chosenBench = benchIds.Distinct().ToList();

            if (chosenBench.Count > BenchSize)
            {
                throw new DomainValidationException(
                    "InvalidBench",
                    $"O banco de reservas pode ter no máximo {BenchSize} jogadores.");
            }

            foreach (var playerId in chosenBench)
            {
                if (!available.TryGetValue(playerId, out var player))
                {
                    throw new DomainValidationException(
                        "PlayerNotAvailable",
                        "Um dos reservas selecionados não está disponível neste clube.");
                }

                if (eleven.Contains(player))
                {
                    throw new DomainValidationException(
                        "PlayerAlreadySelected",
                        "Um jogador não pode estar no time titular e no banco ao mesmo tempo.");
                }

                if (bench.Contains(player))
                {
                    throw new DomainValidationException(
                        "DuplicateBenchPlayer",
                        "O mesmo jogador não pode ser selecionado duas vezes para o banco.");
                }

                bench.Add(player);
            }

            // Ensure at least one goalkeeper on bench if possible
            var benchGoalkeepers = bench.Count(player => player.Position == Position.GK);
            var availableGoalkeepers = squad.All.Count(player => player.Position == Position.GK && !eleven.Contains(player));
            
            if (benchGoalkeepers == 0 && availableGoalkeepers > 0)
            {
                throw new DomainValidationException(
                    "GoalkeeperRequiredOnBench",
                    "O banco de reservas precisa de pelo menos um goleiro.");
            }
        }

        // If bench has fewer than BenchSize players (or wasn't provided), fill with best available
        if (bench.Count < BenchSize)
        {
            var remaining = squad.All
                .Where(player => !eleven.Contains(player) && !bench.Contains(player))
                .OrderByDescending(player => PlayerMetric.Metric(player))
                .Take(BenchSize - bench.Count)
                .ToList();
            
            bench.AddRange(remaining);
        }

        // Order both lists by position then name, with emergency goalkeepers last in their position group
        var orderedEleven = eleven
            .OrderBy(player => PositionOrder.Of(player.Position))
            .ThenBy(player => player.EmergencyGK ? 1 : 0)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var orderedBench = bench
            .OrderBy(player => PositionOrder.Of(player.Position))
            .ThenBy(player => player.EmergencyGK ? 1 : 0)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SquadSelection(squad.Team, orderedEleven, orderedBench, squad.All);
    }

    private static int OverallRating(MatchPlayerSnapshot player) =>
        player.Speed + player.Accuracy + player.Dribbling + player.Heading + player.Strength
        + player.GoalkeeperPower + player.Reflexes;

    /// <summary>
    /// Answers a standing order against the squad as it is on the day of the match.
    ///
    /// <para>
    /// The order is a statement made days ago about men who have since been suspended, injured
    /// or sent away. It is applied as far as it still holds: every man the manager named who is
    /// available today keeps his place, in the manager's own order, and the gaps are filled
    /// from the eleven the staff would have picked — which is the best available man for the
    /// shape, rather than any man at all.
    /// </para>
    ///
    /// <para>
    /// It never refuses, and that is the whole difference from <see cref="SelectStartingEleven"/>.
    /// An order that arrives with the request is a person standing there being told no, and
    /// being told no is right. A standing order is nobody standing there: a match that cannot
    /// start because a left-back named a suspended man eight days ago is a match the world
    /// stops for, and the world does not stop.
    /// </para>
    /// </summary>
    private static SquadSelection ApplyTheStandingOrder(
        SquadSelection squad,
        IReadOnlyCollection<Guid> starterIds,
        IReadOnlyCollection<Guid>? benchIds)
    {
        var available = squad.All.ToDictionary(player => player.PlayerId);

        // The eleven the staff would have picked is the fallback and not a second opinion: a
        // man who has to come in is a man the manager would have chosen had he known, and the
        // staff's eleven is the club's own answer to that.
        var fallback = new List<MatchPlayerSnapshot>(squad.Lineup);

        var eleven = starterIds
            .Select(id => available.GetValueOrDefault(id))
            .Where(player => player is not null)
            .Cast<MatchPlayerSnapshot>()
            .Take(SquadSize)
            .ToList();

        eleven.AddRange(fallback
            .Where(player => !eleven.Contains(player))
            .Take(SquadSize - eleven.Count));

        // One goalkeeper is the rule that survives everything, because a club that plays with
        // nobody in goal is punished for a suspension rather than for football. The keeper is
        // taken from the bench before an outfielder is promoted into the eleven.
        if (eleven.Count(player => player.Position == Position.GK) == 0)
        {
            var keeper = squad.All
                .Where(player => player.Position == Position.GK && !eleven.Contains(player))
                .OrderByDescending(player => PlayerMetric.KeeperAbility(player))
                .FirstOrDefault();

            if (keeper is not null)
            {
                var displaced = eleven.LastOrDefault(player => player.Position != Position.GK);
                if (displaced is not null)
                {
                    eleven.Remove(displaced);
                }

                eleven.Add(keeper);
            }
        }

        // The bench is the manager's bench as far as it holds, and the rest of the squad after
        // it — the same filling rule the automatic bench uses, so a plan with a short bench is
        // a plan about the men who matter rather than a refusal.
        var bench = (benchIds ?? [])
            .Select(id => available.GetValueOrDefault(id))
            .Where(player => player is not null && !eleven.Contains(player))
            .Cast<MatchPlayerSnapshot>()
            .Take(BenchSize)
            .ToList();

        bench.AddRange(squad.All
            .Where(player => !eleven.Contains(player) && !bench.Contains(player))
            .OrderByDescending(player => PlayerMetric.Metric(player))
            .Take(BenchSize - bench.Count));

        return new SquadSelection(
            squad.Team,
            OrderForTheLineup(eleven),
            OrderForTheLineup(bench),
            squad.All);
    }

    /// <summary>
    /// Puts a list of men in the order a formation strip reads: by position, and inside a
    /// position by name, with the men promoted to goal last.
    /// </summary>
    private static List<MatchPlayerSnapshot> OrderForTheLineup(IEnumerable<MatchPlayerSnapshot> players) =>
        players
            .OrderBy(player => PositionOrder.Of(player.Position))
            .ThenBy(player => player.EmergencyGK ? 1 : 0)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static TeamInfo ToTeamInfo(Team team, double stars) =>
        new(team.Id, team.Name, team.ShortName, team.PrimaryColor, team.SecondaryColor, (int)Math.Round(stars * 20));

    private static Team ToTeam(Team? team) =>
        team ?? throw new EntityNotFoundException("Team", Guid.Empty);

    private static int ResolveUserTeamIndex(Match match, Guid? userTeamId)
    {
        if (userTeamId is null)
        {
            return 0;
        }

        return userTeamId == match.HomeTeamId ? 0 : 1;
    }

    private static List<MatchEngineEvent> DrainFeed(MatchState state)
    {
        var events = state.PendingFeed.ToList();
        state.PendingFeed.Clear();
        return events;
    }

    /// <summary>
    /// Builds an event for a command the client issued, so substitutions and penalty
    /// takers show up in the same ordered feed as the engine's own events.
    /// </summary>
    private static MatchEngineEvent EmitEvent(
        MatchState state,
        MatchEventType type,
        Guid? teamId,
        Guid? playerId,
        string description) =>
        new(
            ++state.Sequence,
            state.Minute,
            type,
            teamId,
            playerId,
            state.HomeScore,
            state.AwayScore,
            description,
            type switch
            {
                MatchEventType.SubstitutionMade => "substitution",
                MatchEventType.PenaltyTaken => "penalty",
                _ => "info"
            });

    /// <summary>
    /// Mirrors the engine's working memory onto the persisted match: the clock, the
    /// score and the event sequence, all of which the engine owns while the match runs.
    /// </summary>
    private static void ApplyToMatch(Match match, MatchState state, IReadOnlyList<MatchEngineEvent> events)
    {
        match.ApplyEngineState(
            state.Minute,
            state.HomeScore,
            state.AwayScore,
            state.Sequence,
            HalfOf(state));
    }

    /// <summary>
    /// Which half the row is in, read off the engine rather than off the clock.
    ///
    /// A tie that goes to the spot is in neither half: the state says so, and the row has
    /// to say it too, because a cup leg decided on penalties is a fact about the match and
    /// a results screen that calls it a second-half finish has thrown the penalties away.
    /// </summary>
    private static MatchHalf HalfOf(MatchState state) => state.Half switch
    {
        1 => MatchHalf.Second,
        2 => MatchHalf.ExtraTime,
        3 => MatchHalf.PenaltyShootout,
        _ => MatchHalf.First
    };

    /// <summary>
    /// Writes the events produced by a tick into the match log, so a client that
    /// reconnects can replay the whole match from the REST snapshot.
    /// </summary>
    private async Task PersistEventsAsync(
        Match match,
        IReadOnlyList<MatchEngineEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var persisted = events
            .Select(engineEvent => MatchEvent.FromEngineEvent(engineEvent, match.Id))
            .ToList();

        await _matchRepository.AddEventsAsync(persisted, cancellationToken);
    }

    private static MatchStateView ToView(MatchState state, MatchStatus status, Match match) => new()
    {
        MatchId = state.MatchId,
        HomeScore = state.HomeScore,
        AwayScore = state.AwayScore,
        HomeSaves = state.HomeSaves,
        AwaySaves = state.AwaySaves,
        Minute = state.Minute,
        Second = state.Seconds,
        Half = state.Half,
        Sequence = state.Sequence,
        StoppageTimeMinutes = state.StoppageMinutes,
        Status = status.ToString(),
        IsPaused = state.Paused,
        IsHalfTime = state.HalfTimePauseActive,
        IsFinished = state.MatchFinished,
        Speed = state.Speed,
        HomeShots = state.HomeShots,
        AwayShots = state.AwayShots,
        HomeShotsOnTarget = state.HomeShotsOnTarget,
        AwayShotsOnTarget = state.AwayShotsOnTarget,
        HomeCorners = state.HomeCorners,
        AwayCorners = state.AwayCorners,
        HomeCards = state.HomeCards,
        AwayCards = state.AwayCards,
        HomeFouls = state.HomeFouls,
        AwayFouls = state.AwayFouls,
        HomePossession = state.HomePossession,
        AwayPossession = state.AwayPossession,
        SubstitutionsUsedHome = state.SubstitutionsHome,
        SubstitutionsUsedAway = state.SubstitutionsAway,
        PenaltyAwaitingSelection = state.PenaltyAwaitingSelection,
        Injury = InjuryViewFor(state),
        PossessionTeam = state.PossessionTeam,
        PossessionPlayerId = state.PossessionPlayerId,
        FormationHome = state.HomeFormation.ToString(),
        FormationAway = state.AwayFormation.ToString(),
        Attendance = match.Attendance,
        GateRevenue = match.Gate.GrossRevenue,
        Penalty = PenaltyOptionsFor(state),
        Shootout = ShootoutViewFor(state),
        UserTeamId = state.ManagerTeamId
    };

    /// <summary>
    /// The injury a manager has to act on: who cannot continue, for which club, and how
    /// bad it is. He is still in the eleven at this point, because the change that takes
    /// him off is the change he has not made yet.
    /// </summary>
    private static MatchInjuryView InjuryViewFor(MatchState state)
    {
        if (!state.InjuryAwaitingSubstitution || state.InjuryPlayerId is not { } playerId)
        {
            return new MatchInjuryView();
        }

        var isHome = state.InjuryTeam == 1;
        var lineup = isHome ? state.HomeLineup : state.AwayLineup;

        return new MatchInjuryView
        {
            AwaitingSubstitution = true,
            PlayerId = playerId,
            PlayerName = lineup.FirstOrDefault(player => player.PlayerId == playerId)?.Name,
            Team = state.InjuryTeam,
            Severity = Injury.Grave
        };
    }

    /// <summary>
    /// The shootout as the match stands in it, and null when it is not in one.
    ///
    /// The pool of men the manager may name is the eleven that finished the match, which is
    /// what the engine worked out at the final whistle; a screen that offered the whole squad
    /// would be offering him a striker who played the first leg of a tie he is not playing.
    /// </summary>
    private static ShootoutView? ShootoutViewFor(MatchState state)
    {
        if (state.Shootout is not { } shootout)
        {
            return null;
        }

        var managerIsHome = state.ManagerTeamId is { } managerTeamId
            && state.HomeTeam.Id == managerTeamId;

        var nextIsHome = shootout.NextTeamIsHome;
        var nextTaker = nextIsHome is null ? null : shootout.NextTaker(nextIsHome.Value);

        return new ShootoutView
        {
            HomeTeamId = shootout.HomeTeamId,
            AwayTeamId = shootout.AwayTeamId,
            HomeTakesFirst = shootout.HomeTakesFirst,
            NextTeamId = nextIsHome is null
                ? null
                : nextIsHome.Value ? shootout.HomeTeamId : shootout.AwayTeamId,
            NextTakerId = nextTaker,
            HomeGoals = shootout.HomeGoals,
            AwayGoals = shootout.AwayGoals,
            HomeKicksTaken = shootout.HomeKicksTaken,
            AwayKicksTaken = shootout.AwayKicksTaken,
            IsSuddenDeath = shootout.IsSuddenDeath,
            IsComplete = shootout.IsComplete,
            WinnerTeamId = shootout.IsComplete ? shootout.WinnerTeamId : null,
            AwaitingOrder = state.ShootoutAwaitingOrder,
            Candidates = CandidatesFor(state, managerIsHome),
            DefendingGoalkeeper = (managerIsHome ? state.AwayLineup : state.HomeLineup)
                .FirstOrDefault(player => player.KeepsGoal),
            HomeTakers = shootout.HomeTakers,
            AwayTakers = shootout.AwayTakers,
            Kicks = shootout.Kicks
                .Select(kick => new ShootoutKickView
                {
                    TeamId = kick.TeamId,
                    TakerId = kick.TakerId,
                    Scored = kick.Scored
                })
                .ToList()
        };
    }

    /// <summary>
    /// The men the manager may name, as the match knows them, in the order the engine read
    /// them: the best taker first.
    ///
    /// They are looked up in the eleven that finished the match rather than taken from the
    /// pool of ids, because the id list is the order and the eleven is who those men are.
    /// </summary>
    private static IReadOnlyList<MatchPlayerSnapshot> CandidatesFor(
        MatchState state,
        bool managerIsHome)
    {
        var pool = managerIsHome ? state.HomeShootoutTakers : state.AwayShootoutTakers;
        var lineup = managerIsHome ? state.HomeLineup : state.AwayLineup;

        return pool
            .Select(id => lineup.FirstOrDefault(player => player.PlayerId == id))
            .Where(player => player is not null)
            .Select(player => player!)
            .ToList();
    }

    /// <summary>
    /// The penalty a manager has to act on, with the players he can name. The opponent's
    /// penalty is resolved by the engine itself, so it is never offered to anybody.
    /// </summary>
    private static PenaltyTakerOptions PenaltyOptionsFor(MatchState state)
    {
        if (!state.PenaltyAwaitingSelection || state.ManagerTeamId is not { } managerTeamId)
        {
            return new PenaltyTakerOptions { MatchIsLive = true, AwaitingSelection = false };
        }

        var isHome = state.HomeTeam.Id == managerTeamId;

        if (isHome ? state.PenaltyTeam != 1 : state.PenaltyTeam != 2)
        {
            return new PenaltyTakerOptions { MatchIsLive = true, AwaitingSelection = false };
        }

        return new PenaltyTakerOptions
        {
            MatchIsLive = true,
            AwaitingSelection = true,
            Candidates = MatchEngine.PenaltyTakerCandidates(state, isHome),
            DefendingGoalkeeper = (isHome ? state.AwayLineup : state.HomeLineup)
                .FirstOrDefault(player => player.KeepsGoal)
        };
    }

    /// <summary>
    /// The round as a manager reads it the morning after: what each match was, and how it
    /// came to be.
    ///
    /// The summary is not written here. It is the goals' own narration, in the order they
    /// happened, taken from the events the match already recorded — a match that says
    /// "Balançou a rede!" at minute 12 has said it, and a report that paraphrased it would
    /// be a second account of a match that only happened once. That is also why there is no
    /// random draw in this method: the words a match uses are part of its identity, and
    /// re-rolling them for a report would give the same fixture two different accounts of
    /// the same afternoon.
    /// </summary>
    public async Task<MatchdayReport> GetMatchdayReportAsync(
        Guid roundId,
        CancellationToken cancellationToken = default)
    {
        var round = await _roundRepository.GetAsync(roundId, cancellationToken)
            ?? throw new EntityNotFoundException("Round", roundId);

        var fixtures = await _fixtureRepository.ListByRoundAsync(roundId, cancellationToken);

        if (fixtures.Count == 0)
        {
            return new MatchdayReport
            {
                RoundId = round.Id,
                RoundNumber = round.Number,
                Entries = new List<MatchdayReportEntry>()
            };
        }

        // The clubs of the whole round are the same names in every row of it, and the matches
        // are one query rather than one per fixture: a matchday is read on every load of the
        // championship, and it is the panel beside the table rather than behind it.
        var clubs = await _teamRepository.ListByIdsAsync(
            fixtures.SelectMany(fixture => new[] { fixture.HomeTeamId, fixture.AwayTeamId }).Distinct(),
            cancellationToken);
        var clubById = clubs.ToDictionary(club => club.Id);

        var matches = await _matchRepository.ListByFixtureIdsAsync(
            fixtures.Select(fixture => fixture.Id),
            cancellationToken);
        var matchByFixture = matches
            .Where(match => match.Status != MatchStatus.Abandoned)
            .GroupBy(match => match.FixtureId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(match => match.CreatedAt).First());

        var report = new List<MatchdayReportEntry>();

        foreach (var fixture in fixtures)
        {
            if (!matchByFixture.TryGetValue(fixture.Id, out var match) || !match.IsFinished)
            {
                continue;
            }

            var events = await GetEventsAsync(match.Id, null, cancellationToken);
            var goals = events
                .Where(IsGoal)
                .OrderBy(goal => goal.Sequence)
                .Select(goal => MatchService.NarrationOf(goal))
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .ToList();

            var home = clubById.GetValueOrDefault(fixture.HomeTeamId);
            var away = clubById.GetValueOrDefault(fixture.AwayTeamId);

            report.Add(new MatchdayReportEntry
            {
                FixtureId = fixture.Id,
                MatchId = match.Id,
                HomeTeamId = fixture.HomeTeamId,
                HomeTeamName = home?.Name ?? string.Empty,
                HomeShortName = home?.ShortName ?? string.Empty,
                AwayTeamId = fixture.AwayTeamId,
                AwayTeamName = away?.Name ?? string.Empty,
                AwayShortName = away?.ShortName ?? string.Empty,
                HomeGoals = match.HomeScore,
                AwayGoals = match.AwayScore,
                // A goalless match has no goal to quote, and "0 x 0" is not a story. Saying
                // that nobody found the net is the truest thing there is to say about it.
                Summary = goals.Count > 0
                    ? string.Join(" ", goals)
                    : "Ninguém achou a rede numa partida travada."
            });
        }

        return new MatchdayReport
        {
            RoundId = round.Id,
            RoundNumber = round.Number,
            Entries = report
        };
    }

    private static bool IsGoal(MatchEvent goal) =>
        goal.Type is MatchEventType.GoalScored or MatchEventType.OwnGoalScored;

    /// <summary>
    /// The sentence a goal was announced with, read back out of the event.
    ///
    /// It was written once, when the goal happened, and the event is where it was kept.
    /// Reading it back rather than rewriting it is the whole point: a report that composed
    /// its own sentences about a match would be a second narrator, and a match that only
    /// happened once does not have two.
    /// </summary>
    private static string? NarrationOf(MatchEvent goal)
    {
        if (string.IsNullOrWhiteSpace(goal.Payload))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(goal.Payload);
            return document.RootElement.TryGetProperty("description", out var description)
                ? description.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            // A payload that will not parse is a broken record, not a reason to refuse the
            // report: the rest of the round is still worth reading.
            return null;
        }
    }

    /// <summary>
    /// The eleven the staff would put out, for a club and a shape.
    ///
    /// This used to live in the lineup screen, and having it in two places was wrong twice
    /// over: the client ranked by its own idea of a player, so a keeper whose attributes are
    /// high by design could be proposed, and the eleven it proposed had no shape, because
    /// it handed out two places per line regardless of what the club was made of. The
    /// screen is where the manager reads the suggestion, not where it is worked out, and the
    /// engine and the screen now read the same <see cref="SelectAutomaticEleven"/>.
    /// </summary>
    public async Task<SquadSuggestion> GetSuggestedElevenAsync(
        Guid teamId,
        Guid seasonId,
        string? tacticCode = null,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", teamId);

        var tactic = Tactics.Find(tacticCode);
        if (!string.IsNullOrWhiteSpace(tacticCode) && tactic is null)
        {
            throw new DomainValidationException("UnknownTactic", $"Tática desconhecida: '{tacticCode}'.");
        }

        var squad = await BuildSquadAsync(team, seasonId, tactic, cancellationToken);

        return new SquadSuggestion
        {
            StarterIds = squad.Lineup.Select(player => player.PlayerId).ToList(),
            BenchIds = squad.Bench.Select(player => player.PlayerId).ToList()
        };
    }

    /// <summary>
    /// The eleven plus bench the staff would pick for a club and shape.
    /// </summary>
    public sealed class SquadSuggestion
    {
        public IReadOnlyList<Guid> StarterIds { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> BenchIds { get; init; } = Array.Empty<Guid>();
    }

    private sealed record SquadSelection(
        Team Team,
        List<MatchPlayerSnapshot> Lineup,
        List<MatchPlayerSnapshot> Bench,
        List<MatchPlayerSnapshot> All)
    {
        public static SquadSelection Empty(Team team) =>
            new(team, new List<MatchPlayerSnapshot>(), new List<MatchPlayerSnapshot>(), new List<MatchPlayerSnapshot>());
    }
}
