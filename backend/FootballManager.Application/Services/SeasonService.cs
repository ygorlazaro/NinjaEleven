using FootballManager.Application.Abstractions;
using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Seasons;

namespace FootballManager.Application.Services;

public class SeasonService
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeasonService(ISeasonRepository seasonRepository, IUnitOfWork unitOfWork)
    {
        _seasonRepository = seasonRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _seasonRepository.ListAsync(cancellationToken);

    public async Task<Season> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _seasonRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Season", id);

    public async Task<Season> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        await _seasonRepository.GetCurrentAsync(cancellationToken)
            ?? throw new EntityNotFoundException("CurrentSeason", Guid.Empty);

    public async Task<Season> CreateAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("SeasonNameRequired", "O nome da temporada é obrigatório.");
        }

        var season = Season.Create(name.Trim(), startDate, endDate);
        await _seasonRepository.AddAsync(season, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return season;
    }
}
