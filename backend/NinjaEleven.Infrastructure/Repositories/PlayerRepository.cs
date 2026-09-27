using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public PlayerRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Player>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Players
            .AsNoTracking()
            .OrderBy(player => player.Name)
            .ToListAsync(cancellationToken);

    public async Task<Player?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(player => player.Id == id, cancellationToken);

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default) =>
        await _dbContext.Players.AddAsync(player, cancellationToken);

    public void Update(Player player) => _dbContext.Players.Update(player);

    public void Remove(Player player) => _dbContext.Players.Remove(player);

    public async Task<PlayerSeasonState?> GetSeasonStateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.PlayerSeasonStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.PlayerId == playerId && state.SeasonId == seasonId, cancellationToken);

    public async Task<IReadOnlyList<PlayerSeasonState>> ListSeasonStatesAsync(
        Guid seasonId,
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PlayerSeasonStates
            .AsNoTracking()
            .Where(state => state.SeasonId == seasonId);

        if (teamId.HasValue)
        {
            query = query.Where(state => state.TeamId == teamId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PlayerSeasonState?> GetSeasonStateForUpdateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.PlayerSeasonStates
            .FirstOrDefaultAsync(state => state.PlayerId == playerId && state.SeasonId == seasonId, cancellationToken);

    public void UpdateSeasonState(PlayerSeasonState seasonState) =>
        _dbContext.PlayerSeasonStates.Update(seasonState);

    /// <summary>
    /// The goals of a club's men in a season, summed from the match lines and grouped by
    /// player. Only men who scored are returned: a table of scorers that carried every man who
    /// played would put a defender who never scored in the same list as a striker, and the
    /// list would then be a squad.
    /// </summary>
    public async Task<IReadOnlyList<ClubScorerLine>> ListClubScorerLinesAsync(
        Guid teamId,
        Guid seasonId,
        CompetitionType? competitionType = null,
        CancellationToken cancellationToken = default)
    {
        // The chain from a match line to a kind of competition: line → match → fixture →
        // round → edition → competition. Only the last link carries the kind, so the whole
        // walk is done in the database and nothing is loaded into memory to be filtered there.
        var query =
            from line in _dbContext.MatchPlayerStatistics.AsNoTracking()
            join match in _dbContext.Matches.AsNoTracking() on line.MatchId equals match.Id
            join fixture in _dbContext.Fixtures.AsNoTracking() on match.FixtureId equals fixture.Id
            join round in _dbContext.Rounds.AsNoTracking() on fixture.RoundId equals round.Id
            join edition in _dbContext.CompetitionSeasons.AsNoTracking()
                on round.CompetitionSeasonId equals edition.Id
            join competition in _dbContext.Competitions.AsNoTracking()
                on edition.CompetitionId equals competition.Id
            where line.TeamId == teamId && line.SeasonId == seasonId
            select new { line, competition.Type };

        if (competitionType.HasValue)
        {
            var wanted = competitionType.Value;
            query = query.Where(row => row.Type == wanted);
        }

        // Own goals are summed apart and never added to the goals: a goal conceded into his
        // own net is a defender's error, and a scorers table that counted it would hand a
        // centre-back a column of goals he did not score.
        var rows = await query
            .GroupBy(row => new { row.line.PlayerId, row.line.TeamId })
            .Select(group => new ClubScorerLine
            {
                PlayerId = group.Key.PlayerId,
                TeamId = group.Key.TeamId,
                Goals = group.Sum(row => row.line.Goals),
                OwnGoals = group.Sum(row => row.line.OwnGoals),
                Started = group.Sum(row => row.line.Started ? 1 : 0),
                CameOn = group.Sum(row => row.line.CameOn ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(line => line.Goals > 0)
            .OrderByDescending(line => line.Goals)
            .ThenBy(line => line.PlayerId)
            .ToList();
    }
}
