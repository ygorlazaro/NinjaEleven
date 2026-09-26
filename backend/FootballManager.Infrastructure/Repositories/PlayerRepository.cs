using FootballManager.Application.Repositories;
using FootballManager.Domain.Players;
using FootballManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FootballManager.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly FootballManagerDbContext _dbContext;

    public PlayerRepository(FootballManagerDbContext dbContext)
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
}
