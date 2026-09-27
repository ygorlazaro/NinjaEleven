using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class DivisionRepository : IDivisionRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public DivisionRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Division>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Divisions
            .AsNoTracking()
            .OrderBy(division => division.Tier)
            .ToListAsync(cancellationToken);

    public async Task<Division?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Divisions
            .AsNoTracking()
            .FirstOrDefaultAsync(division => division.Id == id, cancellationToken);

    public async Task<Division?> GetByTierAsync(int tier, CancellationToken cancellationToken = default) =>
        await _dbContext.Divisions
            .AsNoTracking()
            .FirstOrDefaultAsync(division => division.Tier == tier, cancellationToken);

    public async Task AddAsync(Division division, CancellationToken cancellationToken = default) =>
        await _dbContext.Divisions.AddAsync(division, cancellationToken);
}
