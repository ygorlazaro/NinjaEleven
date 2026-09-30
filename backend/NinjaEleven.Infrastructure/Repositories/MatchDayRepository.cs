using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class MatchDayRepository : IMatchDayRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public MatchDayRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MatchDay>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchDays
            .AsNoTracking()
            .Where(matchDay => matchDay.SeasonId == seasonId)
            .OrderBy(matchDay => matchDay.Number)
            .ToListAsync(cancellationToken);

    public async Task<MatchDay?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.MatchDays
            .AsNoTracking()
            .FirstOrDefaultAsync(matchDay => matchDay.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MatchDay>> ListUntilAsync(
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchDays
            .AsNoTracking()
            .Where(matchDay => matchDay.Date <= to)
            .OrderBy(matchDay => matchDay.Date)
            .ThenBy(matchDay => matchDay.Number)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(
        IEnumerable<MatchDay> matchDays,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchDays.AddRangeAsync(matchDays, cancellationToken);
}
