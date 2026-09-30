namespace NinjaEleven.Domain.Finance;

/// <summary>
/// What a movement of money is, because a ledger of "amounts" is a list a manager has to
/// decode: the mark and the words beside a number are what turn a number into a reason.
///
/// Two of these are not movements of money at all. <see cref="Seed"/> is the capital a club
/// is founded on and <see cref="CarryOver"/> is the balance it was handed by the season
/// before. Both state a balance rather than moving one, and both are excluded from a season's
/// income and expenses: counting them would invent revenue the club did not earn and pay it
/// for wages it did not owe.
/// </summary>
public enum FinanceMovementKind
{
    /// <summary>The capital a club is given when the world is drawn. It is not income.</summary>
    Seed,

    /// <summary>
    /// The balance a club closed a season with, opening the next one with it. It is not
    /// income: the money was already the club's, and a ledger that counted it as revenue
    /// would show a fortune the club never had.
    /// </summary>
    CarryOver,

    /// <summary>The gate of a match, split between the club that hosted it and the one that came to it.</summary>
    GateRevenue,

    /// <summary>What the club owes its squad for the season.</summary>
    Wages,

    /// <summary>What a club receives for a player it sells.</summary>
    TransferIn,

    /// <summary>What a club pays for a player it signs.</summary>
    TransferOut,

    /// <summary>
    /// What the club pays the coaching staff for a session of training.
    ///
    /// <para>
    /// Its own kind rather than folded into <see cref="Infrastructure"/>, because a club that
    /// has spent a season's worth of its development budget and cannot say so from its own
    /// statement has been told nothing. A manager deciding whether the points his striker
    /// gained in August are worth what they cost in October needs the training out of the
    /// ledger on its own line, and a line that shared a kind with the buses would put them in
    /// the same pot as the pitch and leave him to divide one by the other.
    /// </para>
    /// </summary>
    Training,

    /// <summary>Money from a sponsor.</summary>
    Sponsorship,

    /// <summary>Money from selling the right to broadcast the club's matches.</summary>
    TvRights,

    /// <summary>What a competition pays for a result.</summary>
    PrizeMoney,

    /// <summary>The ground, the training ground, the buses, the insurance.</summary>
    Infrastructure,

    /// <summary>What a club is charged by a competition for something it did.</summary>
    Fine,

    /// <summary>The club shop.</summary>
    Merchandising
}
