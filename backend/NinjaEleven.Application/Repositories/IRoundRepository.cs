using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Repositories;

public interface IRoundRepository
{
    Task<IReadOnlyList<Round>> ListAsync(CancellationToken cancellationToken = default);
    Task<Round?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> ListByCompetitionSeasonAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The windows scheduled on one matchday, across every competition. A matchday scoreboard
    /// shows all of them, and a window's recovery is applied when the last of its fixtures is
    /// finished, so both need the day rather than one competition's part of it.
    /// </summary>
    Task<IReadOnlyList<Round>> ListByMatchDayAsync(
        Guid matchDayId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Round round, CancellationToken cancellationToken = default);
    void Update(Round round);

    /// <summary>Drops windows that were scheduled and never filled. Same reason as fixtures.</summary>
    void RemoveRange(IEnumerable<Round> rounds);
}
