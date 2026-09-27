using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Application.Services;

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

    /// <summary>
    /// Creates the next season, or the first one.
    ///
    /// The number is worked out from the ones already there rather than being sent in, because
    /// the number is the season's identity and a client that chose it could choose it twice. A
    /// season called "Temporada IV" while the world is on III is a season nobody can order,
    /// and the name is derived from the number so the two can never come apart.
    /// </summary>
    public async Task<Season> CreateAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var existing = await _seasonRepository.ListAsync(cancellationToken);
        var next = existing.Count == 0 ? 1 : existing.Max(season => season.Number) + 1;

        var season = Season.Create(next, startDate, endDate);
        await _seasonRepository.AddAsync(season, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return season;
    }

    /// <summary>Creates a season with a number the caller insists on. Used by the seeder.</summary>
    public async Task<Season> CreateWithNumberAsync(
        int number,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (await _seasonRepository.GetByNumberAsync(number, cancellationToken) is not null)
        {
            throw new DomainValidationException(
                "SeasonNumberTaken",
                $"Já existe uma temporada com o número {number}.");
        }

        var season = Season.Create(number, startDate, endDate);
        await _seasonRepository.AddAsync(season, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return season;
    }
}
