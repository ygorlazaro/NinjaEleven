using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public MatchRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Match>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .OrderBy(match => match.FixtureId)
            .ToListAsync(cancellationToken);

    public async Task<Match?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(match => match.Id == id, cancellationToken);

    public async Task<Match?> GetByFixtureAsync(Guid fixtureId, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .Where(match => match.FixtureId == fixtureId && match.Status != MatchStatus.Abandoned)
            .OrderByDescending(match => match.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Match>> ListUnfinishedAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .Where(match => match.Status != MatchStatus.Finished && match.Status != MatchStatus.Abandoned)
            .OrderBy(match => match.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Match>> ListUnfinishedOnFinishedFixturesAsync(
        CancellationToken cancellationToken = default) =>
        await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                where match.Status != MatchStatus.Finished
                    && match.Status != MatchStatus.Abandoned
                    && fixture.Status == FixtureStatus.Finished
                orderby match.CreatedAt
                select match)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The matches another process is playing, with the window each of them belongs to.
    ///
    /// The window is joined in rather than asked for afterwards: a client following a
    /// matchday needs every live score of it, so this is read once a second while there is
    /// football on, and one question per match to find out which scoreboard a goal belongs to
    /// is the query that turns a live screen into an expensive one.
    /// </summary>
    public async Task<IReadOnlyList<LiveMatchRow>> ListLiveExceptHostAsync(
        string hostId,
        CancellationToken cancellationToken = default)
    {
        var live = MatchStatus.KickOff;

        var rows = await (
            from match in _dbContext.Matches.AsNoTracking()
            join fixture in _dbContext.Fixtures.AsNoTracking() on match.FixtureId equals fixture.Id
            where match.Status == live
                   || match.Status == Domain.Enums.MatchStatus.InProgress
                   || match.Status == Domain.Enums.MatchStatus.HalfTime
                   || match.Status == Domain.Enums.MatchStatus.SecondHalf
            where match.SessionHost != hostId
            orderby match.CreatedAt
            select new { match, fixture.RoundId })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new LiveMatchRow(row.match, row.RoundId)).ToList();
    }

    public async Task<IReadOnlyList<Match>> ListByFixtureIdsAsync(
        IEnumerable<Guid> fixtureIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .Where(match => fixtureIds.Contains(match.FixtureId))
            // A fixture can hold an abandoned match and a replay, and only the newest row that
            // was not abandoned is the match of that fixture. Ordering by creation and taking
            // the last one per fixture is what keeps an abandoned 3-0 out of a table.
            .OrderBy(match => match.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, MatchStatistics>> ListStatisticsByMatchIdsAsync(
        IEnumerable<Guid> matchIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchStatistics
            .AsNoTracking()
            .Where(statistics => matchIds.Contains(statistics.MatchId))
            .ToDictionaryAsync(statistics => statistics.MatchId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, HomeGateReading>> ReadHomeGatesAsync(
        Guid seasonId,
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = teamIds.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, HomeGateReading>();
        }

        // One query, and the two averages are taken over the rows that actually hold each
        // number rather than over a zero standing in for a missing one. A club that has not
        // recorded its pressure yet returns a measured gate and an unmeasured demand, which is
        // what its rows say.
        var rows = await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking() on match.FixtureId equals fixture.Id
                join round in _dbContext.Rounds.AsNoTracking() on fixture.RoundId equals round.Id
                join edition in _dbContext.CompetitionSeasons.AsNoTracking()
                    on round.CompetitionSeasonId equals edition.Id
                where wanted.Contains(match.HomeTeamId)
                    && edition.SeasonId == seasonId
                    && match.Status == MatchStatus.Finished
                group match by match.HomeTeamId into club
                select new
                {
                    TeamId = club.Key,
                    HomeMatches = club.Count(),
                    AverageAttendance = club.Average(candidate => (double)candidate.Attendance),
                    AverageDemand = club
                        .Where(candidate => candidate.Demand != null)
                        .Select(candidate => (double)candidate.Demand!.Value)
                        .ToList()
                        .Count == 0
                            ? (double?)null
                            : club
                                .Where(candidate => candidate.Demand != null)
                                .Average(candidate => (double)candidate.Demand!.Value),
                    BestGate = club.Max(candidate => candidate.Attendance)
                })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.TeamId,
            row => new HomeGateReading
            {
                HomeMatches = row.HomeMatches,
                AverageAttendance = row.AverageAttendance,
                AverageDemand = row.AverageDemand,
                BestGate = row.BestGate
            });
    }

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches.AddAsync(match, cancellationToken);

    /// <summary>
    /// Marks a match's row as changed.
    ///
    /// <para>
    /// Not simply <c>Update(match)</c>, because the match this method is handed is almost never
    /// the instance the context is already tracking. Reads here are <c>AsNoTracking</c> — a
    /// match is a snapshot a command works from, not a graph the context owns — so every call
    /// hands back a fresh instance of the same row. A single command touches a match more than
    /// once: it reads it, plays it, and asks the cup and the books about it, and anything on
    /// that path that attaches an instance leaves the context holding a <i>different</i> copy of
    /// the row the command is holding. EF refuses to attach the second, and it refuses by
    /// throwing an "already being tracked" error from inside a tick, which is a long way round
    /// to say that two parts of one command disagree about who owns a row.
    /// </para>
    ///
    /// <para>
    /// So the values are written onto whichever instance is already tracked. That instance is
    /// the one that will be written, the row ends up with the numbers the command played, and
    /// the copy the command was holding — which is the one the engine mutates and the one the
    /// in-memory session keeps — is left alone to keep doing that.
    /// </para>
    /// </summary>
    public void Update(Match match)
    {
        var tracked = _dbContext.Matches.Local.FirstOrDefault(candidate => candidate.Id == match.Id);

        if (tracked is null)
        {
            _dbContext.Matches.Update(match);
            return;
        }

        // The very instance the context is holding has nothing to copy onto itself.
        if (!ReferenceEquals(tracked, match))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(match);
        }
    }

    public async Task<IReadOnlyList<MatchEvent>> ListEventsAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchEvents
            .AsNoTracking()
            .Where(matchEvent => matchEvent.MatchId == matchId)
            .OrderBy(matchEvent => matchEvent.Sequence)
            .ToListAsync(cancellationToken);

    public async Task AddEventsAsync(
        IEnumerable<MatchEvent> events,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchEvents.AddRangeAsync(events, cancellationToken);

    public async Task<MatchStatistics?> GetStatisticsAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(statistics => statistics.MatchId == matchId, cancellationToken);

    public async Task AddStatisticsAsync(
        MatchStatistics statistics,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchStatistics.AddAsync(statistics, cancellationToken);

    public async Task AddPlayerStatisticsAsync(
        IEnumerable<MatchPlayerStatistics> statistics,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchPlayerStatistics.AddRangeAsync(statistics, cancellationToken);

    /// <inheritdoc />
    public async Task RemovePlayerStatisticsAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var lines = await _dbContext.MatchPlayerStatistics
            .Where(statistics => statistics.MatchId == matchId)
            .ToListAsync(cancellationToken);

        // Staged and not saved: the abandonment of the match and the taking back of its lines
        // are one fact, so they are written by the one unit of work that writes the match. A
        // match closed as abandoned whose lines survived would be a void match still counted.
        _dbContext.MatchPlayerStatistics.RemoveRange(lines);
    }

    public async Task<IReadOnlyList<MatchPlayerStatistics>> ListPlayerStatisticsAsync(
        Guid playerId,
        Guid? seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchPlayerStatistics
            .AsNoTracking()
            .Where(statistics => statistics.PlayerId == playerId
                && (seasonId == null || statistics.SeasonId == seasonId))
            .OrderByDescending(statistics => statistics.MatchId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MatchPlayerStatistics>> ListPlayerStatisticsByMatchIdsAsync(
        IEnumerable<Guid> matchIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchPlayerStatistics
            .AsNoTracking()
            .Where(statistics => matchIds.Contains(statistics.MatchId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Domain.Competitions.MatchDay>> GetMatchDaysAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchDays
            .AsNoTracking()
            .Where(matchDay => matchDay.SeasonId == seasonId)
            .OrderBy(matchDay => matchDay.Number)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Application.Models.PlayerMatchRecord>> GetPlayerHistoryAsync(
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await (
                from statistics in _dbContext.MatchPlayerStatistics.AsNoTracking()
                join match in _dbContext.Matches.AsNoTracking()
                    on statistics.MatchId equals match.Id
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                join round in _dbContext.Rounds.AsNoTracking()
                    on fixture.RoundId equals round.Id
                join home in _dbContext.Teams.AsNoTracking()
                    on fixture.HomeTeamId equals home.Id
                join away in _dbContext.Teams.AsNoTracking()
                    on fixture.AwayTeamId equals away.Id
                // The same joins the club's own history makes, so a player's line is read in the
                // same words a club's line is read in. A round carries its competition season and
                // that carries the season and the competition; the ground is the home club's
                // stadium, which is why a home club with no stadium yet reads as none rather than
                // as an away draw with a name attached.
                join homeStadium in _dbContext.Stadiums.AsNoTracking()
                    on home.StadiumId equals homeStadium.Id into homeStadiumGroup
                from homeStadium in homeStadiumGroup.DefaultIfEmpty()
                join compSeason in _dbContext.CompetitionSeasons.AsNoTracking()
                    on round.CompetitionSeasonId equals compSeason.Id
                join season in _dbContext.Seasons.AsNoTracking()
                    on compSeason.SeasonId equals season.Id
                join competition in _dbContext.Competitions.AsNoTracking()
                    on compSeason.CompetitionId equals competition.Id
                where statistics.PlayerId == playerId
                orderby match.CreatedAt descending, match.Id descending
                select new Application.Models.PlayerMatchRecord
                {
                    MatchId = statistics.MatchId,
                    SeasonId = statistics.SeasonId,
                    Started = statistics.Started,
                    CameOn = statistics.CameOn,
                    SubbedOff = statistics.SubbedOff,
                    WasOnBenchUnused = statistics.WasOnBenchUnused,
                    Goals = statistics.Goals,
                    OwnGoals = statistics.OwnGoals,
                    Saves = statistics.Saves,
                    // The number only. The band is worked out by the service, out of what
                    // comes back: a projection is translated into SQL, and a band is a reading
                    // of a number rather than a column, so asking for it here asked the
                    // database to run the rating's own rules.
                    Rating = statistics.Rating,
                    MinutesPlayed = statistics.MinutesPlayed,
                    Assists = statistics.Assists,
                    YellowCards = statistics.YellowCards,
                    RedCards = statistics.RedCards,
                    WasInjured = statistics.WasInjured,
                    InjuredOff = statistics.InjuredOff,
                    IsHome = statistics.TeamId == fixture.HomeTeamId,
                    OpponentName = statistics.TeamId == fixture.HomeTeamId ? away.Name : home.Name,
                    OpponentTeamId = statistics.TeamId == fixture.HomeTeamId ? away.Id : home.Id,
                    OpponentTeamPrimaryColor = statistics.TeamId == fixture.HomeTeamId ? away.PrimaryColor : home.PrimaryColor,
                    OpponentTeamSecondaryColor = statistics.TeamId == fixture.HomeTeamId ? away.SecondaryColor : home.SecondaryColor,
                    HomeGoals = match.HomeScore,
                    AwayGoals = match.AwayScore,
                    RoundNumber = round.Number,
                    TeamName = statistics.TeamId == fixture.HomeTeamId ? home.Name : away.Name,
                    // The club's own id, so a line is a door to that club and a season's line is
                    // grouped by the shirt the goals were scored in rather than by a name.
                    TeamId = statistics.TeamId,
                    SeasonName = season.Name,
                    CompetitionName = competition.Name,
                    PhaseName = compSeason.IsDivision ? $"Rodada {round.Number}" : (round.Window == 1 ? "Campeonato" : "Copa"),
                    Attendance = match.Attendance,
                    StadiumName = homeStadium != null ? homeStadium.Name : null
                })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Application.Models.TeamMatchRecord>> GetTeamHistoryAsync(
        Guid teamId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                join round in _dbContext.Rounds.AsNoTracking()
                    on fixture.RoundId equals round.Id
                join home in _dbContext.Teams.AsNoTracking()
                    on fixture.HomeTeamId equals home.Id
                join away in _dbContext.Teams.AsNoTracking()
                    on fixture.AwayTeamId equals away.Id
                join homeStadium in _dbContext.Stadiums.AsNoTracking()
                    on home.StadiumId equals homeStadium.Id into homeStadiumGroup
                from homeStadium in homeStadiumGroup.DefaultIfEmpty()
                join compSeason in _dbContext.CompetitionSeasons.AsNoTracking()
                    on round.CompetitionSeasonId equals compSeason.Id
                join season in _dbContext.Seasons.AsNoTracking()
                    on compSeason.SeasonId equals season.Id
                join competition in _dbContext.Competitions.AsNoTracking()
                    on compSeason.CompetitionId equals competition.Id
                where (fixture.HomeTeamId == teamId || fixture.AwayTeamId == teamId)
                    && match.Status == MatchStatus.Finished
                orderby match.CreatedAt descending, match.Id descending
                select new Application.Models.TeamMatchRecord
                {
                    MatchId = match.Id,
                    OpponentName = fixture.HomeTeamId == teamId ? away.Name : home.Name,
                    OpponentTeamId = fixture.HomeTeamId == teamId ? away.Id : home.Id,
                    IsHome = fixture.HomeTeamId == teamId,
                    GoalsFor = fixture.HomeTeamId == teamId ? match.HomeScore : match.AwayScore,
                    GoalsAgainst = fixture.HomeTeamId == teamId ? match.AwayScore : match.HomeScore,
                    RoundNumber = round.Number,
                    PlayedAt = match.CreatedAt,
                    SeasonName = season.Name,
                    CompetitionName = competition.Name,
                    PhaseName = compSeason.IsDivision ? $"Rodada {round.Number}" : (round.Window == 1 ? "Campeonato" : "Copa"),
                    Attendance = match.Attendance,
                    StadiumName = homeStadium != null ? homeStadium.Name : null,
                    TacticCode = fixture.HomeTeamId == teamId ? match.TacticCode : match.AwayTacticCode,
                    OpponentTacticCode = fixture.HomeTeamId == teamId ? match.AwayTacticCode : match.TacticCode
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Application.Models.TeamMatchRecord>>> GetTeamHistoryByTeamsAsync(
        IEnumerable<Guid> teamIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var ids = teamIds.ToList();

        var rows = await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                join round in _dbContext.Rounds.AsNoTracking()
                    on fixture.RoundId equals round.Id
                join compSeason in _dbContext.CompetitionSeasons.AsNoTracking()
                    on round.CompetitionSeasonId equals compSeason.Id
                join competition in _dbContext.Competitions.AsNoTracking()
                    on compSeason.CompetitionId equals competition.Id
                join home in _dbContext.Teams.AsNoTracking()
                    on fixture.HomeTeamId equals home.Id
                join away in _dbContext.Teams.AsNoTracking()
                    on fixture.AwayTeamId equals away.Id
                where ids.Contains(fixture.HomeTeamId) || ids.Contains(fixture.AwayTeamId)
                where match.Status == MatchStatus.Finished
                orderby match.CreatedAt descending, match.Id descending
                select new
                {
                    match.Id,
                    match.FixtureId,
                    fixture.HomeTeamId,
                    fixture.AwayTeamId,
                    match.HomeScore,
                    match.AwayScore,
                    HomeName = home.Name,
                    AwayName = away.Name,
                    round.Number,
                    match.CreatedAt,
                    match.Attendance,
                    competition.Name,
                    compSeason.IsDivision
                })
            .ToListAsync(cancellationToken);

        var byTeam = new Dictionary<Guid, List<Application.Models.TeamMatchRecord>>();

        // One fixture is one row here and two lines of history — one for each side that was
        // asked about. Reading the two sides apart in the query is what a match between two
        // clubs of the set needs, and a match where only one side is in the set must still
        // reach that one.
        foreach (var row in rows)
        {
            if (ids.Contains(row.HomeTeamId))
            {
                Add(byTeam, row.HomeTeamId, new Application.Models.TeamMatchRecord
                {
                    MatchId = row.Id,
                    TeamId = row.HomeTeamId,
                    OpponentName = row.AwayName,
                    OpponentTeamId = row.AwayTeamId,
                    IsHome = true,
                    GoalsFor = row.HomeScore,
                    GoalsAgainst = row.AwayScore,
                    RoundNumber = row.Number,
                    PlayedAt = row.CreatedAt,
                    CompetitionName = row.Name,
                    IsDivision = row.IsDivision,
                    Attendance = row.Attendance
                }, limit);
            }

            if (ids.Contains(row.AwayTeamId))
            {
                Add(byTeam, row.AwayTeamId, new Application.Models.TeamMatchRecord
                {
                    MatchId = row.Id,
                    TeamId = row.AwayTeamId,
                    OpponentName = row.HomeName,
                    OpponentTeamId = row.HomeTeamId,
                    IsHome = false,
                    GoalsFor = row.AwayScore,
                    GoalsAgainst = row.HomeScore,
                    RoundNumber = row.Number,
                    PlayedAt = row.CreatedAt,
                    CompetitionName = row.Name,
                    IsDivision = row.IsDivision,
                    Attendance = row.Attendance
                }, limit);
            }
        }

        return byTeam.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<Application.Models.TeamMatchRecord>)entry.Value);

        // The rows arrive newest first for the whole set rather than per club, so each club's
        // own list is cut here rather than in the query: five recent matches per club cannot
        // be expressed as a Take on a query that spans sixty-four clubs at once.
        static void Add(
            Dictionary<Guid, List<Application.Models.TeamMatchRecord>> map,
            Guid teamId,
            Application.Models.TeamMatchRecord record,
            int cap)
        {
            if (!map.TryGetValue(teamId, out var list))
            {
                list = [];
                map[teamId] = list;
            }

            if (list.Count < cap)
            {
                list.Add(record);
            }
        }
    }

    public async Task<string?> GetLastTacticCodeAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                where (fixture.HomeTeamId == teamId || fixture.AwayTeamId == teamId)
                    && match.Status == MatchStatus.Finished
                    && match.TacticCode != ""
                orderby match.CreatedAt descending, match.Id descending
                select match.TacticCode)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Application.Models.HeadToHeadSummary> GetHeadToHeadSummaryAsync(
        Guid teamId,
        Guid opponentId,
        CancellationToken cancellationToken = default)
    {
        // Counted in the database over every meeting ever played. Each meeting is first
        // flipped into the club's own point of view, because a summary that counted the
        // fixture's home goals as "for" would report a different history depending on which of
        // the two clubs the manager happens to manage.
        var row = await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                where match.Status == MatchStatus.Finished
                    && (
                        (fixture.HomeTeamId == teamId && fixture.AwayTeamId == opponentId) ||
                        (fixture.HomeTeamId == opponentId && fixture.AwayTeamId == teamId)
                    )
                select new
                {
                    GoalsFor = fixture.HomeTeamId == teamId ? match.HomeScore : match.AwayScore,
                    GoalsAgainst = fixture.HomeTeamId == teamId ? match.AwayScore : match.HomeScore
                })
            .GroupBy(meeting => 1)
            .Select(group => new
            {
                Played = group.Count(),
                Wins = group.Count(meeting => meeting.GoalsFor > meeting.GoalsAgainst),
                Draws = group.Count(meeting => meeting.GoalsFor == meeting.GoalsAgainst),
                Losses = group.Count(meeting => meeting.GoalsFor < meeting.GoalsAgainst),
                GoalsFor = group.Sum(meeting => meeting.GoalsFor),
                GoalsAgainst = group.Sum(meeting => meeting.GoalsAgainst)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new Application.Models.HeadToHeadSummary
        {
            Played = row?.Played ?? 0,
            Wins = row?.Wins ?? 0,
            Draws = row?.Draws ?? 0,
            Losses = row?.Losses ?? 0,
            GoalsFor = row?.GoalsFor ?? 0,
            GoalsAgainst = row?.GoalsAgainst ?? 0
        };
    }

    public async Task<IReadOnlyList<Application.Models.TeamMatchRecord>> GetHeadToHeadAsync(
        Guid teamId,
        Guid opponentId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await (
                from match in _dbContext.Matches.AsNoTracking()
                join fixture in _dbContext.Fixtures.AsNoTracking()
                    on match.FixtureId equals fixture.Id
                join round in _dbContext.Rounds.AsNoTracking()
                    on fixture.RoundId equals round.Id
                join home in _dbContext.Teams.AsNoTracking()
                    on fixture.HomeTeamId equals home.Id
                join away in _dbContext.Teams.AsNoTracking()
                    on fixture.AwayTeamId equals away.Id
                join homeStadium in _dbContext.Stadiums.AsNoTracking()
                    on home.StadiumId equals homeStadium.Id into homeStadiumGroup
                from homeStadium in homeStadiumGroup.DefaultIfEmpty()
                join compSeason in _dbContext.CompetitionSeasons.AsNoTracking()
                    on round.CompetitionSeasonId equals compSeason.Id
                join season in _dbContext.Seasons.AsNoTracking()
                    on compSeason.SeasonId equals season.Id
                join competition in _dbContext.Competitions.AsNoTracking()
                    on compSeason.CompetitionId equals competition.Id
                where match.Status == MatchStatus.Finished
                    && (
                        (fixture.HomeTeamId == teamId && fixture.AwayTeamId == opponentId) ||
                        (fixture.HomeTeamId == opponentId && fixture.AwayTeamId == teamId)
                    )
                orderby match.CreatedAt descending, match.Id descending
                select new Application.Models.TeamMatchRecord
                {
                    MatchId = match.Id,
                    OpponentName = fixture.HomeTeamId == teamId ? away.Name : home.Name,
                    OpponentTeamId = fixture.HomeTeamId == teamId ? away.Id : home.Id,
                    IsHome = fixture.HomeTeamId == teamId,
                    GoalsFor = fixture.HomeTeamId == teamId ? match.HomeScore : match.AwayScore,
                    GoalsAgainst = fixture.HomeTeamId == teamId ? match.AwayScore : match.HomeScore,
                    RoundNumber = round.Number,
                    PlayedAt = match.CreatedAt,
                    SeasonName = season.Name,
                    CompetitionName = competition.Name,
                    PhaseName = compSeason.IsDivision ? $"Rodada {round.Number}" : (round.Window == 1 ? "Campeonato" : "Copa"),
                    Attendance = match.Attendance,
                    StadiumName = homeStadium != null ? homeStadium.Name : null
                })
            .Take(limit)
            .ToListAsync(cancellationToken);
}