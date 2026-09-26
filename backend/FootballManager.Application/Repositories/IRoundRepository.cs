using FootballManager.Domain.Competitions;

namespace FootballManager.Application.Repositories;

public interface IRoundRepository
{
    Task<IReadOnlyList<Round>> ListAsync(CancellationToken cancellationToken = default);
    Task<Round?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> ListByCompetitionSeasonAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);
    Task AddAsync(Round round, CancellationToken cancellationToken = default);
}
