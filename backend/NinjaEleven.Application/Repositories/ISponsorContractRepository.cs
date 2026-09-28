using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for shirt deals between sponsors and clubs.
/// </summary>
public interface ISponsorContractRepository
{
    Task<SponsorContract?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The active deal a club currently has, if it has one.</summary>
    Task<SponsorContract?> GetActiveByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>All contracts a club has ever held, newest first.</summary>
    Task<IReadOnlyList<SponsorContract>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task AddAsync(SponsorContract contract, CancellationToken cancellationToken = default);
    void Update(SponsorContract contract);
}
