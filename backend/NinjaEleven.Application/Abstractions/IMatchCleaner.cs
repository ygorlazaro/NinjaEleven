namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Closes the matches the world has stopped driving.
/// </summary>
public interface IMatchCleaner
{
    /// <summary>
    /// Puts back on the schedule every fixture that says it was played and has no result to
    /// show for it.
    ///
    /// <para>
    /// This runs before the sweep that closes orphans, and it runs first on purpose. A fixture
    /// is believed by three readers — the window that closes itself, the claim that completes
    /// it, and the sweep below — and a fixture column nothing checks is a matchday that can
    /// disappear from a season without a line of log anywhere. Six fixtures of a second
    /// division were lost exactly this way, and the table above them was a game short for
    /// eleven of its sixteen clubs for the rest of the campaign.
    /// </para>
    ///
    /// <para>
    /// Reopening is the whole of the repair, and it is enough: the claim reads the fixtures
    /// rather than its own two columns, so a fixture back on the schedule puts its window back
    /// on the calendar and the next walk plays it.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many fixtures were put back on the schedule.</returns>
    Task<int> ReopenTheFixturesNobodyDecidedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons every match still on the pitch whose fixture has already been decided.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many matches were closed.</returns>
    Task<int> AbandonMatchesOnDecidedFixturesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the match of a fixture that somebody opened and no process is playing: it is
    /// still on the pitch at minute zero with no working memory behind it, so the world takes
    /// the fixture back and plays it rather than holding it for ever.
    ///
    /// <para>
    /// A match somebody opened and never touched belongs to nobody: it is at minute zero, and
    /// the only things that can own a match are a walk in progress or the loop. Without this a
    /// world handed one match nothing was driving could never move again, and it would say so
    /// by playing nothing at all.
    /// </para>
    /// </summary>
    /// <param name="fixtureId">The fixture whose match is sitting there doing nothing.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Whether a match was closed, so the fixture could be played again.</returns>
    Task<bool> ReleaseTheUntouchedMatchAsync(Guid fixtureId, CancellationToken cancellationToken = default);
}
