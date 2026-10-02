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

    /// <summary>How big a company this is: 1 local, 2 regional, 3 national.</summary>
    public int Weight { get; init; }

    /// <summary>How many clubs it already has on its shirt.</summary>
    public int ClubsSponsored { get; init; }

    /// <summary>How many it is willing to have. A full slate pays less for the next one.</summary>
    public int MaxClubs { get; init; }
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
///
/// <para>
/// It carries the company and nothing else. The price and the length were fields here once,
/// and a field a client can fill in is a number the backend did not decide: the manager
/// budgeted his season against a fee the book had never agreed to pay, and a length nobody
/// had quoted. Both are the sponsor's to decide now, and they are on the offer the screen
/// was already showing.
/// </para>
/// </summary>
public class SponsorSignRequestDto
{
    public Guid SponsorId { get; init; }
}
