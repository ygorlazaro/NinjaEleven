namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// The rules that decide what a club owes when it releases a player from his contract.
///
/// A release is a decision a club makes about a man on its books, and the money it costs is
/// what the club still owes him: the wages of the season he has left to play and the wages of
/// every season the contract promised him after it. The club does not have to pay the whole
/// of it — a release is a settlement, not a purchase — so the figure is halved, and the half
/// is what leaves the club's book on the day the man is let go.
/// </summary>
public static class ReleaseRules
{
    /// <summary>The share of what the club still owes that a release actually costs.</summary>
    public const decimal ReleaseFraction = 0.5m;

    /// <summary>
    /// What a club owes to release a player, in limos.
    ///
    /// The wage is a hundredth of what the player is worth, and the club owes it for every
    /// round of the season he has left to play plus every season the contract promised after
    /// this one. A contract is a promise about the future and the dates only say when it
    /// started: a three-season deal signed halfway through a season still owes three.
    /// </summary>
    /// <param name="salaryPerRound">What the player costs the club for one round of the season.</param>
    /// <param name="roundsLeftThisSeason">How many rounds of this season the club still has to pay for.</param>
    /// <param name="seasonsLeftAfterThis">How many full seasons the contract promised beyond this one.</param>
    /// <param name="roundsPerSeason">How many rounds a season holds.</param>
    /// <returns>The settlement, rounded to the cent.</returns>
    public static decimal ReleaseCost(
        decimal salaryPerRound,
        int roundsLeftThisSeason,
        int seasonsLeftAfterThis,
        int roundsPerSeason)
    {
        if (salaryPerRound < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(salaryPerRound), "A wage is not a negative number.");
        }

        if (roundsLeftThisSeason < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundsLeftThisSeason), "A season has no negative rounds.");
        }

        if (seasonsLeftAfterThis < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonsLeftAfterThis), "A contract does not owe negative seasons.");
        }

        if (roundsPerSeason <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundsPerSeason), "A season has some rounds in it.");
        }

        var owed = salaryPerRound * roundsLeftThisSeason
                   + salaryPerRound * roundsPerSeason * seasonsLeftAfterThis;

        return decimal.Round(owed * ReleaseFraction, 2, MidpointRounding.AwayFromZero);
    }
}