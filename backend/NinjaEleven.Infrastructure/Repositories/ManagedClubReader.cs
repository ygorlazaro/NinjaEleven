using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

/// <summary>
/// Reads the clubs a person is in charge of.
/// </summary>
/// <remarks>
/// The world seeds a manager for every club so the transfer market has somebody to buy and
/// sell with, and those managers have no user behind them. A club somebody is actually
/// playing is a different row: it is the manager with a user, and it is the one whose
/// matches are left on the touchline rather than simulated.
/// </remarks>
public class ManagedClubReader : IManagedClubReader
{
    private readonly NinjaElevenDbContext _dbContext;

    public ManagedClubReader(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> ListManagedClubsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Managers
            .Where(manager => manager.UserId != null)
            .Select(manager => manager.TeamId)
            .ToListAsync(cancellationToken);
}
