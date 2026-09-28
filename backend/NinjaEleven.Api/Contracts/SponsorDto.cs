using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A sponsor's presence on a shirt, as the sponsor screen reads it — whether as a current
/// deal or as a candidate waiting to be signed.
/// </summary>
public class SponsorOfferDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public decimal PerMatchFee { get; init; }
    public int ContractMatches { get; init; }
    public string Color { get; init; } = "#f2d34f";
}

/// <summary>
/// A club's sponsor book: the deal on the shirt, how many matches of it are still to be
/// played, and the offers waiting for the manager to choose.
/// </summary>
public class SponsorBookDto
{
    public Guid TeamId { get; init; }
    public Guid SeasonId { get; init; }

    /// <summary>The sponsor on the shirt, represented as a full offer on the deal.</summary>
    public SponsorOfferDto? Current { get; init; }

    /// <summary>Matches of the deal still to be played. Zero is the only moment a change is allowed.</summary>
    public int MatchesLeft { get; init; }

    /// <summary>The offers on the table, a shortlist and not a market.</summary>
    public IReadOnlyList<SponsorOfferDto> Candidates { get; init; } = Array.Empty<SponsorOfferDto>();

    /// <summary>The sponsor the club is carrying, so the card can say who it is at a glance.</summary>
    public Guid MasterSponsorId { get; init; }
}

/// <summary>
/// A request to sign a shirt deal for a club.
/// </summary>
public class SponsorSignRequestDto
{
    public Guid SponsorId { get; init; }
    public decimal? PerMatchFee { get; init; }
    public int? ContractMatches { get; init; }
}
