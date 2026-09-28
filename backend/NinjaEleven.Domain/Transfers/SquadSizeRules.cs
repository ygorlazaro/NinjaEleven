namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// The limits on how many players a club may have on its books in a season.
///
/// A squad below the minimum is a club that cannot fill a starting eleven and a bench,
/// which is a match the game cannot simulate. A squad above the maximum is a market that
/// has been bought out, which is a game that has already been won. The bounds are rules
/// of the pyramid, not settings of a screen, so they are asked in the two ways the market
/// actually asks them: may this club take one more man on, and may it let one go.
/// </summary>
public static class SquadSizeRules
{
    /// <summary>The fewest players a club may carry into a season.</summary>
    public const int MinSquadSize = 21;

    /// <summary>The most players a club may carry in a season.</summary>
    public const int MaxSquadSize = 35;

    public static bool IsValid(int size) => size >= MinSquadSize && size <= MaxSquadSize;

    /// <summary>
    /// Whether a club with this many players may take one more on. The check is at the moment
    /// of the arrival rather than at the moment of the proposal, because a squad is filled by
    /// the deals that complete and not by the ones that were made: a club that signed four men
    /// in July and the window opened with thirty-four on its books is a club that cannot sign
    /// a fifth, and a squad over the maximum is a squad whose bench is a reserve team.
    /// </summary>
    public static bool CanAddOne(int currentSize) => currentSize < MaxSquadSize;

    /// <summary>
    /// Whether a club with this many players may let one go. It is the same bound read the
    /// other way, and it is what stops a club being sold down to a team the engine cannot put
    /// eleven men on: a release is a club's decision about its own book, and a book that
    /// cannot field a side is not one it is allowed to shrink any further.
    /// </summary>
    public static bool CanRemoveOne(int currentSize) => currentSize - 1 >= MinSquadSize;
}
