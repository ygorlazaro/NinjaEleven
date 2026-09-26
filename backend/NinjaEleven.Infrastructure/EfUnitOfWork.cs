using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure;

/// <summary>
/// EF Core implementation of the unit of work: it simply commits everything the
/// repositories tracked during the use case.
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly NinjaElevenDbContext _dbContext;

    public EfUnitOfWork(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
