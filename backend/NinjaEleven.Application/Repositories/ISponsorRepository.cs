using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for master sponsor data: the companies whose names go on shirts.
/// </summary>
public interface ISponsorRepository
{
    Task<Sponsor?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sponsor>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sponsor>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(Sponsor sponsor, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Sponsor> sponsors, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}
