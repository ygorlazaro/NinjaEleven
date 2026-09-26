using FootballManager.Application.Abstractions;
using FootballManager.Application.Repositories;
using FootballManager.Application.Services;
using FootballManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FootballManager.Infrastructure;

/// <summary>
/// EF Core implementation of the unit of work: it simply commits everything the
/// repositories tracked during the use case.
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly FootballManagerDbContext _dbContext;

    public EfUnitOfWork(FootballManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
