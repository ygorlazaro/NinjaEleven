using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// The calendar of a season: the matchdays, and therefore the windows of football in them.
/// </summary>
public interface IMatchDayRepository
{
    Task<IReadOnlyList<MatchDay>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task<MatchDay?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every matchday from the beginning of the calendar up to the given date, across every
    /// season.
    ///
    /// <para>
    /// This is the question the world asks when it wakes up: which days are in reach, and
    /// which of them it has not played. The lower end is the calendar's own beginning and not
    /// a date somebody chose, because a matchday that is behind is owed — a world that was
    /// down over a matchday still has to play it, and a bound of "yesterday" quietly caps how
    /// far behind the world may fall at the price of never playing anything again.
    /// </para>
    ///
    /// <para>
    /// The upper end is a real bound. A day in the future has not happened, and offering it
    /// would be a window that goes out before the day it belongs to.
    /// </para>
    /// </summary>
    /// <param name="to">The latest day to consider, inclusive.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<MatchDay>> ListUntilAsync(
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<MatchDay> matchDays, CancellationToken cancellationToken = default);
}
