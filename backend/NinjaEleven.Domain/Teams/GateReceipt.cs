using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Teams;

/// <summary>
/// How a gate is divided between the two clubs of a match.
/// </summary>
/// <remarks>
/// A championship game is not a cup tie, and the money says so. The club that puts the game
/// on takes two thirds and the club that travels to it takes a third — not out of kindness, but
/// because a trip is what a supporter costs and a third of a gate is what a visiting
/// support pays for its coach. In a cup tie the gate is halved instead, because a knockout is
/// two clubs meeting once on neutral ground as often as not: there is no home season behind
/// the ticket, so the club that happens to be drawn at home has not earned two thirds of
/// anything and taking it would hand a trophy's biggest night to whoever the draw favoured.
/// </remarks>
public readonly record struct GateSplit(decimal HomeShare, decimal AwayShare)
{
    /// <summary>The share the club that hosted the match takes in a division game.</summary>
    public const decimal ChampionshipHomeShare = 2m / 3m;

    /// <summary>The share the club that travelled takes in a division game.</summary>
    public const decimal ChampionshipAwayShare = 1m / 3m;

    /// <summary>
    /// The split for a kind of competition: two thirds and a third for the championship,
    /// half each for anything that is a knockout.
    /// </summary>
    public static GateSplit For(CompetitionType competitionType) =>
        competitionType == CompetitionType.League
            ? new GateSplit(ChampionshipHomeShare, ChampionshipAwayShare)
            : new GateSplit(0.5m, 0.5m);
}

/// <summary>
/// What a match earned at the gate, and who earned it.
///
/// What comes out of the split is a number the club's books have to balance, and a two thirds
/// that leaves a third of one limo unaccounted for is a number the books cannot balance, so the
/// two sides are rounded to the cent and the home side takes whatever the away side leaves
/// behind.
/// </summary>
public readonly record struct GateReceipt(
    int Attendance,
    decimal TicketPrice,
    decimal GrossRevenue,
    decimal HomeRevenue,
    decimal AwayRevenue)
{
    public static GateReceipt For(int attendance, decimal ticketPrice, GateSplit split)
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
        var away = Math.Round(gross * split.AwayShare, 2, MidpointRounding.AwayFromZero);

        // The home share is whatever is left, so gross is always home plus away and a club's
        // books add up even when a share does not divide to the cent.
        return new GateReceipt(attendance, ticketPrice, gross, gross - away, away);
    }
}
