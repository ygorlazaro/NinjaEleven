namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// A company of a given size, and everything that follows from being that size.
///
/// <para>
/// The four numbers travel together because they are one decision and not four: a national
/// brand that keeps eight shirts, works in the top two divisions and insists on a club above
/// average is one company, and any one of those four given on its own would describe a company
/// the rest of the game has no way to reason about.
/// </para>
/// </summary>
/// <param name="Weight">1 local, 2 regional, 3 national.</param>
/// <param name="MaxClubs">How many shirts the company is willing to have at once.</param>
/// <param name="MinAppeal">
/// How good a club has to be before the company will put its name on it, in the same units as
/// <see cref="SponsorPricing.Appeal"/>.
/// </param>
/// <param name="MaxTier">The best division the company approaches. Tier 1 is the top.</param>
public record SponsorSize(int Weight, int MaxClubs, double MinAppeal, int MaxTier)
{
    /// <summary>
    /// The shop round the corner: three shirts, any club, and it will not be travelling to a
    /// fourth division for the audience.
    /// </summary>
    public static SponsorSize Local { get; } = new(
        1, SponsorPricing.MaxClubsForWeight(1), SponsorPricing.DefaultMinAppeal, SponsorPricing.MaxTierForWeight(1));

    /// <summary>
    /// The company whose name is on the shirt of whoever is winning: four shirts, and a club
    /// that is not losing.
    /// </summary>
    public static SponsorSize Regional { get; } = new(
        2, SponsorPricing.MaxClubsForWeight(2), SponsorPricing.RegionalMinAppeal, SponsorPricing.MaxTierForWeight(2));

    /// <summary>
    /// The brand that is on a shirt because the shirt is going to be on television: eight
    /// shirts, a club above average, and not below the second division.
    /// </summary>
    public static SponsorSize National { get; } = new(
        3, SponsorPricing.MaxClubsForWeight(3), SponsorPricing.NationalMinAppeal, SponsorPricing.MaxTierForWeight(3));

    /// <summary>The size of a given weight, which is how a number becomes a company.</summary>
    public static SponsorSize ForWeight(int weight) => weight switch
    {
        1 => Local,
        3 => National,
        _ => Regional
    };
}
