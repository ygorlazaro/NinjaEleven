namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for the plan a club's manager has laid for its next match.
/// </summary>
public interface ITeamMatchPlanRepository
{
    /// <summary>
    /// The plan this club has for this season, or null when its manager has never said one.
    /// </summary>
    Task<Domain.Matches.TeamMatchPlan?> GetAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Domain.Matches.TeamMatchPlan plan, CancellationToken cancellationToken = default);

    void Update(Domain.Matches.TeamMatchPlan plan);
}