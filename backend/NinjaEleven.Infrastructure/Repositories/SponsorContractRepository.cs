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

    public async Task AddAsync(SponsorContract contract, CancellationToken cancellationToken = default) =>
        await _dbContext.SponsorContracts.AddAsync(contract, cancellationToken);

    public void Update(SponsorContract contract) =>
        _dbContext.SponsorContracts.Update(contract);
}
