using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class TeamMatchPlanRepository : ITeamMatchPlanRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public TeamMatchPlanRepository(NinjaElevenDbContext dbContext) => _dbContext = dbContext;

    public async Task<TeamMatchPlan?> GetAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamMatchPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.TeamId == teamId && plan.SeasonId == seasonId, cancellationToken);

    /// <summary>
    /// The club's own last word on how it plays, read as one row: the most recently restated
    /// plan it has, whatever season that was written in.
    /// </summary>
    public async Task<TeamMatchPlan?> GetLatestAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamMatchPlans
            .AsNoTracking()
            .Where(plan => plan.TeamId == teamId)
            .OrderByDescending(plan => plan.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(TeamMatchPlan plan, CancellationToken cancellationToken = default) =>
        await _dbContext.TeamMatchPlans.AddAsync(plan, cancellationToken);

    /// <summary>
    /// The plan is one row per club and season, so a restatement is the row the context
    /// already holds rather than a second answer to the same question.
    /// </summary>
    public void Update(TeamMatchPlan plan) => _dbContext.TeamMatchPlans.Update(plan);
}