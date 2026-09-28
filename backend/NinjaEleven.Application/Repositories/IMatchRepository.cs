using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Repositories;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> ListAsync(CancellationToken cancellationToken = default);
    Task<Match?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The match that belongs to a fixture right now: the live one, or the most recent
    /// finished one. An abandoned match is not one of them: it has no result and its
    /// fixture goes back on the schedule.
    /// </summary>
    Task<Match?> GetByFixtureAsync(Guid fixtureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Matches that are neither finished nor abandoned. After a restart these are the
    /// rows whose working memory is gone, so they are abandoned and their fixtures are
    /// put back on the schedule.
    /// </summary>
    Task<IReadOnlyList<Match>> ListUnfinishedAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Match match, CancellationToken cancellationToken = default);
    void Update(Match match);

    /// <summary>
    /// The matches played in a set of fixtures, finished or not.
    ///
    /// Both states are wanted at once and for different reasons: a table is built from the
    /// finished ones, a live table from the ones in progress, and a cup aggregate from the
    /// two legs of a tie whatever state they are in. Reading them separately to answer one
    /// question about a matchday is how the two tables end up disagreeing about what happened.
    /// </summary>
    Task<IReadOnlyList<Match>> ListByFixtureIdsAsync(
        IEnumerable<Guid> fixtureIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The statistics rows of a set of matches, keyed by match. A tiebreaker needs the card
    /// counters, and a card counter is only in the statistics row.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MatchStatistics>> ListStatisticsByMatchIdsAsync(
        IEnumerable<Guid> matchIds,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Writes the line of every player who took part in a match. Called once, when the
    /// whistle goes, because after that the live session is gone and this is the only
    /// record that the player was there.
    /// </summary>
    Task AddPlayerStatisticsAsync(
        IEnumerable<MatchPlayerStatistics> statistics,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A player's match lines, newest first. The season is optional: without it the whole
    /// career comes back, which is what a "total" row on a history is made of.
    /// </summary>
    Task<IReadOnlyList<MatchPlayerStatistics>> ListPlayerStatisticsAsync(
        Guid playerId,
        Guid? seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The matchdays of a season, in order. A transfer window is measured against the
    /// calendar, and the calendar is the matchdays — so the round a transfer is proposed in
    /// is read off the number of the day a match was played on.
    /// </summary>
    Task<IReadOnlyList<Domain.Competitions.MatchDay>> GetMatchDaysAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A player's whole match history with each match already resolved — opponent, score
    /// and round — because a history that has to be assembled row by row is a history that
    /// takes a hundred queries to draw.
    /// </summary>
    Task<IReadOnlyList<Application.Models.PlayerMatchRecord>> GetPlayerHistoryAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A club's last finished matches, newest first, each already resolved into opponent,
    /// goals for and against. A club looked up in a modal has to be drawn in one query: a
    /// form guide assembled a match at a time is a form guide that asks the database ten
    /// times to answer "how has this club been doing".
    ///
    /// Only finished matches are here. A match in progress has no result to show and an
    /// abandoned one never had one.
    /// </summary>
    Task<IReadOnlyList<Application.Models.TeamMatchRecord>> GetTeamHistoryAsync(
        Guid teamId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Head-to-head matches between two clubs, newest first. A club's history against a
    /// specific rival is a different question from its general run, and the screen that asks
    /// for it has a different purpose: it is the story of this particular rivalry.
    /// </summary>
    Task<IReadOnlyList<Application.Models.TeamMatchRecord>> GetHeadToHeadAsync(
        Guid teamId,
        Guid opponentId,
        int limit,
        CancellationToken cancellationToken = default);
}
