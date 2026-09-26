using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public SeasonRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .OrderBy(season => season.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<Season?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Id == id, cancellationToken);

    public async Task<Season?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Status == SeasonStatus.InProgress, cancellationToken);

    public async Task AddAsync(Season season, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons.AddAsync(season, cancellationToken);

    public void Update(Season season) => _dbContext.Seasons.Update(season);
}
