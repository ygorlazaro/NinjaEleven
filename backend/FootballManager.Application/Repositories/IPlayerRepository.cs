using FootballManager.Domain.Players;

namespace FootballManager.Application.Repositories;

/// <summary>
/// Persistence contract for players and their season state.
/// </summary>
public interface IPlayerRepository
{
    Task<IReadOnlyList<Player>> ListAsync(CancellationToken cancellationToken = default);
    Task<Player?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Player player, CancellationToken cancellationToken = default);
    void Update(Player player);
    void Remove(Player player);

    Task<PlayerSeasonState?> GetSeasonStateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Season state of every player, optionally restricted to one club.
    /// </summary>
    Task<IReadOnlyList<PlayerSeasonState>> ListSeasonStatesAsync(
        Guid seasonId,
        Guid? teamId = null,
        CancellationToken cancellationToken = default);
}
