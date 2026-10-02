using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A window of football the calendar says is due, and the matchday it belongs to.
/// </summary>
/// <param name="MatchDayId">The day.</param>
/// <param name="MatchDayNumber">Which day of the season it is, counted from one.</param>
/// <param name="RoundId">The window itself: one competition's football on that day.</param>
/// <param name="RoundNumber">The window's own number inside its competition.</param>
/// <param name="Wave">Which window of the day it is, which is what says how late it went out.</param>
/// <param name="KickOffAt">The instant the calendar says it should have kicked off.</param>
public record DueRound(
    Guid MatchDayId,
    int MatchDayNumber,
    Guid RoundId,
    int RoundNumber,
    CompetitionType Wave,
    DateTimeOffset KickOffAt);

/// <summary>
/// What one step of the world did, when a person walked it by hand.
/// </summary>
/// <param name="Kind">Which of the three things an advance can be.</param>
/// <param name="SeasonId">The season the step belongs to.</param>
/// <param name="SeasonName">Its name, so a caller can say "Temporada II" without reading it.</param>
/// <param name="MatchDayNumber">Which day of the season was played, when a window was.</param>
/// <param name="MatchDayDate">The date of that day, which is what the calendar says it is.</param>
/// <param name="Wave">
/// Which window of that day was played: the championship, the cup or the Supercup. A day with a
/// cup leg in it is two advances, not one, because its football is six hours apart.
/// </param>
/// <param name="Rounds">Every window of football the step played, and what happened in each.</param>
/// <param name="SeasonClosed">Whether the season was closed and the next one opened by this step.</param>
/// <param name="SeasonsOpened">The season opened after the one that was closed, when there was one.</param>
public record WorldAdvance(
    WorldAdvanceKind Kind,
    Guid? SeasonId,
    string? SeasonName,
    int? MatchDayNumber,
    DateOnly? MatchDayDate,
    CompetitionType? Wave,
    IReadOnlyList<RoundRun> Rounds,
    bool SeasonClosed,
    Guid? SeasonOpened)
{
    /// <summary>An advance that had nothing to do, which is not a failure.</summary>
    public static WorldAdvance Nothing() =>
        new(WorldAdvanceKind.Nothing, null, null, null, null, null, Array.Empty<RoundRun>(), false, null);
}

/// <summary>The three things walking the world by hand can be.</summary>
public enum WorldAdvanceKind
{
    /// <summary>
    /// There was no window left to play and no season left to close. The world is where it is
    /// and a person asking it to move has been told there is nothing to move.
    /// </summary>
    Nothing = 0,

    /// <summary>
    /// One window of football was played: a day, and one of the windows in it. Every division's
    /// round of a championship day is one advance, because every division plays in the same wave.
    /// </summary>
    Window = 1,

    /// <summary>
    /// A season was over. The last window of a season is its final's second leg, and playing it
    /// is what closes the season, pays what is owed and draws the next one — the new pyramid,
    /// the new rosters and the Supercup of day one.
    /// </summary>
    SeasonClosed = 2
}

/// <summary>
/// What happened to one fixture while its window was being played.
/// </summary>
/// <param name="FixtureId">The fixture.</param>
/// <param name="MatchId">The match that was played, when one was.</param>
/// <param name="HomeScore">Goals scored by the home club.</param>
/// <param name="AwayScore">Goals scored by the away club.</param>
/// <param name="Status">
/// <see cref="FixtureRunStatus.Finished"/>, <see cref="FixtureRunStatus.Failed"/> when it
/// threw, and <see cref="FixtureRunStatus.PlayedElsewhere"/> when another process is holding
/// the match's working memory. None of them is a retry and none of them is forgotten: the
/// window is only completed when every one of them is finished.
/// </param>
/// <param name="Error">Why it failed, when it did.</param>
public record FixtureRun(
    Guid FixtureId,
    Guid? MatchId,
    int? HomeScore,
    int? AwayScore,
    FixtureRunStatus Status,
    string? Error = null);

/// <summary>How a fixture ended up when its window was played.</summary>
public enum FixtureRunStatus
{
    /// <summary>Played from kick-off to full time by this process.</summary>
    Finished = 0,

    /// <summary>
    /// It threw. The fixtures before it keep their results and this one keeps its own state,
    /// so the next run of the window picks up here rather than starting the window again.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Somebody else is playing it. Its working memory is in another process, so this one
    /// leaves it alone; the window is not completed until that match is finished.
    /// </summary>
    PlayedElsewhere = 2,

    /// <summary>
    /// It was finished before this run started. This is the shape an idempotent execution
    /// takes on the second attempt, and the shape a restart takes on the fixtures that were
    /// played before the process went down.
    /// </summary>
    AlreadyFinished = 3,

    /// <summary>
    /// It is the manager's own club's match, and it was started rather than walked through in
    /// one go: the world opens it and hands the clock to the loop, so his game is played out in
    /// the time a match takes and he can watch it, claim it and make his own substitutions
    /// while it runs — or sleep through it, and find the result of a game that really happened
    /// rather than a fixture still sitting at minute zero. The window stays owed until that
    /// match reaches the final whistle, and the window closes on the match he finished or
    /// nobody did.
    /// </summary>
    StartedForTheManager = 4
}

/// <summary>
/// What one window of football cost, and what it left behind.
/// </summary>
/// <param name="RoundId">The window.</param>
/// <param name="Claim">Whether this process was the one that played it.</param>
/// <param name="Fixtures">Every fixture the window held, and what happened to each.</param>
/// <param name="Duration">How long the whole window took.</param>
public record RoundRun(
    Guid RoundId,
    RoundClaim Claim,
    IReadOnlyList<FixtureRun> Fixtures,
    TimeSpan Duration)
{
    /// <summary>The matches played by this run, from kick-off to the final whistle.</summary>
    public int Played => Fixtures.Count(run => run.Status is FixtureRunStatus.Finished);

    /// <summary>The fixtures that were already finished when this run began.</summary>
    public int AlreadyPlayed => Fixtures.Count(run => run.Status is FixtureRunStatus.AlreadyFinished);

    /// <summary>The fixtures that threw. One of them is why a window was not completed.</summary>
    public int Failed => Fixtures.Count(run => run.Status is FixtureRunStatus.Failed);

    /// <summary>The fixtures another process is playing right now.</summary>
    public int PlayedElsewhere => Fixtures.Count(run => run.Status is FixtureRunStatus.PlayedElsewhere);

    /// <summary>The fixtures that were started for the manager and are being played live right now.</summary>
    public int StartedForTheManager => Fixtures.Count(run => run.Status is FixtureRunStatus.StartedForTheManager);

    /// <summary>
    /// Whether the window has been played out in full. A window is only completed on this
    /// answer, so a round that failed half way through stays open and is finished by the run
    /// that picks up the fixtures that are left.
    /// </summary>
    public bool IsComplete => Fixtures.Count > 0 && Played + AlreadyPlayed == Fixtures.Count;
}
