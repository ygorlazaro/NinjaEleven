namespace NinjaEleven.Domain.Teams;

/// <summary>
/// The terms a club and a player can agree on, kept apart from the contract itself because
/// these are the numbers a rule is made of and a contract is a thing those numbers are
/// written on.
/// </summary>
public static class ContractRules
{
    /// <summary>
    /// The shortest renewal a manager can sign.
    ///
    /// One, and not zero: a renewal for no seasons is not a renewal, it is a decision to let
    /// the contract run out, and a man whose deal is up should leave rather than stay in a
    /// squad the club has stopped planning around.
    /// </summary>
    public const int FewestRenewableSeasons = 1;

    /// <summary>
    /// The longest renewal a manager can sign.
    ///
    /// Five, and the number is a budget and not a rule about men: a wage is fixed for the run
    /// of a contract, so five seasons is five seasons of a wage the club cannot renegotiate,
    /// and a board that commits to that is making a plan rather than a bet. The default deal
    /// is three, so a renewal may shorten or lengthen it but cannot lock a club away from its
    /// own squad for a decade.
    /// </summary>
    public const int MostRenewableSeasons = 5;

    /// <summary>Whether a manager may renew for this many seasons.</summary>
    public static bool IsARenewableLength(int seasons) =>
        seasons is >= FewestRenewableSeasons and <= MostRenewableSeasons;
}
