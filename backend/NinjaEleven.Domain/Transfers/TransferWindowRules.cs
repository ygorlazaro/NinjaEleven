namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// The rules that decide when a transfer is allowed to move.
///
/// A transfer is a promise about the future, and a promise is only kept when the calendar
/// reaches the day it named. So the whole rule is three windows and the arithmetic that says
/// which of them a proposal made today is waiting for:
///
/// <list type="bullet">
///   <item>
///     <b>After the Supercup.</b> The first match of a season is played between the two clubs
///     that won something in the last one, and it is the first thing the new season does. A
///     player who walks in after it has played is in the world before his new club's first
///     championship round, which is the only way a man arrives with a round to play in.
///   </item>
///   <item>
///     <b>After the tenth round of the championship.</b> The first mid-season window, and the
///     early one: a club that has played nine matches has seen its season, and a man signed in
///     October has the rest of the year to be part of it.
///   </item>
///   <item>
///     <b>After the twentieth round of the championship.</b> The second window, and the last
///     one. A proposal made after it has been answered waits for the Supercup of the next
///     season, which is the only arrival the rules name in a year after that.
///   </item>
/// </list>
///
/// A proposal is never refused for being early — it waits. What the window decides is
/// <em>when</em> the wait ends, and that is decided once, when the proposal is made, because a
/// deal that moved its own arrival date every time it was asked about would be a deal nobody
/// could plan a squad around.
///
/// The round a window is named for is the round whose closing delivers the man: the deals
/// settled when the tenth round closes are the ones the squad has for the eleventh. It is the
/// convention the season has always used — "after the eleventh round" was always a statement
/// about the closing — and both halves of it live in this file so a retune cannot move one and
/// leave the other behind.
///
/// Only a round of the championship counts. A cup tie and the Supercup are not a round of the
/// league, and a window measured in calendar days drifts; the number on the round is the fact.
/// </summary>
public static class TransferWindowRules
{
    /// <summary>
    /// The championship round whose closing brings the first mid-season window home.
    /// </summary>
    public const int FirstArrivalRound = 10;

    /// <summary>
    /// The championship round whose closing brings the second — and last — mid-season window
    /// home. A proposal made after this round has been answered is a proposal waiting for the
    /// Supercup, which is the first match of the next season.
    /// </summary>
    public const int SecondArrivalRound = 20;

    /// <summary>
    /// Whether this championship round is a window. It is the two named rounds and nothing
    /// else: a round in between is a round the market runs on and a round no man arrives in,
    /// and calling every round from the first window to the last one a window would have the
    /// world settling the same deals twelve times over for no reason a reader could name.
    /// </summary>
    public static bool IsOpen(int roundNumber) =>
        roundNumber == FirstArrivalRound || roundNumber == SecondArrivalRound;

    /// <summary>
    /// The season a proposal made in the round given is due to arrive in.
    ///
    /// While a mid-season window is still ahead of it the player arrives in this season; after
    /// the last of them the next thing that happens is the Supercup, which is the first match
    /// of the next season and the arrival the rules name before any other.
    /// </summary>
    public static int ArrivalSeasonNumberFor(int currentRound, int currentSeasonNumber) =>
        currentRound <= SecondArrivalRound ? currentSeasonNumber : currentSeasonNumber + 1;

    /// <summary>
    /// The round of the arrival season the player walks in on, once that season exists: the
    /// window still ahead of the proposal, or the Supercup's own round when there is none left.
    /// </summary>
    public static int ArrivalRoundFor(int currentRound) =>
        currentRound <= FirstArrivalRound
            ? FirstArrivalRound
            : currentRound <= SecondArrivalRound
                ? SecondArrivalRound
                : 1;
}