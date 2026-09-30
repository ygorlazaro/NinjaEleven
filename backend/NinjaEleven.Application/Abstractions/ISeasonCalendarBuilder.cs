namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Draws the calendar of a season that has never been drawn.
/// </summary>
/// <remarks>
/// It is a seam of its own for the same reason <see cref="ISeasonCloser"/> is: whether a season
/// needs drawing and what drawing it does are the two questions the walking of the world asks,
/// and it must not be able to answer them with a second calendar of its own.
/// </remarks>
public interface ISeasonCalendarBuilder
{
    /// <summary>
    /// Draws the whole season, or leaves the calendar that is already there alone.
    /// </summary>
    /// <param name="seasonId">The season to draw.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DrawAsync(Guid seasonId, CancellationToken cancellationToken = default);
}
