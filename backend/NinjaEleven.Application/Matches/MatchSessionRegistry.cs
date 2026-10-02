using System.Collections.Concurrent;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Matches;

/// <summary>
/// A match that is being played right now. The engine is kept together with its state
/// because the random source is stateful: dropping either one mid match would change
/// the stream of rolls and break the replay guarantee of a given seed.
/// </summary>
public sealed class LiveMatch
{
    public LiveMatch(
        Guid matchId,
        Guid roundId,
        bool autoContinue,
        MatchEngine engine,
        MatchState state)
    {
        MatchId = matchId;
        RoundId = roundId;
        AutoContinue = autoContinue;
        Engine = engine;
        State = state;
    }

    public Guid MatchId { get; }

    /// <summary>Round the fixture belongs to, so a client can follow a whole matchday.</summary>
    public Guid RoundId { get; }

    /// <summary>
    /// True for the matches nobody is watching: the ones of the other clubs in the
    /// round. They are simulated by the same loop, but nobody is there to leave the
    /// half-time pause, so the loop does it for them.
    ///
    /// Set to false the moment a manager lays claim to a match: the match keeps on the
    /// clock it already had, but the interval becomes the manager's and the engine stops
    /// substituting for his side.
    ///
    /// <para>
    /// It is flipped back when the claim goes stale. A manager who leaves mid match was
    /// expected to come back, and most of the time he does — but the two decisions that hold
    /// the clock for him (who takes the penalty, who comes on for the man who cannot carry
    /// on) would then hold it for ever, and a world that waits for ever plays nothing at
    /// all. So a claim is honoured while it is being answered and expires after
    /// <see cref="MatchRules.ManagerDecisionTimeoutSeconds"/> of a manager not answering,
    /// at which point the engine answers for him.
    /// </para>
    /// </summary>
    public bool AutoContinue { get; set; }

    /// <summary>
    /// When the manager claimed this match, or null for a match nobody has claimed.
    ///
    /// It is what makes the claim expire. The wait is measured from the moment he took the
    /// keyboard, so a match claimed by a tab that was closed the moment it opened cannot sit
    /// on a question for the length of a match.
    /// </summary>
    public DateTimeOffset? ManagerClaimedAt { get; set; }

    /// <summary>
    /// When the window for naming a penalty taker closes, or null when none is open.
    ///
    /// <para>
    /// The window is the only one in a match that still stops the clock, and it is the
    /// backend's window: <see cref="MatchRules.PenaltySelectionSeconds"/> from the moment
    /// the penalty is awarded, after which the engine sends somebody to the spot by itself.
    /// The screen draws the count that is left; it does not own it, because a rule kept in
    /// the client is a rule a closed tab stops applying.
    /// </para>
    /// </summary>
    public DateTimeOffset? PenaltyEndsAt { get; set; }

    /// <summary>
    /// When the interval closes by itself, or null while the match is not at one.
    ///
    /// <para>
    /// Twenty seconds of break, the backend's number, after which the second half begins
    /// whether or not anybody pressed anything. A manager may end it early — that is what
    /// the button on the interval is for — but he may not keep it open, because a break
    /// that waits for a screen is a fixture that never finishes and a window that never
    /// closes.
    /// </para>
    /// </summary>
    public DateTimeOffset? HalfTimeEndsAt { get; set; }

    /// <summary>
    /// When the window for replacing a man who cannot carry on closes, or null when none
    /// is open. The clock does not stop for this one: the club plays the man down until
    /// somebody is named or the engine names somebody.
    /// </summary>
    public DateTimeOffset? InjuryWindowEndsAt { get; set; }

    /// <summary>
    /// Who is moving this match's clock, when the answer is not simply "the loop".
    ///
    /// <para>
    /// Two things can be asked to start a match and only one of them may drive it. The headless
    /// player walks a match of the world's own from whistle to whistle in one go, and the loop
    /// must keep its hands off it while it does — two drivers on one match is a match played at
    /// double speed and then asked for a second half it does not have.
    /// </para>
    ///
    /// <para>
    /// <see cref="None"/> is every other match: a wave the matchday opened, a match a manager
    /// started from the lineup screen, and the manager's own match when the world opened it. The
    /// last one is the important one — a club somebody is in charge of is played by the loop like
    /// any other, in the time a match takes, so a manager who is not at his screen does not leave
    /// his fixture stuck at minute zero holding its window and the season behind it.
    /// </para>
    ///
    /// <para>
    /// There is deliberately no driver that means "nobody". A match with no driver is a match
    /// that never finishes, and the only two answers to that question are the walk and the loop.
    /// </para>
    /// </summary>
    public MatchDriver Driver { get; set; } = MatchDriver.None;

    public MatchEngine Engine { get; }
    public MatchState State { get; }

    /// <summary>
    /// Guards the state. Ticks can arrive from the REST command endpoint and, later,
    /// from the hub, so the clock is never advanced by two callers at once.
    /// </summary>
    public object Gate { get; } = new();
}

/// <summary>Whose clock a live match is on.</summary>
public enum MatchDriver
{
    /// <summary>The background loop, because nobody else has a claim on it.</summary>
    None = 0,

    /// <summary>
    /// The headless player, which is walking it from kick-off to the final whistle in one go.
    /// </summary>
    WalkedByTheWorld = 1
}

/// <summary>
/// In memory registry of the matches currently being played. The persisted match row
/// stays the source of truth for history; this only holds the live working memory of
/// a match that has not finished yet.
/// </summary>
public interface IMatchSessionRegistry
{
    void Register(LiveMatch session);
    bool TryGet(Guid matchId, out LiveMatch session);
    void Remove(Guid matchId);

    /// <summary>
    /// Matches currently being played. The background loop walks this list, which is
    /// what guarantees a single simulation per match instead of one per caller.
    /// </summary>
    IReadOnlyCollection<Guid> ActiveMatchIds { get; }
}

public sealed class MatchSessionRegistry : IMatchSessionRegistry
{
    private readonly ConcurrentDictionary<Guid, LiveMatch> _sessions = new();

    public IReadOnlyCollection<Guid> ActiveMatchIds => _sessions.Keys.ToList();

    public void Register(LiveMatch session) => _sessions[session.MatchId] = session;

    public bool TryGet(Guid matchId, out LiveMatch session) => _sessions.TryGetValue(matchId, out session!);

    public void Remove(Guid matchId) => _sessions.TryRemove(matchId, out _);
}
