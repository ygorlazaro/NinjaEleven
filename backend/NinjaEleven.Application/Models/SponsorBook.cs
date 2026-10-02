using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A single shirt deal, as the sponsor screen reads it.
/// </summary>
public class SponsorOffer
{
    public Guid SponsorId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public decimal PerMatchFee { get; init; }
    public int ContractMatches { get; init; }
    public string Color { get; init; } = "#f2d34f";

    /// <summary>How big a company this is: 1 local, 2 regional, 3 national.</summary>
    public int Weight { get; init; }

    /// <summary>How many clubs this sponsor already has on its shirt.</summary>
    public int ClubsSponsored { get; init; }

    /// <summary>How many it is willing to have. A full slate pays less for the next one.</summary>
    public int MaxClubs { get; init; }
}

/// <summary>
/// The club's shirt state: the deal it is playing under, and the list of sponsors hoping for it.
/// </summary>
public class SponsorBook
{
    public Guid TeamId { get; init; }
    public Guid SeasonId { get; init; }

    /// <summary>The deal the club is currently playing under, or null when it is free.</summary>
    public SponsorContractSlim? Current { get; init; }

    /// <summary>
    /// Sponsors who are willing to take the shirt: a club without a deal gets candidates, and
    /// a club with a deal that is about to end gets fresh ones to renew with.
    /// </summary>
    public IReadOnlyList<SponsorOffer> Candidates { get; init; } = Array.Empty<SponsorOffer>();
}

/// <summary>
/// A contract, without the sponsor detail — the list on the screen does not need it.
/// </summary>
public class SponsorContractSlim
{
    public Guid ContractId { get; init; }
    public Guid SponsorId { get; init; }
    public string SponsorName { get; init; } = string.Empty;
    public string SponsorIndustry { get; init; } = string.Empty;
    public string SponsorColor { get; init; } = "#f2d34f";
    public decimal PerMatchFee { get; init; }
    public int ContractMatches { get; init; }
    public int MatchesPlayed { get; init; }
    public int MatchesLeft => Math.Max(0, ContractMatches - MatchesPlayed);
}
