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
    Task AddAsync(Manager manager, CancellationToken cancellationToken = default);
    void Update(Manager manager);
}
