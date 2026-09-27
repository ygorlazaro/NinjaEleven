using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class FinanceRepository : IFinanceRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public FinanceRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(FinanceMovement movement, CancellationToken cancellationToken = default) =>
        await _dbContext.FinanceMovements.AddAsync(movement, cancellationToken);

    public async Task<FinanceMovement?> GetLastAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await _dbContext.FinanceMovements
            .AsNoTracking()
            .Where(movement => movement.TeamId == teamId)
            .OrderByDescending(movement => movement.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExistsInSeasonAsync(
        Guid teamId,
        Guid seasonId,
        FinanceMovementKind kind,
        CancellationToken cancellationToken = default) =>
        await _dbContext.FinanceMovements
            .AsNoTracking()
            .AnyAsync(
                movement => movement.TeamId == teamId
                            && movement.SeasonId == seasonId
                            && movement.Kind == kind,
                cancellationToken);

    public async Task<bool> ExistsForMatchAsync(
        Guid teamId,
        Guid matchId,
        FinanceMovementKind kind,
        CancellationToken cancellationToken = default) =>
        await _dbContext.FinanceMovements
            .AsNoTracking()
            .AnyAsync(
                movement => movement.TeamId == teamId
                            && movement.MatchId == matchId
                            && movement.Kind == kind,
                cancellationToken);

    public async Task<IReadOnlyList<FinanceMovement>> ListAsync(
        Guid teamId,
        Guid? seasonId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.FinanceMovements
            .AsNoTracking()
            .Where(movement => movement.TeamId == teamId
                               && (seasonId == null || movement.SeasonId == seasonId))
            // The sequence is the club's own order of writing, and the sequence counts across
            // seasons, so a career reads as one book with the seasons inside it.
            .OrderByDescending(movement => movement.Sequence)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(
        Guid teamId,
        Guid? seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.FinanceMovements
            .AsNoTracking()
            .CountAsync(
                movement => movement.TeamId == teamId
                            && (seasonId == null || movement.SeasonId == seasonId),
                cancellationToken);

    public async Task<FinanceTotals> TotalsAsync(
        Guid teamId,
        Guid? seasonId,
        CancellationToken cancellationToken = default)
    {
        var lines = _dbContext.FinanceMovements
            .AsNoTracking()
            .Where(movement => movement.TeamId == teamId
                               && (seasonId == null || movement.SeasonId == seasonId)
                               && movement.Kind != FinanceMovementKind.Seed
                               && movement.Kind != FinanceMovementKind.CarryOver);

        // The two totals are asked of the database rather than of a page of lines: a season's
        // income is the whole season, and a ledger that only ever read ten lines at a time
        // could not add one up.
        var income = await lines
            .Where(movement => movement.Amount > 0m)
            .SumAsync(movement => movement.Amount, cancellationToken);

        var expenses = await lines
            .Where(movement => movement.Amount < 0m)
            .SumAsync(movement => -movement.Amount, cancellationToken);

        return new FinanceTotals(income, expenses);
    }
}
