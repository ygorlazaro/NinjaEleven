using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class ManagerRepository : IManagerRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public ManagerRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Manager?> GetByTeamAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await _dbContext.Managers
            .FirstOrDefaultAsync(manager => manager.TeamId == teamId, cancellationToken);

    public async Task<Manager?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Managers
            .FirstOrDefaultAsync(manager => manager.Id == id, cancellationToken);

    public async Task<Manager?> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbContext.Managers
            .FirstOrDefaultAsync(manager => manager.UserId == userId, cancellationToken);

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Managers.AnyAsync(cancellationToken);

    public async Task AddAsync(Manager manager, CancellationToken cancellationToken = default) =>
        await _dbContext.Managers.AddAsync(manager, cancellationToken);

    public void Update(Manager manager) =>
        _dbContext.Managers.Update(manager);
}
