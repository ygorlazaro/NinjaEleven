using NinjaEleven.Application.Services;

namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Closing a season, as the thing that moves the world sees it.
///
/// <para>
/// It is a seam of its own for the same reason <see cref="IHeadlessMatchPlayer"/> is: the
/// execution service walks windows and the close of a season is a different job with fifteen
/// dependencies of its own — the tables, the purse, the trophies, the pyramid, the rosters and
/// the calendar of the season after. Naming the two questions here is what keeps "play the next
/// window" and "pay a season out" from being one method that does both.
/// </para>
///
/// <para>
/// Both answers are asked of the calendar rather than worked out here. Whether a season is over
/// is read from its fixtures, so a season left half played by a restart is not a season that is
/// over, and a season closed over a hole would pay its purses before its last matchday was
/// played.
/// </para>
/// </summary>
public interface ISeasonCloser
{
    /// <summary>Whether every fixture of every window of the season has been played.</summary>
    Task<bool> IsFinishedAsync(Guid seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pays what is owed, writes the trophies, marks the season finished and — unless asked not
    /// to — opens the one after it, which is where the next season's day one and its Supercup
    /// come from.
    /// </summary>
    Task<SeasonCloseResult> CloseAsync(
        Guid seasonId,
        bool openTheNextSeason = true,
        CancellationToken cancellationToken = default);
}
