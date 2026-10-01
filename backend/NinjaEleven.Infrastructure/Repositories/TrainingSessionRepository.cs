using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Players;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class TrainingSessionRepository : ITrainingSessionRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public TrainingSessionRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// The count is taken in the database rather than by reading the rows and counting them in
    /// memory, but the rule still counts rows: there is no counter column on this table for it
    /// to be out of step with, which is the whole reason the allowance is allowed to be
    /// trustworthy.
    /// </summary>
    public async Task<IReadOnlyList<TrainingSession>> ListByTeamAndDayAsync(
        Guid teamId,
        DateOnly day,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TrainingSessions
            .AsNoTracking()
            .Where(session => session.TeamId == teamId && session.Day == day)
            .OrderBy(session => session.PerformedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListByPlayerAndDayAsync(
        Guid playerId,
        DateOnly day,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TrainingSessions
            .AsNoTracking()
            .Where(session => session.PlayerId == playerId && session.Day == day)
            .OrderBy(session => session.PerformedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListByPlayerAsync(
        Guid playerId,
        int take = 20,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TrainingSessions
            .AsNoTracking()
            .Where(session => session.PlayerId == playerId)
            .OrderByDescending(session => session.PerformedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TrainingSession session, CancellationToken cancellationToken = default) =>
        await _dbContext.TrainingSessions.AddAsync(session, cancellationToken);
}
