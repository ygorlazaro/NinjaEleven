using FootballManager.Application.Abstractions;
using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Competitions;
using FootballManager.Domain.Enums;

namespace FootballManager.Application.Services;

public class CompetitionService
{
    private readonly ICompetitionRepository _competitionRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompetitionService(
        ICompetitionRepository competitionRepository,
        ISeasonRepository seasonRepository,
        IUnitOfWork unitOfWork)
    {
        _competitionRepository = competitionRepository;
        _seasonRepository = seasonRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Competition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _competitionRepository.ListAsync(cancellationToken);

    public async Task<Competition> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _competitionRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Competition", id);

    public async Task<IReadOnlyList<Competition>> GetBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        return await _competitionRepository.ListBySeasonAsync(seasonId, cancellationToken);
    }

    public async Task<Competition> CreateAsync(
        string name,
        CompetitionType type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("CompetitionNameRequired", "The competition name is required.");
        }

        var competition = Competition.Create(name.Trim(), type);
        await _competitionRepository.AddAsync(competition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return competition;
    }
}
