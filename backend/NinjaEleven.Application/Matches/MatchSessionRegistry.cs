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
    /// substituting for his side. It is never flipped back — a manager who leaves mid
    /// match is expected to come back to the screen he left, the same as a match he
    /// started himself.
    /// </summary>
    public bool AutoContinue { get; set; }

    /// <summary>
    /// Who is moving this match's clock, when the answer is not simply "the loop".
    ///
    /// <para>
    /// Three things can start a match and only one of them may drive it. The headless player
    /// walks a match of the world's own from whistle to whistle in one go, and the loop must
    /// keep its hands off it while it does — two drivers on one match is a match played at
    /// double speed and then asked for a second half it does not have. The world can also
    /// open the manager's own match and stop, leaving it on the touchline for him, and then
    /// nobody may drive that one either: a match nobody is playing is a match the manager can
    /// still arrive at, and a loop that ran it out from under him is the world playing his
    /// evening for him.
    /// </para>
    ///
    /// <para>
    /// <see cref="None"/> is every other match — a manager's own from the lineup screen, and a
    /// wave the matchday opened and left to the loop — and those are the ones the loop drives.
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
    WalkedByTheWorld = 1,

    /// <summary>
    /// Nobody: the world opened it for the manager and stopped. It waits for him.
    /// </summary>
    LeftForTheManager = 2
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
