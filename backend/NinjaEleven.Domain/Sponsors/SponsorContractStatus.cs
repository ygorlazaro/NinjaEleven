namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// The state of a sponsor contract: the deal is live, it has run out, or it has been
/// cancelled mid-flight by the club walking away from it.
/// </summary>
public enum SponsorContractStatus
{
    /// <summary>The deal is on the shirt and the sponsor is being paid per match.</summary>
    Active,

    /// <summary>The contract has been paid for all of its matches and the club is free to sign another.</summary>
    Expired,

    /// <summary>Manually ended before expiry — reserved for future use (e.g. termination fees).</summary>
    Terminated
}
