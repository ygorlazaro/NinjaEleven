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
    /// </summary>
    public bool AutoContinue { get; }

    public MatchEngine Engine { get; }
    public MatchState State { get; }

    /// <summary>
    /// Guards the state. Ticks can arrive from the REST command endpoint and, later,
    /// from the hub, so the clock is never advanced by two callers at once.
    /// </summary>
    public object Gate { get; } = new();
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
