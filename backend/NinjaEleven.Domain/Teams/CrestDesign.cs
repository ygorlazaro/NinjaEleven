using System.Text.Json;
using System.Text.Json.Serialization;

namespace NinjaEleven.Domain.Teams;

/// <summary>
/// The ten shields a club may be given. The shape is the one thing about a crest that is not a
/// colour, and it is the one thing that makes a club's badge recognisable from across a stand
/// at a size where the lettering is not.
/// </summary>
public enum CrestShape
{
    /// <summary>A circle. The oldest badge there is and the simplest to read small.</summary>
    Round = 0,

    /// <summary>An oval, taller than it is wide.</summary>
    Oval = 1,

    /// <summary>The conventional shield: square shoulders, sides in, a point at the bottom.</summary>
    Shield = 2,

    /// <summary>A shield with two lugs cut out of its shoulders, the way a club that wants to
    /// look like a piece of armour does.</summary>
    EaredShield = 3,

    /// <summary>Six straight sides.</summary>
    Hexagon = 4,

    /// <summary>A square with the corners taken off in one curve.</summary>
    Squircle = 5,

    /// <summary>A long pennant: a straight top and a deep point.</summary>
    Pennant = 6,

    /// <summary>A rectangle with a notch cut out of the bottom edge.</summary>
    Banner = 7,

    /// <summary>A rhombus, wider than it is tall.</summary>
    Diamond = 8,

    /// <summary>A five-pointed star, used as a badge rather than as a figure.</summary>
    Star = 9
}

/// <summary>
/// The figures a crest may carry. They are figures rather than letters because a badge with a
/// picture on it survives being shrunk into a table cell and one with a word on it does not.
/// </summary>
public enum CrestFigure
{
    None = 0,
    Ball = 1,
    Star = 2,
    Flame = 3,
    Bolt = 4,
    Crown = 5,
    Wave = 6,
    Sword = 7,
    Anchor = 8
}

/// <summary>
/// The lettering on a crest, where it sits and what colour it is written in.
/// </summary>
/// <remarks>
/// The colour is the manager's third colour and is deliberately not one of the club's two: a
/// club that is red and black writing its name in white is the ordinary case, and a lettering
/// colour picked from the club's own pair would make that club impossible to draw.
/// </remarks>
public sealed class CrestText
{
    /// <summary>How much of a club's short name a crest may carry.</summary>
    public const int MaxLength = 12;

    public string Content { get; }
    public string Color { get; }

    /// <summary>
    /// How far down the shield's field the lettering sits, from 0 at the top of the field to
    /// 1 at the bottom. A number rather than a pixel because the crest is drawn at thirty
    /// pixels in a table and at ninety on the club's own page, and the same fraction has to
    /// mean the same place in both.
    /// </summary>
    public double VerticalPosition { get; }

    public CrestText(string content, string color, double verticalPosition)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("A crest's lettering needs something to say.", nameof(content));
        }

        Content = content.Trim();
        Color = ClubColours.Normalise(color);
        VerticalPosition = Clamp(verticalPosition);

        if (Content.Length > MaxLength)
        {
            Content = Content[..MaxLength];
        }
    }

    /// <summary>
    /// How near the top of the field the lettering may sit.
    ///
    /// The position is the middle of the letters rather than their first line, so a crest
    /// dragged all the way to the top would hang half its name outside the shield. The ends of
    /// the range are where a line of lettering still fits inside every one of the ten shapes —
    /// the narrow ones at the top being the round and the oval.
    /// </summary>
    public const double LowestPosition = 0.08;

    /// <summary>How near the bottom of the field the lettering may sit.</summary>
    public const double HighestPosition = 0.92;

    private static double Clamp(double value) =>
        Math.Clamp(double.IsNaN(value) ? 0.5 : value, LowestPosition, HighestPosition);
}

/// <summary>
/// The figure on a crest, where it sits and what colour it is drawn in. The same third colour
/// as the lettering, and for the same reason: it is a choice that belongs to the club and is not
/// one of the two colours the rest of the identity is made of.
/// </summary>
public sealed class CrestEmblem
{
    public CrestFigure Kind { get; }
    public string Color { get; }
    public double VerticalPosition { get; }

