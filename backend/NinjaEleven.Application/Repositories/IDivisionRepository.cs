using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// The tiers of the pyramid, which are permanent and not part of any season. Which clubs are
/// in one is not kept here: that belongs to the participants of the division's competition
/// season, because it is only true for one season.
/// </summary>
public interface IDivisionRepository
{
    Task<IReadOnlyList<Division>> ListAsync(CancellationToken cancellationToken = default);
    Task<Division?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Division?> GetByTierAsync(int tier, CancellationToken cancellationToken = default);
    Task AddAsync(Division division, CancellationToken cancellationToken = default);
}
