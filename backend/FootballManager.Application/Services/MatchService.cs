using FootballManager.Application.Abstractions;
using FootballManager.Application.Matches;
using FootballManager.Application.Models;
using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Enums;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Players;
using FootballManager.Domain.Teams;

namespace FootballManager.Application.Services;

/// <summary>
/// Match use cases. The persisted row stays the source of truth for history while a
/// match is running; the engine's working memory lives in the session registry and is
/// rebuilt from the snapshot when a client reconnects.
/// </summary>
public class MatchService
{
    private const int SquadSize = 11;
    private const int BenchSize = 7;
    private const int MaxSubstitutionsPerTeam = 5;
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
    private readonly IUnitOfWork _unitOfWork;

    public MatchService(
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        IFixtureRepository fixtureRepository,
        IRoundRepository roundRepository,
        ICompetitionRepository competitionRepository,
        IMatchSessionRegistry sessions,
        IUnitOfWork unitOfWork)
    {
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _fixtureRepository = fixtureRepository;
        _roundRepository = roundRepository;
        _competitionRepository = competitionRepository;
        _sessions = sessions;
        _unitOfWork = unitOfWork;
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

        return afterSequence.HasValue
            ? events.Where(matchEvent => matchEvent.Sequence > afterSequence.Value).ToList()
            : events.ToList();
    }

    /// <summary>
    /// Creates the playable session for a fixture and locks the starting eleven of
    /// both clubs. Starting the same fixture twice returns the existing match instead
    /// of a second one.
    /// </summary>
    public async Task<MatchCommandResult> StartAsync(
        Guid fixtureId,
        int? seed = null,
        Guid? userTeamId = null,
        IReadOnlyCollection<Guid>? starterIds = null,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        var existing = await _matchRepository.GetByFixtureAsync(fixtureId, cancellationToken);
        if (existing is not null)
        {
            return new MatchCommandResult
            {
                Accepted = false,
                MatchId = existing.Id,
                ErrorMessage = "This fixture has already been started."
            };
        }

        var seasonId = await ResolveSeasonIdAsync(fixture, cancellationToken);
        var homeTeam = await _teamRepository.GetAsync(fixture.HomeTeamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", fixture.HomeTeamId);
        var awayTeam = await _teamRepository.GetAsync(fixture.AwayTeamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", fixture.AwayTeamId);

        var homeSquad = await BuildSquadAsync(homeTeam, seasonId, cancellationToken);
        var awaySquad = await BuildSquadAsync(awayTeam, seasonId, cancellationToken);

        if (homeSquad.Lineup.Count < SquadSize || awaySquad.Lineup.Count < SquadSize)
        {
            throw new DomainValidationException(
                "SquadTooSmall",
                $"Both clubs need at least {SquadSize} available players to kick off.");
        }

        // The manager's eleven is validated by the backend; the opponent is picked by
        // the same rules the engine would use on its own.
        if (userTeamId is { } managerTeamId && starterIds is { Count: > 0 })
        {
            if (managerTeamId == homeTeam.Id)
            {
                homeSquad = SelectStartingEleven(homeSquad, starterIds);
            }
            else if (managerTeamId == awayTeam.Id)
            {
                awaySquad = SelectStartingEleven(awaySquad, starterIds);
            }
            else
            {
                throw new DomainValidationException(
                    "TeamNotInMatch",
                    "The team is not playing this fixture.");
            }
        }

        var matchSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        var match = Match.Create(fixtureId, homeTeam.Id, awayTeam.Id);
        match.KickOff(matchSeed);
        match.StartFirstHalf();

        var context = new MatchContext(
            match.Id,
            ToTeamInfo(homeTeam),
            ToTeamInfo(awayTeam),
            homeSquad.Lineup,
            awaySquad.Lineup,
            homeSquad.Bench,
            awaySquad.Bench,
            new DeterministicRandomSource(matchSeed));

        var state = new MatchState(context);
        var engine = new MatchEngine(context.Random);

        fixture.MarkInProgress();
        _fixtureRepository.Update(fixture);

        await _matchRepository.AddAsync(match, cancellationToken);

        var events = engine.Initialize(state, 0).ToList();
        ApplyToMatch(match, state, events);
        await PersistEventsAsync(match, events, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _sessions.Register(new LiveMatch(match.Id, engine, state));

        return new MatchCommandResult
        {
            Accepted = true,
            MatchId = match.Id,
            Events = events
        };
    }

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
                HomeLineup = session.State.HomeLineup,
                AwayLineup = session.State.AwayLineup,
                HomeBench = session.State.HomeBench,
                AwayBench = session.State.AwayBench
            };
        }

        var seasonId = await ResolveSeasonIdAsync(match.FixtureId, cancellationToken);
        var homeSquad = await BuildSquadAsync(
            ToTeam(await _teamRepository.GetAsync(match.HomeTeamId, cancellationToken)), seasonId, cancellationToken);
        var awaySquad = await BuildSquadAsync(
            ToTeam(await _teamRepository.GetAsync(match.AwayTeamId, cancellationToken)), seasonId, cancellationToken);

        return new MatchLineup
        {
            MatchId = match.Id,
            UserTeamIndex = ResolveUserTeamIndex(match, userTeamId),
            HomeTeam = homeSquad.Team,
            AwayTeam = awaySquad.Team,
            HomeLineup = homeSquad.Lineup,
            AwayLineup = awaySquad.Lineup,
            HomeBench = homeSquad.Bench,
            AwayBench = awaySquad.Bench
        };
    }

