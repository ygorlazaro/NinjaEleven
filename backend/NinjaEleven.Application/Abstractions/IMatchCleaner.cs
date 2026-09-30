namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Closes the matches the world has stopped driving.
/// </summary>
public interface IMatchCleaner
{
    /// <summary>
    /// Abandons every match still on the pitch whose fixture has already been decided.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many matches were closed.</returns>
    Task<int> AbandonMatchesOnDecidedFixturesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the match of a fixture that somebody started and nobody ever touched: it is
    /// still on the touchline at minute zero with no manager behind it, so the world takes
    /// the fixture back and plays it rather than waiting for ever for somebody who is not
    /// coming.
    ///
    /// <para>
    /// A match is left for the manager because a manager who cannot watch his own game
    /// being played for him is not playing the game. He is given the chance, and the chance
    /// is measured in something other than good intentions: a match that has been ticked is
    /// being played by somebody, and one that has not been touched since it was opened
    /// belongs to nobody. Without this a world handed one match nobody wanted could never
    /// move again, and it would say so by playing nothing at all.
    /// </para>
    /// </summary>
    /// <param name="fixtureId">The fixture whose match is sitting on the touchline.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Whether a match was closed, so the fixture could be played again.</returns>
    Task<bool> ReleaseTheUntouchedMatchAsync(Guid fixtureId, CancellationToken cancellationToken = default);
}
