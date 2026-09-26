using FootballManager.Domain.Matches;

namespace FootballManager.Application.Repositories;

public interface IFixtureRepository
{
    Task<IReadOnlyList<Fixture>> ListAsync(CancellationToken cancellationToken = default);
    Task<Fixture?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fixture>> ListByRoundAsync(Guid roundId, CancellationToken cancellationToken = default);
    Task AddAsync(Fixture fixture, CancellationToken cancellationToken = default);
    void Update(Fixture fixture);
}
