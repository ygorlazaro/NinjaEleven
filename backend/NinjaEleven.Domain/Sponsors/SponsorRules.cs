using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Sponsors;

/// <summary>
/// The constants of a shirt deal. Centralised so the sponsor system is tuned in one place
/// rather than scattered across action tables.
/// </summary>
public static class SponsorRules
{
    /// <summary>
    /// How many sponsors a club is offered at once.
    ///
    /// <para>
    /// Three, and exactly three: fewer is a short list to choose from and more is a list
    /// nobody reads. A club that only attracts two is shown two — a short list is a fact
    /// about the club, and padding it with companies that would refuse it would be a screen
    /// lying about who wants its name on the shirt.
    /// </para>
    /// </summary>
    public const int CandidateOffers = 3;

    /// <summary>
    /// How many clubs of the same division one company may have its name on.
    ///
    /// <para>
    /// Two, and not one: a brand that will not put a second name in the same championship is a
    /// rule that leaves a third of the pyramid without a shirt. A thirty-company catalogue with
    /// one club each would put twenty-eight clubs on the market at most, and sixteen of the
    /// sixty-four played every week with a bare back — a company that works in two divisions
    /// having a club in each is ordinary business, and the rule that kept the brand's name
    /// exclusive cost more shirts than the exclusivity was worth.
    /// </para>
    /// </summary>
    public const int MaxClubsPerDivision = 2;

    /// <summary>
    /// The length band of a sponsor's first offer: between this many and twice this many
    /// matches.
    /// </summary>
    public const int BaseOfferLength = 5;

    /// <summary>
    /// The deal a club is offered a new set of candidates for.
    ///
    /// <para>
    /// A club whose deal has one match left is a club about to be playing a match with
    /// nothing on the shirt, which is the moment a sponsor in the market actually signs
    /// somebody. The same rule is what an NPC follows without anybody watching.
    /// </para>
    /// </summary>
    public const int MatchesLeftBeforeRenewalWindow = 1;

    /// <summary>
    /// What size of company a sponsor is, dealt out of its own name.
    ///
    /// <para>
    /// A catalog of thirty identical sponsors is a catalog with no pyramid in it: every company
    /// paying the same for the same shirt, so a club's place, form and crowd would decide the
    /// price but the company would never decide whether it wanted the club at all. Keyed on the
    /// name rather than on a counter, because a name is the same in a world seeded today and in
    /// a world migrated onto the columns — the two arrive at the same book, and an identity
    /// column would hand a different one to every database it was seeded into.
    /// </para>
    /// </summary>
    public static SponsorSize SizeFor(string name)
    {
        var weight = (int)(StableHash.Of(name) % 3) + 1;

        return SponsorSize.ForWeight(weight);
    }
}