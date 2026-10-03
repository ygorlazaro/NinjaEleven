using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A sponsor's presence on a shirt, as the sponsor screen reads it — whether as a current
/// deal or as a candidate waiting to be signed.
/// </summary>
/// <summary>
/// A company's mark, as a screen draws it.
///
/// <para>
/// The design crosses the wire as a shape and two colours rather than as a picture, because the
/// drawing is the client's and the decision is the world's: a panel, a brand colour, a legible
/// ink and the words. Sending a bitmap would make every logo a thing the game cannot restyle,
/// compare or recolour, and a sponsor's colour is half of what identifies it.
/// </para>
/// </summary>
public class SponsorLogoDto
{
    /// <summary>The panel the name is written on.</summary>
    public SponsorLogoShape Shape { get; init; }

    /// <summary>The panel itself, in the company's brand colour.</summary>
    public string BackgroundColor { get; init; } = "#f2d34f";

    /// <summary>The lettering, black or white against the panel so that it can be read.</summary>
    public string InkColor { get; init; } = "#111111";

    /// <summary>The words on the mark: the company's own name.</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// A sponsor as a shirt carries it: who it is, what colour it is, and the mark it is drawn as.
///
/// <para>
/// It is a separate shape from <see cref="SponsorOfferDto"/> on purpose. An offer is a price and a
/// length, and it exists only while a manager is choosing; this is a company that already has a
/// shirt, and it exists on a scoreboard. One DTO for both would put a fee on a scoreboard.
/// </para>
/// </summary>
public class SponsorMarkDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;

    /// <summary>The brand colour, kept beside the mark so a screen can colour its own ink with it.</summary>
    public string Color { get; init; } = "#f2d34f";

    public SponsorLogoDto Logo { get; init; } = new();
}

public class SponsorOfferDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public decimal PerMatchFee { get; init; }
    public int ContractMatches { get; init; }

    /// <summary>The mark's own colour, so a sponsor is a thing the screen can draw.</summary>
    public string Color { get; init; } = "#f2d34f";

    /// <summary>
    /// The mark itself, so the sponsor screen draws the company the way the shirt does. A list of
    /// offers that showed each name in the same text and no colour at all would be a shortlist a
    /// manager has to read, rather than one he recognises.
    /// </summary>
    public SponsorLogoDto Logo { get; init; } = new();

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
