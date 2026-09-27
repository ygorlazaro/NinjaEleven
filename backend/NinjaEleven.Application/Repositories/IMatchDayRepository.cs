using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// The calendar of a season: the matchdays, and therefore the windows of football in them.
/// </summary>
public interface IMatchDayRepository
{
    Task<IReadOnlyList<MatchDay>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task<MatchDay?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<MatchDay> matchDays, CancellationToken cancellationToken = default);
}
