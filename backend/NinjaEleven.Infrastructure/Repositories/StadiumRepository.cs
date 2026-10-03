using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class StadiumRepository : IStadiumRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public StadiumRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Stadium>> ListForUpdateAsync(
        IEnumerable<Guid> stadiumIds,
        CancellationToken cancellationToken = default)
    {
        var ids = stadiumIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<Stadium>();
        }

        return await _dbContext.Stadiums
            .Where(stadium => ids.Contains(stadium.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Stadium>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Stadiums
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}