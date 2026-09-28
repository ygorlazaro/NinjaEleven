namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// The constants of a shirt deal. Centralised so the sponsor system is tuned in one place
/// rather than scattered across action tables.
/// </summary>
public static class SponsorRules
{
    /// <summary>The minimum number of sponsor candidates offered to a club without a deal.</summary>
    public const int MinCandidateOffers = 3;

    /// <summary>The maximum number of sponsor candidates offered to a club without a deal.</summary>
    public const int MaxCandidateOffers = 5;

    /// <summary>The upper bound on the per-match fee a sponsor will offer, in limos.</summary>
    public const decimal MaxPerMatchFee = 500_000m;

    /// <summary>The lower bound on the per-match fee a sponsor will offer, in limos.</summary>
    public const decimal MinPerMatchFee = 10_000m;

    /// <summary>
    /// The length band of a sponsor's first offer: between this many and twice this many
    /// matches.
    /// </summary>
    public const int BaseOfferLength = 5;

    /// <summary>
    /// The maximum number of sponsor candidates kept on a club's list at once. When a deal
    /// is signed the list is cleared; when it is paid off a new list is drawn.
    /// </summary>
    public const int MaxCandidatesHeld = 7;
}
