using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// The panels a company's mark may be written on.
///
/// <para>
/// They are not the ten shields a club may be given, and they are deliberately not: a crest is
/// a badge a club wears and a sponsor's mark is a wordmark on somebody else's shirt, and a
/// scoreboard where both sides are drawn from one shape list is a scoreboard where the sponsor
/// of a club looks like a division below it. The vocabulary is the vocabulary of packaging —
/// a strip, a block, a roundel, a plate — because that is what a company's name is printed on.
/// </para>
/// </summary>
public enum SponsorLogoShape
{
    /// <summary>The strip: a long low rectangle. The commonest wordmark there is.</summary>
    Banner = 0,

    /// <summary>A block: a rectangle with the corners taken off, carrying more of the name.</summary>
    Block = 1,

    /// <summary>A roundel: a circle with a band across the middle for the name to sit in.</summary>
    Roundel = 2,

    /// <summary>Six straight sides, the way a hexagonal badge is cut.</summary>
    Hexagon = 3,

    /// <summary>A plain disc, with the name set straight across it.</summary>
    Disc = 4,

    /// <summary>A plate: a wide rectangle with clipped corners and a flat top.</summary>
    Plaque = 5,

    /// <summary>A lozenge: a rhombus wider than it is tall.</summary>
    Lozenge = 6,

    /// <summary>A pennant strip: a bar with a swallow-tail cut out of the right-hand end.</summary>
    Pennant = 7
}

/// <summary>
/// A company's mark: the panel it is written on, the colour it is written in, and the words.
///
/// <para>
/// A sponsor's logo is worked out of its name rather than drawn and stored, and the reason is
/// the same one a club's size is dealt out of its name: the mark has to be the same every time
/// anybody looks at it. A stored mark that a seeder wrote would have to be migrated, and a
/// stored mark a manager could change would be a mark that is a different company's by the time
/// the game was next started. The hash is FNV-1a, which is stable across processes and
/// machines — a different hash would put a different mark on a sponsor's shirt every time the
/// API was restarted, which is the one thing a shirt cannot do.
/// </para>
///
/// <para>
/// The words are the company's own name, because a sponsor's mark is a wordmark far more often
/// than it is a device, and a mark of nothing but a coloured shape is the initials placeholder
/// with extra steps.
/// </para>
/// </summary>
public sealed class SponsorLogoDesign
{
    /// <summary>How much of a company's name its own mark may carry.</summary>
    /// <remarks>
    /// Long enough for "Cia. Energética Paulista" to be recognisable as itself and short enough
    /// to be set inside a narrow panel at the size a sponsor is drawn on a scoreboard, which is
    /// small. The name travels whole on the sponsor's own page and truncated on the shirt, which
    /// is what every wordmark in football does.
    /// </remarks>
    public const int MaxLength = 22;

    public SponsorLogoShape Shape { get; }

    /// <summary>The panel, in the company's own brand colour.</summary>
    public string BackgroundColor { get; }

    /// <summary>The lettering, picked black or white against the panel so that it can be read.</summary>
    public string InkColor { get; }

    public string Text { get; }

    public SponsorLogoDesign(SponsorLogoShape shape, string backgroundColor, string inkColor, string text)
    {
        if (!Enum.IsDefined(shape))
        {
            throw new ArgumentException($"'{shape}' is not one of the eight panels.", nameof(shape));
        }

        Shape = shape;
        BackgroundColor = ClubColours.Normalise(backgroundColor);
        InkColor = ClubColours.Normalise(inkColor);

        var trimmed = (text ?? string.Empty).Trim();
        Text = trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }
}

/// <summary>
/// The mark every sponsor in the catalog is given, worked out of its name.
/// </summary>
public static class SponsorLogoDefaults
{
    private static readonly SponsorLogoShape[] Shapes = Enum.GetValues<SponsorLogoShape>();

    /// <summary>
    /// The logo this sponsor wears, and the same one every time anybody asks.
    /// </summary>
    public static SponsorLogoDesign LogoFor(Sponsor sponsor) => LogoFor(sponsor.Name, sponsor.Color);

    /// <summary>
    /// The logo a name and a brand colour are given.
    ///
    /// <para>
    /// The panel is dealt out of the hash and the words are the company's own. The lettering
    /// colour is not chosen by the sponsor: a dark brand printed in its own hue is a mark that
    /// cannot be read, and a sponsor that has not been given a second colour is not a company
    /// that cannot be drawn. Black or white against the panel is the whole of the decision.
    /// </para>
    /// </summary>
    public static SponsorLogoDesign LogoFor(string name, string color)
    {
        var digest = StableHash.Of(name);
        var background = ClubColours.NormaliseOr(color, "#f2d34f");

        return new SponsorLogoDesign(
            Shapes[digest % Shapes.Length],
            background,
            ClubColours.InkOn(background),
            name);
    }
}