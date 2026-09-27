using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class FixtureRepository : IFixtureRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public FixtureRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Fixture>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .OrderBy(fixture => fixture.RoundId)
            .ToListAsync(cancellationToken);

    public async Task<Fixture?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .FirstOrDefaultAsync(fixture => fixture.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Fixture>> ListByRoundAsync(
        Guid roundId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .Where(fixture => fixture.RoundId == roundId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Fixture>> ListByRoundIdsAsync(
        IEnumerable<Guid> roundIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .Where(fixture => roundIds.Contains(fixture.RoundId))
            .OrderBy(fixture => fixture.RoundId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Fixture fixture, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures.AddAsync(fixture, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Fixture> fixtures, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures.AddRangeAsync(fixtures, cancellationToken);

    public void Update(Fixture fixture) => _dbContext.Fixtures.Update(fixture);

    public void RemoveRange(IEnumerable<Fixture> fixtures) => _dbContext.Fixtures.RemoveRange(fixtures);
}
