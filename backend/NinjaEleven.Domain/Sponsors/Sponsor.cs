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

    private Sponsor() { }

    public static Sponsor Create(string name, string industry, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A sponsor needs a name.", nameof(name));
        if (string.IsNullOrWhiteSpace(industry))
            throw new ArgumentException("A sponsor has an industry.", nameof(industry));

        return new Sponsor
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Industry = industry.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? "#f2d34f" : color.Trim()
        };
    }
}
