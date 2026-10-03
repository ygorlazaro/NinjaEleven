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

    /// <summary>
    /// The order this club last laid down, whatever season it was laid in.
    /// </summary>
    /// <remarks>
    /// A plan is keyed by season so that a squad of one year is not asked to play the next
    /// one's football, and that is right for the <i>people</i>. It is wrong for the shape:
    /// a manager who leaves the board saying 3-4-3 has not said anything about November, and
    /// a club whose standing order evaporates on the day the new season opens is a club
    /// that goes out in whatever shape it happened to finish the last one in.
    /// </remarks>
    Task<Domain.Matches.TeamMatchPlan?> GetLatestAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Domain.Matches.TeamMatchPlan plan, CancellationToken cancellationToken = default);

    void Update(Domain.Matches.TeamMatchPlan plan);
}