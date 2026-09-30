namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// The pyramid a finished season hands to the next one: which clubs start it in which tier.
/// </summary>
/// <remarks>
/// <para>
/// It is worked out from the movements rather than settled club by club, because the four
/// divisions are a closed system: what goes up from one tier is exactly what comes down from
/// the one above it, and a pyramid assembled club by club cannot know that. A tier therefore
/// always comes out with the number of clubs the rules say it holds, and the check for it is
/// part of the answer rather than a surprise somebody discovers three competitions later.
/// </para>
/// </remarks>
public static class Pyramid
{
    /// <summary>
    /// The clubs that start the next season in a tier: the ones that stay in it, the ones
    /// coming up from below and the ones going down from above.
    /// </summary>
    /// <param name="tier">The tier being assembled.</param>
    /// <param name="movements">Where every club in the pyramid ends up.</param>
    /// <returns>The clubs of that tier, in the order they arrived.</returns>
    /// <exception cref="InvalidOperationException">
    /// The tier does not come out with the number of clubs the rules say it holds, which
    /// means the tables it was worked out from were not a season's tables.
    /// </exception>
    public static IReadOnlyList<Guid> ClubsOf(
        int tier,
        IReadOnlyDictionary<int, DivisionMovement> movements)
    {
        // Every movement says where its club *ends*, so the whole answer comes from asking
        // all of them. Reading a tier's own list and then adding what arrives counts the
        // clubs that are leaving twice over — they are in the tier's own list, which holds
        // the staying clubs and the relegated ones together, and never in the arrivals — so
        // every club that moved would be in two tiers at once and the pyramid would grow by
        // all of them: sixteen clubs become twenty-four, and the country becomes eighty-eight.
        var clubs = movements.Values
            .SelectMany(division => division.Movements)
            .Where(movement => movement.ToTier == tier)
            .Select(movement => movement.TeamId)
            .Distinct()
            .ToList();

        if (clubs.Count != CompetitionRules.ClubsPerDivision)
        {
            throw new InvalidOperationException(
                $"Tier {tier} came out with {clubs.Count} clubs instead of " +
                $"{CompetitionRules.ClubsPerDivision}. The divisions it was worked out from are " +
                "not sixteen apiece.");
        }

        return clubs;
    }
}
