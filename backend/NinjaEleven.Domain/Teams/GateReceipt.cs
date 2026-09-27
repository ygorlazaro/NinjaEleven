namespace NinjaEleven.Domain.Teams;

/// <summary>
/// What a match earned at the gate, and who earned it.
///
/// Two thirds of the gate goes to the club that put the game on and one third to the club
/// that came to it. The away club is not here out of kindness: it brought supporters, and a
/// trip is what a supporter costs. What comes out of the split is a number the club's books
/// will one day have to balance, and a two thirds that leaves a third of one limo unaccounted
/// for is a number the books cannot balance, so the two sides are rounded to the cent and the
/// home side takes whatever the away side leaves behind.
/// </summary>
public readonly record struct GateReceipt(
    int Attendance,
    decimal TicketPrice,
    decimal GrossRevenue,
    decimal HomeRevenue,
    decimal AwayRevenue)
{
    /// <summary>The share of the gate the club that hosted the match takes.</summary>
    public const decimal HomeShare = 2m / 3m;

    /// <summary>The share of the gate the club that travelled takes.</summary>
    public const decimal AwayShare = 1m / 3m;

    public static GateReceipt For(int attendance, decimal ticketPrice)
    {
        if (attendance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attendance), attendance, "Nobody turned up negative.");
        }

        if (ticketPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticketPrice), ticketPrice, "A ticket cannot cost less than nothing.");
        }

        var gross = Math.Round(attendance * ticketPrice, 2, MidpointRounding.AwayFromZero);
        var away = Math.Round(gross * AwayShare, 2, MidpointRounding.AwayFromZero);

        // The home share is whatever is left, so gross is always home plus away and a club's
        // books add up even when a third of a limo does not divide.
        return new GateReceipt(attendance, ticketPrice, gross, gross - away, away);
    }
}
