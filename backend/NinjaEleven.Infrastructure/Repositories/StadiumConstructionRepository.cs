using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class StadiumConstructionRepository : IStadiumConstructionRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public StadiumConstructionRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<StadiumConstruction>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.StadiumConstructions
            .AsNoTracking()
            .Where(construction => construction.SeasonId == seasonId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StadiumConstruction>> ListUnfinishedAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.StadiumConstructions
            .Where(construction => construction.CompletedAt == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StadiumConstruction>> ListUnfinishedForStadiumsAsync(
        IEnumerable<Guid> stadiumIds,
        CancellationToken cancellationToken = default)
    {
        var ids = stadiumIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<StadiumConstruction>();
        }

        return await _dbContext.StadiumConstructions
            .Where(construction => construction.CompletedAt == null && ids.Contains(construction.StadiumId))
            .ToListAsync(cancellationToken);
    }

    public Task<StadiumConstruction?> FindOpenForStadiumAsync(
        Guid stadiumId,
        CancellationToken cancellationToken = default) =>
        _dbContext.StadiumConstructions
            .FirstOrDefaultAsync(
                construction => construction.StadiumId == stadiumId && construction.CompletedAt == null,
                cancellationToken);

    public Task<StadiumConstruction?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _dbContext.StadiumConstructions
            .FirstOrDefaultAsync(construction => construction.Id == id, cancellationToken);

    public async Task AddAsync(StadiumConstruction construction, CancellationToken cancellationToken = default) =>
        await _dbContext.StadiumConstructions.AddAsync(construction, cancellationToken);

    /// <summary>
    /// Copies the caller's values onto the tracked instance rather than attaching the caller's
    /// row, because the caller read it with <c>AsNoTracking</c> and the context is already
    /// holding a copy of the same row from its own earlier read.
    /// </summary>
    public void Update(StadiumConstruction construction)
    {
        ArgumentNullException.ThrowIfNull(construction);

        var tracked = _dbContext.StadiumConstructions.Local
            .FirstOrDefault(candidate => candidate.Id == construction.Id);

        if (tracked is null)
        {
            _dbContext.StadiumConstructions.Update(construction);
            return;
        }

        // `Complete` answers whether this call was the one that closed the work, which is the
        // guard the settlement leans on. Applying a completion the settlement already applied
        // would be harmless to the row and wrong to the report, so the row's own answer decides.
        tracked.Complete(construction.CompletedAt ?? construction.StartedAt);
    }
}