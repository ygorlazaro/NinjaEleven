using FootballManager.Domain.Matches;

namespace FootballManager.Application.Repositories;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> ListAsync(CancellationToken cancellationToken = default);
    Task<Match?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Match?> GetByFixtureAsync(Guid fixtureId, CancellationToken cancellationToken = default);
    Task AddAsync(Match match, CancellationToken cancellationToken = default);
    void Update(Match match);

    /// <summary>
    /// Persisted event log of a match, ordered by sequence. A client that reconnects
    /// uses it to fill the gap between the snapshot and the new SignalR events.
    /// </summary>
    Task<IReadOnlyList<MatchEvent>> ListEventsAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends the events produced by a tick to the persisted log.
    /// </summary>
    Task AddEventsAsync(IEnumerable<MatchEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Structured statistics of a match, including the card counters used by the
    /// classification tiebreakers.
    /// </summary>
    Task<MatchStatistics?> GetStatisticsAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the structured statistics of a finished match, so a results screen can
    /// be served after the live session is gone.
    /// </summary>
    Task AddStatisticsAsync(MatchStatistics statistics, CancellationToken cancellationToken = default);
}
