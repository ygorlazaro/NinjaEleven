using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// The cup's ties, and the shelf where the trophies end up.
/// </summary>
public interface ICupTieRepository
{
    Task<IReadOnlyList<CupTie>> ListByCompetitionSeasonAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CupTie>> ListByRoundAsync(
        Guid competitionSeasonId,
        int roundNumber,
        CancellationToken cancellationToken = default);

    Task<CupTie?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CupTie?> GetByLegAsync(Guid fixtureId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<CupTie> ties, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a tie as changed. A tie is resolved in place rather than replaced, so this is
    /// how the aggregate and the winner reach the database.
    /// </summary>
    void Update(CupTie tie);

    /// <summary>
    /// Gets the club IDs that are still alive in the cup (haven't been eliminated yet).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAliveClubsInCupAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What a club has won, kept rather than recomputed: the table of a season played years ago
/// still says who won it, and a club that changed divisions afterwards still has the one it
/// won before the move.
/// </summary>
public interface ITrophyRepository
{
    Task<IReadOnlyList<TrophyAward>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrophyAward>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TrophyAward> trophies, CancellationToken cancellationToken = default);
}
