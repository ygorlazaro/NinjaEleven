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
    /// Kicks a match off and hands the clock to this process's loop, which plays it to the
    /// final whistle in the time a match takes.
    ///
    /// <para>
    /// It is how the world opens the manager's own match, and it is the only difference between
    /// that match and the thirty-one others of the day: the same kick-off, the same lineup, the
    /// same bench, the same events — but played out in real time rather than walked through in
    /// one go, so the manager's own game is the one thing in the world he can watch live.
    /// </para>
    ///
    /// <para>
    /// It is played whether or not he is there. A club whose manager is asleep does not stop
    /// playing football: the match runs, the feed runs, the window closes on the final whistle,
    /// and the manager who opens it the next morning finds the result of a game that really
    /// happened rather than a fixture stuck at minute zero waiting for him. Nobody has to be
    /// online for a matchday to finish — and that is a rule about the world, not a courtesy
    /// extended to the manager who happens to be looking.
    /// </para>
    ///
    /// <para>
    /// And when he does arrive mid-match, his screen claims it: the clock stays where it is and
    /// the keyboard changes hands, which is the seam <c>AttachManager</c> is.
    /// </para>
    /// </summary>
    /// </summary>
    /// <param name="fixtureId">The fixture of the manager's own club.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The kick-off's own answer.</returns>
    Task<HeadlessMatchResult> StartAndLetTheLoopRunAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default);
}
