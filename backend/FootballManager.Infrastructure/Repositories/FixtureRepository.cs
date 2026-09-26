using FootballManager.Application.Repositories;
using FootballManager.Domain.Matches;
using FootballManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FootballManager.Infrastructure.Repositories;

public class FixtureRepository : IFixtureRepository
{
    private readonly FootballManagerDbContext _dbContext;

    public FixtureRepository(FootballManagerDbContext dbContext)
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

    public async Task AddAsync(Fixture fixture, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures.AddAsync(fixture, cancellationToken);

    public void Update(Fixture fixture) => _dbContext.Fixtures.Update(fixture);
}
