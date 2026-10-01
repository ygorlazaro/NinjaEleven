using NinjaEleven.Domain.Managers;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for the manager who owns the career. A club has one manager and only one,
/// so the store is keyed by the club.
/// </summary>
public interface IManagerRepository
{
    Task<Manager?> GetByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Manager?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Manager?> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every manager a person is behind, with the account behind it, tracked so the sweep can
    /// write to them.
    ///
    /// This is a read of a set rather than a read of one, and the reason is the same as
    /// everywhere else: a dormancy sweep asks the same question of every manager in the
    /// world, and asking it one at a time turns a single cheap question into a query per
    /// career on earth. The rows come back tracked and with their account attached because
    /// the sweep answers by writing — a manager whose account is gone and a manager whose
    /// account merely has not been read are the same row, seen from two sides, and the
    /// question is about both.
    /// </summary>
    Task<IReadOnlyList<Manager>> ListManagedWithTheirAccountsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Manager manager, CancellationToken cancellationToken = default);
    void Update(Manager manager);
    void Remove(Manager manager);
}
