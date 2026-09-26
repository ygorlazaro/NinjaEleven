using FootballManager.Domain.Seasons;

namespace FootballManager.Application.Repositories;

public interface ISeasonRepository
{
    Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken = default);
    Task<Season?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Season?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Season season, CancellationToken cancellationToken = default);
    void Update(Season season);
}
