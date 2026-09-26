namespace FootballManager.Application.Abstractions;

/// <summary>
/// Persists the changes accumulated by the repositories during a use case. Services
/// never touch EF Core; they only ask the unit of work to commit.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
