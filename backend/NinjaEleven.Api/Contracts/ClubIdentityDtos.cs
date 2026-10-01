using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A crest as it crosses the wire: the shape, the two club colours, and whatever the club chose
/// to put on it.
/// </summary>
/// <remarks>
/// The enums are the domain's own rather than a second copy of them, and they travel as names
/// because the whole game serialises enums as names. A screen that compared
/// <c>shape === "Shield"</c> against a number would be the one place in the project that had to
/// know how the transport happens to be configured.
/// </remarks>
public class CrestDto
{
    public CrestShape Shape { get; init; }
    public string PrimaryColor { get; init; } = string.Empty;
    public string SecondaryColor { get; init; } = string.Empty;
    public CrestTextDto? Text { get; init; }
    public CrestEmblemDto? Emblem { get; init; }
}

/// <summary>The lettering on a crest: what it says, how high up it sits and what colour it is.</summary>
public class CrestTextDto
{
    public string Content { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public double VerticalPosition { get; init; }
}

/// <summary>The figure on a crest, and where on the shield it stands.</summary>
public class CrestEmblemDto
{
    public CrestFigure Kind { get; init; }
    public string Color { get; init; } = string.Empty;
    public double VerticalPosition { get; init; }
}

/// <summary>One of a club's shirts: two colours, a cut, and an optional third for the number.</summary>
public class KitDto
{
    public string PrimaryColor { get; init; } = string.Empty;
    public string SecondaryColor { get; init; } = string.Empty;
    public KitPattern Pattern { get; init; }
    public string? TrimColor { get; init; }
}

/// <summary>
/// A crest as a manager sends it back from the editor.
///
/// <para>
/// Every field is required rather than optional-with-a-default. The editor is the thing that
/// decides what a crest is, and a request that left a field out would be asking the server to
/// invent a decision the manager did not make — and a server that invented one would store a
/// crest the manager never saw.
/// </para>
/// </summary>
public class UpdateTeamCrestRequestDto
{
    public CrestShape Shape { get; init; }
    public string PrimaryColor { get; init; } = string.Empty;
    public string SecondaryColor { get; init; } = string.Empty;
    public CrestTextDto? Text { get; init; }
    public CrestEmblemDto? Emblem { get; init; }
}

/// <summary>
/// A club's two shirts as a manager sends them back.
///
/// <para>
/// Both arrive together even though a club may only have drawn one, because the second shirt
/// exists for exactly one reason — to be changed into when the first one clashes — and a club
/// that has decided what it will wear when it clashes has decided both.
/// </para>
/// </summary>
public class UpdateTeamKitsRequestDto
{
    public KitDto HomeKit { get; init; } = new();
    public KitDto? AwayKit { get; init; }
}
