using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Application.Repositories;

public interface ISeasonRepository
{
    Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken = default);
    Task<Season?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The season with a given number. A season is identified by the number it was given and
    /// not by the year it fell in, so "the season after this one" is a lookup by number.
    /// </summary>
    Task<Season?> GetByNumberAsync(int number, CancellationToken cancellationToken = default);

    Task<Season?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Season season, CancellationToken cancellationToken = default);
    void Update(Season season);
}
