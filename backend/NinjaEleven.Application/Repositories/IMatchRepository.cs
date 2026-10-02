using NinjaEleven.Application.Models;
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

    /// <summary>
    /// Matches that are still on the pitch on a fixture that has already been decided.
    ///
    /// <para>
    /// A live match holds its fixture: the database refuses a second one for the same fixture
    /// while the first is running, so an orphan is not a row nobody looks at — it is a row
    /// that stops its fixture from ever being played again, and no walk will ask about it,
    /// because a fixture that is already decided is a fixture every walk has finished with.
    /// That is how twenty-one matches ended up holding index entries on fixtures the season
    /// had already settled, keeping a whole window of the world owed for ever.
    /// </para>
    ///
    /// <para>
    /// It is read as one question rather than as an unfinished list and a fixture read per
    /// row: the answer is a join, and a sweep that asked once per match would be a sweep that
    /// is itself too slow to run.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Match>> ListUnfinishedOnFinishedFixturesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The matches that are on the pitch right now and are not being played by this process,
    /// with the window each of them belongs to.
    ///
    /// A match's working state lives in the memory of whoever kicked it off, and after this
    /// change that is not necessarily the API: the Scheduler plays the matches nobody is
    /// watching. Those matches are still real, still have events, and a client following one
    /// has to be shown them — so the process that owns the connections reads the rows the
    /// other process is writing. The window comes with the match because the scoreboard of a
    /// matchday is addressed by window, and asking for it once per second per match is the
    /// read a matchday is made of.
    /// </summary>
    /// <param name="hostId">The process doing the asking; its own matches are not returned.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<LiveMatchRow>> ListLiveExceptHostAsync(
        string hostId,
        CancellationToken cancellationToken = default);

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
    /// Takes back the lines of a match that is being given up on.
    ///
    /// <para>
    /// A match that is abandoned never happened as football: its fixture is reopened and
    /// played again from the whistle, so the twenty-two lines it wrote when the whistle went
    /// are a record of an evening that was thrown away. Left in place they are read as
    /// football — a scorers table is counted from these lines and nothing on that walk knows
    /// the match was void — so a process that restarted at minute eighty leaves goals in a
    /// season's artilharia that no table will ever explain, and a striker is credited with a
    /// game the fixture has already had a different answer to.
    /// </para>
    ///
    /// <para>
    /// The match row and its events stay: the fact that it was started and given up on is what
    /// explains the fixture being played twice, and a record of the attempt is not the problem.
    /// The lines are, because they are the ones that are summed.
    /// </para>
    /// </summary>
    Task RemovePlayerStatisticsAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The match lines of a set of matches, keyed by match.
    ///
    /// A report is written about a whole match at once: the eleven that started, the men who
    /// came on, who was booked, who scored. Reading those lines a player at a time is
    /// twenty-two questions asked of the database to answer one about a match, and the answer
    /// is about a match that has already been played and will not need asking again.
    /// </summary>
    Task<IReadOnlyList<MatchPlayerStatistics>> ListPlayerStatisticsByMatchIdsAsync(
        IEnumerable<Guid> matchIds,
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
    /// The shape a club last put on the pitch with, or null when it has not finished a match
    /// that recorded one.
    ///
    /// <para>
    /// This is a standing order's memory. A manager who wrote nothing this week has still been
    /// playing a shape all season, and a club that kicks off with no order is better off
    /// repeating the last one than repeating nothing — the difference between a habit and an
    /// accident.
    /// </para>
    ///
    /// <para>
    /// It is asked of finished matches only, and of the club's own games in any competition, so
    /// a club whose last football was in the cup is not handed back the shape it happened to
    /// use in the league that week.
    /// </para>
    /// </summary>
    Task<string?> GetLastTacticCodeAsync(
        Guid teamId,
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

    /// <summary>
    /// Every meeting two clubs have ever played, counted.
    ///
    /// <para>
    /// It is a different question from <see cref="GetHeadToHeadAsync"/>, which is a page of
    /// the most recent ones, and it is asked in the database rather than in a loop over that
    /// page: a rivalry of forty years is forty rows summed and not forty queries read, and a
    /// limit on the page must not quietly become a limit on the record.
    /// </para>
    /// </summary>
    Task<Application.Models.HeadToHeadSummary> GetHeadToHeadSummaryAsync(
        Guid teamId,
        Guid opponentId,
        CancellationToken cancellationToken = default);
}
