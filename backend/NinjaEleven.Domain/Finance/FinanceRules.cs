using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Domain.Finance;

/// <summary>
/// The money of the game, and the rules that balance it.
/// </summary>
public static class FinanceRules
{
    /// <summary>
    /// What every club is given when a world is drawn.
    ///
    /// It is a million limos for a club with a five thousand seat ground and a squad of
    /// twenty-three, and the number is small on purpose: it is roughly what a club earns in
    /// three matchdays at the gate. A starting balance that covered twenty seasons would make
    /// every decision about money a decision with no consequence, and the whole point of a
    /// club having a bank balance is that a manager can lose it.
    /// </summary>
    public const decimal StartingBalance = 1_000_000m;

    /// <summary>
    /// How many seasons a contract runs for when nobody says otherwise.
    ///
    /// Three is a long contract in football and a short one in a game: it is long enough that
    /// a squad is not rebuilt every year, and short enough that a club which has spent its
    /// money has to start again before the player who is worth a fortune has left.
    /// </summary>
    public const int DefaultContractSeasons = 3;

    /// <summary>
    /// How many matches of the championship a club plays in a season, and therefore how many
    /// times its wage bill is settled.
    ///
    /// The bill is a season's wages, and a season's wages are paid after every match of the
    /// league — so a club pays the same money in total either way, and a book that carried one
    /// line a season would tell a manager nothing about which matchdays cost what. A club pays
    /// nothing for a cup tie: the cup is a knockout the club entered for the prize, and a
    /// wage bill for a match that decides a tie is a bill for a match the league did not ask
    /// the club to play in.
    /// </summary>
    public static int WageMatchDays => Competitions.CompetitionRules.LeagueMatchDays;

    /// <summary>
    /// The number of movements a page of a ledger holds when the caller does not say.
    /// Ten is a screenful: long enough to be a page, short enough that the last line of it is
    /// not far below the fold.
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>The most movements one page may hold, so a caller cannot ask for the whole book.</summary>
    public const int MaxPageSize = 100;
}
