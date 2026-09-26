using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Competitions;

namespace FootballManager.Application.Services;

public class RoundService
{
    private readonly IRoundRepository _roundRepository;

    public RoundService(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<IReadOnlyList<Round>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _roundRepository.ListAsync(cancellationToken);

    public async Task<Round> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _roundRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Round", id);

    public async Task<IReadOnlyList<Round>> GetByCompetitionSeasonAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await _roundRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);
}
