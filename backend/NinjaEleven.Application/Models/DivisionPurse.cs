namespace NinjaEleven.Application.Models;

/// <summary>
/// One division's purse, and the share of it that goes with each finishing position.
///
/// It is a reading of <c>PrizeRules</c> rather than a table of its own: the weights and the
/// rounding are the domain's, and this only says the same numbers in a shape a screen can carry.
/// </summary>
/// <param name="Tier">Which division, counted from one at the top.</param>
/// <param name="Name">The division's name, in the game's own words.</param>
/// <param name="Purse">What the whole table is paid out of.</param>
/// <param name="Clubs">How many clubs share it.</param>
/// <param name="Shares">One share per position, the champion's first.</param>
public record DivisionPurse(
    int Tier,
    string Name,
    decimal Purse,
    int Clubs,
    IReadOnlyList<PrizeShare> Shares);

/// <summary>What one finishing position in a division is paid.</summary>
/// <param name="Position">Where the club finished, counted from one.</param>
/// <param name="Amount">Its share of the purse.</param>
public record PrizeShare(int Position, decimal Amount);

/// <summary>
/// What the cup pays, in the order a club meets it: the champion's cheque, and the consolation
/// for the round that knocked each other club out.
/// </summary>
/// <param name="TieRound">
/// Which of the five tie-rounds, counted from the round of 16. It is zero for the champion's
/// cheque, which is not a consolation and belongs to no round a club was knocked out in.
/// </param>
/// <param name="Name">The round's name, in the game's own words.</param>
/// <param name="Amount">What it pays.</param>
/// <param name="IsChampion">Whether this is the winner's cheque rather than a consolation.</param>
public record CupPrize(int TieRound, string Name, decimal Amount, bool IsChampion);
