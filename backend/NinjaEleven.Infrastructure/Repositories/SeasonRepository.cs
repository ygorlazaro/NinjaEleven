using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Infrastructure.Persistence;

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
            .OrderBy(season => season.Number)
            .ToListAsync(cancellationToken);

    public async Task<Season?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Id == id, cancellationToken);

    public async Task<Season?> GetByNumberAsync(int number, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Number == number, cancellationToken);

    public async Task<Season?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Status == SeasonStatus.InProgress, cancellationToken);

    public async Task AddAsync(Season season, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons.AddAsync(season, cancellationToken);

    /// <summary>
    /// Marks a season's row as changed.
    ///
    /// <para>
    /// The same reason as <c>MatchRepository.Update</c>: reads here are <c>AsNoTracking</c>, so
    /// the context may already be holding this season from an earlier read on the same request.
    /// A season close reads the season it is closing, opens the next one, and then writes to
    /// the one it closed — and a second instance of that key is an error EF refuses from inside
    /// the middle of a close, which is the one place a half-finished close is worst.
    /// </para>
    /// </summary>
    public void Update(Season season)
    {
        var tracked = _dbContext.Seasons.Local
            .FirstOrDefault(candidate => candidate.Id == season.Id);

        if (tracked is null)
        {
            _dbContext.Seasons.Update(season);
            return;
        }

        if (!ReferenceEquals(tracked, season))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(season);
        }
    }
}
