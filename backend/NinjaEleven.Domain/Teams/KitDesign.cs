using System.Text.Json;
using System.Text.Json.Serialization;

namespace NinjaEleven.Domain.Teams;

/// <summary>
/// The cuts a shirt may be made in. The colour is any club's and the cut is not: a plain
/// block, a set of stripes, a band down the chest, a sash, a split down the middle and a
/// chequer are shirts any club could have turned out in, and a kit drawn in one cut for every
/// club in the country is a swatch rather than a shirt.
/// </summary>
public enum KitPattern
{
    /// <summary>The whole body in the club's first colour.</summary>
    Solid = 0,

    /// <summary>A plain body with the sleeves cut in the second colour, which is what a club
    /// whose two colours do not work as a pattern turns out in.</summary>
    SolidSeparateSleeves = 1,

    /// <summary>Wide vertical stripes.</summary>
    VerticalStripe = 2,

    /// <summary>Horizontal bands.</summary>
    HorizontalStripe = 3,

    /// <summary>Narrow vertical stripes, several of them.</summary>
    ThinStripes = 4,

    /// <summary>One band across the chest, from shoulder to hip.</summary>
    Sash = 5,

    /// <summary>Two halves down the middle.</summary>
    Halves = 6,

    /// <summary>A chequer, in squares.</summary>
    Checkered = 7
}

/// <summary>
/// Which of a club's two shirts a team is playing in.
/// </summary>
/// <remarks>
/// It is a fact about a match rather than about a club, and it is stamped on the match rather
/// than worked out whenever a screen asks: a manager who reloads his own match in the middle
/// of the second half has to be looking at the same two shirts he was looking at before.
/// </remarks>
public enum KitSide
{
    Home = 0,
    Away = 1
}

/// <summary>
/// One of a club's shirts: two colours, a cut, and a third colour of the club's own choosing
/// for the collar and the number.
/// </summary>
/// <remarks>
/// The third colour is optional and it is not decoration. A club that is dark blue on dark
/// blue has a number nobody can read, and the number is how a player is identified from the
/// other side of a stadium. The colour is left out when the two colours already work, so a club
/// whose kit is legible has nothing to set.
/// </remarks>
public sealed class KitDesign
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public KitPattern Pattern { get; }

    /// <summary>The collar and the number, or empty when the club has not chosen one.</summary>
    public string? TrimColor { get; }

    public KitDesign(
        string primaryColor,
        string secondaryColor,
        KitPattern pattern,
        string? trimColor = null)
    {
        if (!Enum.IsDefined(pattern))
        {
            throw new ArgumentException($"'{pattern}' is not one of the cuts.", nameof(pattern));
        }

        PrimaryColor = ClubColours.Normalise(primaryColor);
        SecondaryColor = ClubColours.Normalise(secondaryColor);
        Pattern = pattern;

        // An empty string is a club that has not chosen, and null is the same answer written
        // the other way round; only a colour that was actually offered and is broken is a
        // refusal.
        TrimColor = string.IsNullOrWhiteSpace(trimColor)
            ? null
            : ClubColours.Normalise(trimColor);
    }

    /// <summary>The colour the number is read in, worked out when the club chose none.</summary>
    public string NumberColor => TrimColor ?? ClubColours.InkOn(PrimaryColor);

    public string ToJson() => JsonSerializer.Serialize(
        new KitDesignRecord(PrimaryColor, SecondaryColor, Pattern, TrimColor),
        Options);

    /// <summary>
    /// Reads a shirt back, or answers that there is none. Never throws: a column edited by hand
    /// leaves the club wearing its two colours, which is what it wore before kits existed.
    /// </summary>
    public static KitDesign? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<KitDesignRecord>(json, Options);

            if (record is null)
            {
                return null;
            }

            return new KitDesign(
                record.PrimaryColor,
                record.SecondaryColor,
                Enum.IsDefined(record.Pattern) ? record.Pattern : KitPattern.Solid,
                record.TrimColor);
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

    private sealed record KitDesignRecord(
        string PrimaryColor,
        string SecondaryColor,
        KitPattern Pattern,
        string? TrimColor);
}
