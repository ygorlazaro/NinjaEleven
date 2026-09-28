using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class SponsorRepository : ISponsorRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public SponsorRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Sponsor?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Sponsors
            .FirstOrDefaultAsync(sponsor => sponsor.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Sponsor>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Sponsors
            .AsNoTracking()
            .OrderBy(sponsor => sponsor.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Sponsor>> ListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Sponsors
            .AsNoTracking()
            .Where(sponsor => ids.Contains(sponsor.Id))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Sponsor sponsor, CancellationToken cancellationToken = default) =>
        await _dbContext.Sponsors.AddAsync(sponsor, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Sponsor> sponsors, CancellationToken cancellationToken = default) =>
        await _dbContext.Sponsors.AddRangeAsync(sponsors, cancellationToken);

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Sponsors.AnyAsync(cancellationToken);
}
