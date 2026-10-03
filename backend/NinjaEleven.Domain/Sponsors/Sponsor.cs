namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// A company whose name goes on a shirt. The sponsor is permanent master data: the same
/// name, industry and colour every season, so a club that signs a deal with a sponsor
/// this year can show the same mark next year in a contract that expired and was renewed.
/// </summary>
public class Sponsor
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Industry { get; private set; } = string.Empty;

    /// <summary>The sponsor's brand colour, so a shirt can carry the mark in the mark's own hue.</summary>
    public string Color { get; private set; } = "#f2d34f";

    /// <summary>
    /// The mark this company wears, worked out of its own name rather than stored beside it.
    ///
    /// <para>
    /// It is a computed answer rather than a column for the same reason a club's crest is
    /// allowed to be one or the other on purpose: the mark has to be the same mark every time
    /// anybody looks at it, and a value derived from the name always is — there is nothing to
    /// migrate and nothing that can drift. The shape, the brand colour, the lettering colour
    /// and the words are all of it.
    /// </para>
    /// </summary>
    public SponsorLogoDesign Logo => SponsorLogoDefaults.LogoFor(this);

    /// <summary>
    /// How big a company this is, 1 to 3, and the size of its cheque.
    ///
    /// <para>
    /// A local shop that sponsors a fourth-division club and a national oil company that
    /// sponsors eight first-division clubs do not pay the same for the same slot on a shirt,
    /// and a catalog without this number is a catalog of thirty identical offers. It is data
    /// rather than a formula because a company is what it is: nobody works a brand's size out
    /// from its industry at runtime.
    /// </para>
    /// </summary>
    public int Weight { get; private set; } = 2;

    /// <summary>
    /// How many clubs this sponsor has on its shirt at the same time.
    ///
    /// <para>
    /// It is the one number a manager should look at before signing, because it moves the
    /// price: a sponsor filling its slate pays more per club than one that is full and taking
    /// the next one to keep its name around. It is also the reason a good club has to choose —
    /// the brands that want it are the ones that can afford to spread themselves thin.
    /// </para>
    /// </summary>
    public int MaxClubs { get; private set; } = 4;

    /// <summary>
    /// How good a club has to be before this sponsor will put its name on it.
    ///
    /// <para>
    /// Read in the same units as <see cref="Sponsors.SponsorPricing.Appeal"/>: 1.00 is an
    /// average club doing average things, above 1.00 is a club with something to show. A
    /// national brand that took anyone would be a national brand whose shirt is on the club
    /// that finished last, and the manager looking at that shirt would be right to think less
    /// of it.
    /// </para>
    /// </summary>
    public double MinAppeal { get; private set; }

    /// <summary>
    /// The best division this sponsor will approach. Tier 1 is the top of the pyramid.
    /// </summary>
    public int MaxTier { get; private set; } = 3;

    private Sponsor() { }

    /// <param name="size">
    /// The whole of what a company of this size is: how much it pays, how many shirts it keeps,
    /// which divisions it works in and how good a club it insists on. It is one argument
    /// because it is one decision — asking for the four separately is asking four questions
    /// whose answers have to agree, and a sponsor seeded as national with a local club's
    /// appetite is a company the rest of the game cannot reason about.
    /// </param>
    public static Sponsor Create(
        string name,
        string industry,
        string color,
        SponsorSize? size = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A sponsor needs a name.", nameof(name));
        if (string.IsNullOrWhiteSpace(industry))
            throw new ArgumentException("A sponsor has an industry.", nameof(industry));

        var company = size ?? SponsorSize.Regional;

        return new Sponsor
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Industry = industry.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? "#f2d34f" : color.Trim(),
            Weight = company.Weight,
            MaxClubs = company.MaxClubs,
            MinAppeal = company.MinAppeal,
            MaxTier = company.MaxTier
        };
    }
}