    /// <summary>
    /// Live state of a match. A finished match is answered from the persisted row,
    /// because its working memory is no longer in the registry.
    /// </summary>
    public async Task<MatchStateView> GetStateAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
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
                HomeShots = 0,
                AwayShots = 0,
                HomeShotsOnTarget = 0,
                AwayShotsOnTarget = 0,
                HomeCorners = 0,
                AwayCorners = 0,
                HomeCards = 0,
                AwayCards = 0,
                HomeFouls = 0,
                AwayFouls = 0,
                HomePossession = 50,
                AwayPossession = 50,
                SubstitutionsUsedHome = 0,
                SubstitutionsUsedAway = 0,
                PenaltyAwaitingSelection = false
            };
        }

        lock (session.Gate)
        {
            return ToView(session.State, match.Status);
        }
    }

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
                ErrorMessage = "This match is not in progress."
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
                    ErrorMessage = "The match is paused."
                };
            }

            if (session.State.HalfTimePauseActive)
            {
                return new MatchCommandResult
                {
                    Accepted = false,
                    MatchId = matchId,
                    ErrorMessage = "The match is at half-time."
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

        if (finished)
        {
            match.Finish();
            _sessions.Remove(matchId);
            await SaveStatisticsAsync(match.Id, session.State, cancellationToken);

            var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);
            fixture?.MarkFinished();
            if (fixture is not null)
            {
                _fixtureRepository.Update(fixture);
            }
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
            return Refused(matchId, "This match is not in progress.");
        }

        lock (session.Gate)
        {
            if (session.State.Paused)
            {
                return Refused(matchId, "The match is already paused.");
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
            return Refused(matchId, "This match is not in progress.");
        }

        lock (session.Gate)
        {
            if (!session.State.Paused)
            {
                return Refused(matchId, "The match is not paused.");
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
            throw new DomainValidationException("InvalidSpeed", "The speed must be between 1 and 8.");
        }

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "This match is not in progress.");
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
            return Refused(matchId, "This match is not in progress.");
        }

        if (playerOutId == playerInId)
        {
            throw new DomainValidationException("InvalidSubstitution", "A player cannot replace himself.");
        }

        List<MatchEngineEvent> substitutionEvents;

        lock (session.Gate)
        {
            var isHome = teamId == match.HomeTeamId;
            if (!isHome && teamId != match.AwayTeamId)
            {
                throw new DomainValidationException("TeamNotInMatch", "The team is not playing this match.");
            }

            var lineup = isHome ? session.State.HomeLineup : session.State.AwayLineup;
            var bench = isHome ? session.State.HomeBench : session.State.AwayBench;

            var outgoing = lineup.FirstOrDefault(player => player.PlayerId == playerOutId);
            if (outgoing is null)
            {
                throw new DomainValidationException("PlayerNotOnPitch", "The outgoing player is not on the pitch.");
            }

            var incoming = bench.FirstOrDefault(player => player.PlayerId == playerInId);
            if (incoming is null)
            {
                throw new DomainValidationException("PlayerNotOnBench", "The incoming player is not on the bench.");
            }

            var used = isHome ? session.State.SubstitutionsHome : session.State.SubstitutionsAway;
            if (used >= MaxSubstitutionsPerTeam)
            {
                throw new DomainValidationException(
                    "SubstitutionLimitReached",
                    "This club has already used every substitution.");
            }

            lineup[lineup.IndexOf(outgoing)] = incoming;
            bench[bench.IndexOf(incoming)] = outgoing;
            incoming.SubbedIn = true;

            if (isHome)
            {
                session.State.SubstitutionsHome++;
            }
            else
            {
                session.State.SubstitutionsAway++;
            }

            var events = new List<MatchEngineEvent>
            {
                EmitEvent(session.State, MatchEventType.SubstitutionMade, teamId, incoming.PlayerId,
                    $"Substitution: {incoming.Name} replaces {outgoing.Name}")
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
    /// Chooses the player who will take a penalty the engine awarded.
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
            return Refused(matchId, "This match is not in progress.");
        }

        List<MatchEngineEvent> penaltyEvents;

        lock (session.Gate)
        {
            var lineup = teamId == match.AwayTeamId
                ? session.State.AwayLineup
                : session.State.HomeLineup;

            var taker = lineup.FirstOrDefault(player => player.PlayerId == playerId);
            if (taker is null)
            {
                throw new DomainValidationException("PlayerNotOnPitch", "The penalty taker is not on the pitch.");
            }

            var events = new List<MatchEngineEvent>
            {
                EmitEvent(session.State, MatchEventType.PenaltyTaken, teamId, playerId,
                    $"{taker.Name} takes the penalty")
            };

            session.State.PenaltyAwaitingSelection = false;
            session.State.PenaltyTeam = null;
            DrainFeed(session.State);
            penaltyEvents = events;
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
    /// Leaves the half-time pause and starts the second half.
    /// </summary>
    public async Task<MatchCommandResult> ContinueSecondHalfAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchAsync(matchId, cancellationToken);

        if (!_sessions.TryGet(matchId, out var session))
        {
            return Refused(matchId, "This match is not in progress.");
        }

        List<MatchEngineEvent> produced;

        lock (session.Gate)
        {
            if (!session.State.HalfTimePauseActive)
            {
                return Refused(matchId, "The match is not at half-time.");
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
            FormationHome = DefaultFormation,
            FormationAway = DefaultFormation,
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

        for (var i = 0; i < state.HomeShots; i++) statistics.AddShot(true, i < state.HomeShotsOnTarget);
        for (var i = 0; i < state.AwayShots; i++) statistics.AddShot(false, i < state.AwayShotsOnTarget);
        for (var i = 0; i < state.HomeCorners; i++) statistics.AddCorner(true);
        for (var i = 0; i < state.AwayCorners; i++) statistics.AddCorner(false);
        for (var i = 0; i < state.HomeFouls; i++) statistics.AddFoul(true);
        for (var i = 0; i < state.AwayFouls; i++) statistics.AddFoul(false);
        for (var i = 0; i < state.SubstitutionsHome; i++) statistics.AddSubstitution(true);
        for (var i = 0; i < state.SubstitutionsAway; i++) statistics.AddSubstitution(false);
        for (var i = 0; i < state.HomeScore; i++) statistics.AddGoal(true);
        for (var i = 0; i < state.AwayScore; i++) statistics.AddGoal(false);

        AddCards(statistics, state, state.HomeLineup.Concat(state.HomeBench), true);
        AddCards(statistics, state, state.AwayLineup.Concat(state.AwayBench), false);

        await _matchRepository.AddStatisticsAsync(statistics, cancellationToken);
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
        FormationHome = DefaultFormation,
        FormationAway = DefaultFormation,
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

    private async Task<Guid> ResolveSeasonIdAsync(Fixture fixture, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken)
            ?? throw new EntityNotFoundException("Round", fixture.RoundId);

        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(
            round.CompetitionSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", round.CompetitionSeasonId);

        return competitionSeason.SeasonId;
    }

    private async Task<Guid> ResolveSeasonIdAsync(Guid fixtureId, CancellationToken cancellationToken)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", fixtureId);

        return await ResolveSeasonIdAsync(fixture, cancellationToken);
    }

    /// <summary>
    /// Picks the starting eleven and the bench of a club: the strongest available
    /// players, one goalkeeper in the eleven and the rest as options.
    /// </summary>
    private async Task<SquadSelection> BuildSquadAsync(
        Team team,
        Guid seasonId,
        CancellationToken cancellationToken)
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

        var keeper = snapshots
            .Where(player => player.Position == Position.GK)
            .OrderByDescending(player => player.Reflexes + player.GoalkeeperPower)
            .FirstOrDefault();

        var starters = new List<MatchPlayerSnapshot>(SquadSize);
        if (keeper is not null)
        {
            starters.Add(keeper);
        }

        starters.AddRange(snapshots
            .Where(player => !starters.Contains(player))
            .OrderByDescending(player => OverallRating(player))
            .Take(SquadSize - starters.Count));

        var bench = snapshots
            .Where(player => !starters.Contains(player))
            .OrderByDescending(player => OverallRating(player))
            .Take(BenchSize)
            .ToList();

        return new SquadSelection(team, starters, bench, snapshots);
    }

    /// <summary>
    /// Applies the manager's chosen eleven. The backend is the authority on the lineup
    /// rules: exactly eleven players, all of them available for this club, and exactly
    /// one effective goalkeeper. Any other shape of defence, midfield or attack is
    /// deliberately allowed.
    /// </summary>
    private static SquadSelection SelectStartingEleven(
        SquadSelection squad,
        IReadOnlyCollection<Guid> starterIds)
    {
        var chosen = starterIds.Distinct().ToList();

        if (chosen.Count != SquadSize)
        {
            throw new DomainValidationException(
                "InvalidLineup",
                $"A starting eleven needs exactly {SquadSize} players.");
        }

        var available = squad.All.ToDictionary(player => player.PlayerId);
        var eleven = new List<MatchPlayerSnapshot>(SquadSize);

        foreach (var playerId in chosen)
        {
            if (!available.TryGetValue(playerId, out var player))
            {
                throw new DomainValidationException(
                    "PlayerNotAvailable",
                    "One of the selected players is not available for this club.");
            }

            eleven.Add(player);
        }

        var goalkeepers = eleven.Count(player => player.Position == Position.GK);
        if (goalkeepers != 1)
        {
            throw new DomainValidationException(
                "GoalkeeperRequired",
                goalkeepers == 0
                    ? "The starting eleven needs exactly one goalkeeper."
                    : "The starting eleven cannot have more than one goalkeeper.");
        }

        var bench = squad.All
            .Where(player => !eleven.Contains(player))
            .OrderByDescending(OverallRating)
            .Take(BenchSize)
            .ToList();

        return new SquadSelection(squad.Team, eleven, bench, squad.All);
    }

    private static int OverallRating(MatchPlayerSnapshot player) =>
        player.Speed + player.Accuracy + player.Dribbling + player.Heading + player.Strength
        + player.GoalkeeperPower + player.Reflexes;

    private static TeamInfo ToTeamInfo(Team team) =>
        new(team.Id, team.Name, team.ShortName, team.PrimaryColor, team.SecondaryColor, team.Rating);

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
        match.ApplyEngineState(state.Minute, state.HomeScore, state.AwayScore, state.Sequence);
    }

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

    private static MatchStateView ToView(MatchState state, MatchStatus status) => new()
    {
        MatchId = state.MatchId,
        HomeScore = state.HomeScore,
        AwayScore = state.AwayScore,
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
        PenaltyAwaitingSelection = state.PenaltyAwaitingSelection
    };

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
