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

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches.AddAsync(match, cancellationToken);

    public void Update(Match match) => _dbContext.Matches.Update(match);

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
                    YellowCards = statistics.YellowCards,
                    RedCards = statistics.RedCards,
                    WasInjured = statistics.WasInjured,
                    InjuredOff = statistics.InjuredOff,
                    IsHome = statistics.TeamId == fixture.HomeTeamId,
                    OpponentName = statistics.TeamId == fixture.HomeTeamId ? away.Name : home.Name,
                    HomeGoals = match.HomeScore,
                    AwayGoals = match.AwayScore,
                    RoundNumber = round.Number
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
                    Attendance = match.Attendance
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

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
                    Attendance = match.Attendance
                })
            .Take(limit)
            .ToListAsync(cancellationToken);
}