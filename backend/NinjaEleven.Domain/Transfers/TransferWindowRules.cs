namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// The rules that decide when a transfer is allowed to move.
///
/// A transfer is a promise about the future, and a promise is only kept when the calendar
/// reaches the day it named. So the whole rule is two windows and the arithmetic that says
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
///     <b>After the eleventh round of the championship.</b> The mid-season window, and the
///     reason a transfer is not simply "any time": a man who arrives in round four has played
///     three matches for his old club and none for his new one, and his new club's table is
///     already a season of somebody else's football.
///   </item>
/// </list>
///
/// A proposal is never refused for being early — it waits. What the window decides is
/// <em>when</em> the wait ends, and that is decided once, when the proposal is made, because a
/// deal that moved its own arrival date every time it was asked about would be a deal nobody
/// could plan a squad around.
///
/// Only a round of the championship counts. A cup tie and the Supercup are not a round of the
/// league, and a window measured in calendar days drifts; the number on the round is the fact.
/// </summary>
public static class TransferWindowRules
{
    /// <summary>
    /// The first championship round after which a player may arrive mid-season. A proposal made
    /// before this round is played is waiting for it.
    /// </summary>
    public const int FirstArrivalRound = 11;

    /// <summary>
    /// The last championship round of a season a player may arrive in. There is no second window
    /// after it: the season is over, and the next one starts with its own Supercup.
    /// </summary>
    public const int LastArrivalRound = 22;

    /// <summary>
    /// Whether the mid-season window is open in a championship round. It opens with the
    /// eleventh round and closes with the last one, because the season's remaining rounds are
    /// the window's own and a man arriving in round twenty-two has one match to play in a new
    /// shirt, which is a season of contract for a single afternoon.
    /// </summary>
    public static bool IsOpen(int roundNumber) =>
        roundNumber >= FirstArrivalRound && roundNumber <= LastArrivalRound;

    /// <summary>
    /// The season a proposal made in the round given is due to arrive in.
    ///
    /// Before the eleventh round the mid-season window is still ahead of it, so the player
    /// arrives in this season when that round is played. After the eleventh round the window
    /// has already come and gone, so the next thing that happens is the Supercup — which is the
    /// first match of the next season, and the arrival the rules name before any other.
    /// </summary>
    public static int ArrivalSeasonNumberFor(int currentRound, int currentSeasonNumber) =>
        currentRound <= FirstArrivalRound ? currentSeasonNumber : currentSeasonNumber + 1;

    /// <summary>
    /// The round of the arrival season the player walks in on, once that season exists. A
    /// mid-season arrival is the eleventh round of the season it was bought in; an arrival after
    /// the Supercup is the first round of the season that Supercup opened.
    /// </summary>
    public static int ArrivalRoundFor(int currentRound) =>
        currentRound <= FirstArrivalRound ? FirstArrivalRound : 1;
}
