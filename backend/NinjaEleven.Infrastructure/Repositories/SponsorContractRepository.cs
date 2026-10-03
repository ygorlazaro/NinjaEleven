using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class SponsorContractRepository : ISponsorContractRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public SponsorContractRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SponsorContract?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.SponsorContracts
            .Include(contract => contract.Sponsor)
            .FirstOrDefaultAsync(contract => contract.Id == id, cancellationToken);

    public async Task<SponsorContract?> GetActiveByTeamAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await _dbContext.SponsorContracts
            .Include(contract => contract.Sponsor)
            .FirstOrDefaultAsync(
                contract => contract.TeamId == teamId
                            && contract.Status == SponsorContractStatus.Active
                            && contract.ContractMatches > contract.MatchesPlayed,
                cancellationToken);

    public async Task<IReadOnlyList<SponsorContract>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await _dbContext.SponsorContracts
            .AsNoTracking()
            .Include(contract => contract.Sponsor)
            .Where(contract => contract.TeamId == teamId)
            .OrderByDescending(contract => contract.SignedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SponsorContract>>> ListActiveBySponsorIdsAsync(
        IEnumerable<Guid> sponsorIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sponsorIds.ToList();

        var contracts = await _dbContext.SponsorContracts
            .AsNoTracking()
            .Where(contract => ids.Contains(contract.SponsorId))
            .Where(contract => contract.Status == SponsorContractStatus.Active
                               && contract.ContractMatches > contract.MatchesPlayed)
            .OrderByDescending(contract => contract.SignedAt)
            .ToListAsync(cancellationToken);

        return contracts
            .GroupBy(contract => contract.SponsorId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SponsorContract>)group.ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, SponsorContract>> ListActiveByTeamIdsAsync(
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        var ids = teamIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, SponsorContract>();
        }

        // A club cannot hold two live deals — `Team.SignSponsorContract` refuses it — so the
        // newest wins rather than the first row to come back. The ordering is here to make that
        // true in the database rather than to hope it is.
        var contracts = await _dbContext.SponsorContracts
            .AsNoTracking()
            .Include(contract => contract.Sponsor)
            .Where(contract => ids.Contains(contract.TeamId))
            .Where(contract => contract.Status == SponsorContractStatus.Active
                               && contract.ContractMatches > contract.MatchesPlayed)
            .OrderByDescending(contract => contract.SignedAt)
            .ToListAsync(cancellationToken);

        return contracts
            .GroupBy(contract => contract.TeamId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public async Task AddAsync(SponsorContract contract, CancellationToken cancellationToken = default) =>
        await _dbContext.SponsorContracts.AddAsync(contract, cancellationToken);

    public void Update(SponsorContract contract) =>
        _dbContext.SponsorContracts.Update(contract);
}