    public CrestEmblem(CrestFigure kind, string color, double verticalPosition)
    {
        if (kind is CrestFigure.None)
        {
            throw new ArgumentException("An emblem has to be one of the figures.", nameof(kind));
        }

        Kind = kind;
        Color = ClubColours.Normalise(color);
        VerticalPosition = Math.Clamp(
            double.IsNaN(verticalPosition) ? 0.5 : verticalPosition,
            LowestPosition,
            HighestPosition);
    }

    /// <summary>
    /// How near the top of the field a figure may stand. It is further from the ends than the
    /// lettering's is because a figure is tall as well as wide, and one dragged to the very top
    /// of a shield hangs out of the top of it.
    /// </summary>
    public const double LowestPosition = 0.16;

    /// <summary>How near the bottom of the field a figure may stand.</summary>
    public const double HighestPosition = 0.84;
}

/// <summary>
/// A club's crest, drawn rather than described.
///
/// <para>
/// The two colours have one job each and they are not interchangeable: the primary is
/// everything in the foreground — the border and the lettering — and the secondary is the field
/// the elements sit on. A crest whose field is its own secondary and whose border is its own
/// primary is the same club as one whose field is the primary, read upside down, and the two
/// are kept apart because the club's two colours are a pair and a pair has an order.
/// </para>
///
/// <para>
/// Both elements are optional and both may be present at once. A crest with a figure and no
/// lettering and a crest with lettering and no figure are both crests; a crest with neither is
/// a blank shape, which is why the editor never offers one.
/// </para>
/// </summary>
public sealed class CrestDesign
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public CrestShape Shape { get; }
    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public CrestText? Text { get; }
    public CrestEmblem? Emblem { get; }

    public CrestDesign(
        CrestShape shape,
        string primaryColor,
        string secondaryColor,
        CrestText? text = null,
        CrestEmblem? emblem = null)
    {
        if (!Enum.IsDefined(shape))
        {
            throw new ArgumentException($"'{shape}' is not one of the ten shields.", nameof(shape));
        }

        Shape = shape;
        PrimaryColor = ClubColours.Normalise(primaryColor);
        SecondaryColor = ClubColours.Normalise(secondaryColor);
        Text = text;
        Emblem = emblem;
    }

    /// <summary>Whether this crest says anything at all, or is a coloured shape.</summary>
    public bool HasElement => Text is not null || Emblem is not null;

    /// <summary>
    /// A crest with neither element has nothing a manager could have chosen beyond two colours
    /// and a shape, and a club is not a swatch. So this is the door a caller goes through
    /// rather than a check it has to remember to make.
    /// </summary>
    public CrestDesign WithElement(CrestText? text = null, CrestEmblem? emblem = null)
    {
        if (text is null && emblem is null)
        {
            throw new ArgumentException(
                "A crest needs a letter or a figure: an empty shield is not a club's badge.",
                nameof(text));
        }

        return new CrestDesign(Shape, PrimaryColor, SecondaryColor, text ?? Text, emblem ?? Emblem);
    }

    public string ToJson() => JsonSerializer.Serialize(
        new CrestDesignRecord(Shape, PrimaryColor, SecondaryColor, Text, Emblem),
        Options);

    /// <summary>
    /// Reads a crest back, or answers that there is none.
    ///
    /// <para>
    /// Never throws. A column that has been edited by hand, or written by an older version of
    /// the game, must not be able to take a screen down: the screen that asks for a crest
    /// answers with the initials placeholder, which is what it answered before the crest
    /// existed, and the club keeps its name.
    /// </para>
    /// </summary>
    public static CrestDesign? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<CrestDesignRecord>(json, Options);

            if (record is null)
            {
                return null;
            }

            return new CrestDesign(
                Enum.IsDefined(record.Shape) ? record.Shape : CrestShape.Shield,
                record.PrimaryColor,
                record.SecondaryColor,
                record.Text,
                record.Emblem);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The shape as it crosses the wire. It exists so a property bag can be deserialised
    /// without the domain constructors being asked to validate a payload that arrived from
    /// anywhere.
    /// </summary>
    private sealed record CrestDesignRecord(
        CrestShape Shape,
        string PrimaryColor,
        string SecondaryColor,
        CrestText? Text,
        CrestEmblem? Emblem);
}
