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

    /// <summary>
    /// How many days of the season the treasurer's statement covers.
    ///
    /// <para>
    /// A week is the cadence a manager actually reads at, and it is chosen rather than tuned:
    /// a statement per matchday is a copy of the ledger, and a statement per month is a
    /// surprise. Seven days is also the shortest span in which the four things a manager
    /// spends money on — the gate, the wages, the market and the training ground — have all
    /// happened at least once, which is what makes a weekly number mean something.
    /// </para>
    /// </summary>
    public const int StatementDays = 7;

    /// <summary>
    /// Whether the treasurer's books are closed on this day of the season.
    ///
    /// <para>
    /// It is asked of the day rather than of the calendar, so the statement is written when
    /// the world reaches a week rather than when a process happens to be looking. The last
    /// championship day closes the books whatever its number is: a division that ends on day
    /// thirty-one would otherwise leave three days of gate receipts and wages belonging to no
    /// statement at all, and the week the manager reads would not be the week he played.
    /// </para>
    /// </summary>
    public static bool ClosesTheBooksOn(int matchDayNumber) =>
        matchDayNumber % StatementDays == 0 || IsTheLastChampionshipDay(matchDayNumber);

    /// <summary>Whether this day is the last one the championship is played on.</summary>
    public static bool IsTheLastChampionshipDay(int matchDayNumber) =>
        matchDayNumber == CompetitionRules.LeagueMatchDays + CompetitionRules.FirstChampionshipMatchDay - 1;

    /// <summary>The first day a statement ending on this one covers.</summary>
    public static int FirstDayOfTheStatementEndingOn(int matchDayNumber) =>
        Math.Max(1, matchDayNumber - (StatementDays - 1));

    /// <summary>The most movements one page may hold, so a caller cannot ask for the whole book.</summary>
    public const int MaxPageSize = 100;
}
