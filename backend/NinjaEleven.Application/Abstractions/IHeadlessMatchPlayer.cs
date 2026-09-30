using NinjaEleven.Application.Models;

namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// What came of playing one fixture with nobody watching it.
/// </summary>
/// <param name="Started">Whether this process kicked the match off.</param>
/// <param name="MatchId">The match it is about, when there is one to name.</param>
/// <param name="Refusal">Why it was not played, when it was not.</param>
/// <param name="Message">The refusal in words, for the log.</param>
/// <param name="HomeScore">Goals by the home club, when the match was played here.</param>
/// <param name="AwayScore">Goals by the away club, when the match was played here.</param>
public record HeadlessMatchResult(
    bool Started,
    Guid MatchId,
    MatchRefusal Refusal,
    string? Message,
    int? HomeScore = null,
    int? AwayScore = null);

/// <summary>
/// Plays one fixture from the whistle to the final whistle with nobody holding the keyboard.
/// </summary>
/// <para>
/// It is a seam of its own rather than a method on the execution service for one reason: a
/// match takes about a hundred ticks, and each of them is its own commit. Playing a whole
/// matchday's thirty-two fixtures in one unit of work would leave the change tracker holding
/// every event of every match of the day for as long as the day lasted, and the world would
/// get slower as the round got longer. So each fixture is played in a scope of its own, and
/// the thing that creates it is named here.
/// </para>
///
/// <para>
/// Everything else about it is deliberately ordinary: it goes through the same
/// <see cref="Services.MatchService"/> a manager's own game goes through, so a match nobody
/// watched produces the same events, the same statistics, the same books and the same inbox as
/// one somebody did.
/// </para>
/// </summary>
public interface IHeadlessMatchPlayer
{
    Task<HeadlessMatchResult> PlayAsync(Guid fixtureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kicks a match off and walks no part of it: the clock is started, the lineup and the
    /// bench are locked, the events begin — and then nobody touches it.
    ///
    /// <para>
    /// It is how the world opens the manager's own match. A manager who cannot watch his own
    /// game being played for him is not playing the game, and a window that simulated it would
    /// hand him a result for an evening he sat through on another screen. So the world starts
    /// it, leaves the session marked as nobody's to drive, and waits: the manager's screen
    /// claims the match when he gets there, and the window closes on the match he finished.
    /// </para>
    /// </summary>
    /// <param name="fixtureId">The fixture of the manager's own club.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The kick-off's own answer.</returns>
    Task<HeadlessMatchResult> StartAndLeaveAsync(Guid fixtureId, CancellationToken cancellationToken = default);
}
