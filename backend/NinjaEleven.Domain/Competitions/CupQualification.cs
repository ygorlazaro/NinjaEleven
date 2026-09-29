using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// Cup draw for 64 clubs: completely random draw for each round, no seeding.
/// All 64 clubs participate from the 32nd round (round of 64).
/// </summary>
public static class CupQualification
{
    /// <summary>
    /// Returns all 64 club IDs for the cup. No seeding, no qualification needed - all clubs enter.
    /// </summary>
    public static IReadOnlyList<Guid> GetAllClubsForCup(IReadOnlyDictionary<int, IReadOnlyList<StandingEntry>> standingsByTier)
    {
        ArgumentNullException.ThrowIfNull(standingsByTier);

        var allClubs = new List<Guid>();
        foreach (var entry in standingsByTier)
        {
            foreach (var standing in entry.Value)
            {
                allClubs.Add(standing.TeamId);
            }
        }

        if (allClubs.Count != CompetitionRules.TotalClubs)
        {
            throw new InvalidOperationException($"Expected {CompetitionRules.TotalClubs} clubs for the cup, got {allClubs.Count}");
        }

        return allClubs;
    }

    /// <summary>
    /// Randomly pairs clubs for a round. The club listed first in each pair is at home in the first leg.
    /// </summary>
    public static IReadOnlyList<(Guid Home, Guid Away)> RandomPairings(IReadOnlyList<Guid> clubs, Random random)
    {
        ArgumentNullException.ThrowIfNull(clubs);

        if (clubs.Count < 2)
        {
            throw new ArgumentException("A cup tie needs two clubs.", nameof(clubs));
        }

        if (clubs.Count % 2 != 0)
        {
            throw new ArgumentException(
                $"A cup is drawn with an even number of clubs, and {clubs.Count} is not one.",
                nameof(clubs));
        }

        // Shuffle the clubs randomly
        var shuffled = clubs.OrderBy(_ => random.Next()).ToList();

        var pairings = new List<(Guid, Guid)>(shuffled.Count / 2);

        for (var index = 0; index < shuffled.Count / 2; index++)
        {
            pairings.Add((shuffled[index * 2], shuffled[index * 2 + 1]));
        }

        return pairings;
    }

    /// <summary>How many clubs are left after a round of the bracket.</summary>
    public static int SurvivorsAfter(int roundNumber)
    {
        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundNumber), roundNumber, "A cup round is counted from one.");
        }

        return CompetitionRules.CupSize / (int)Math.Pow(2, roundNumber);
    }
}

/// <summary>
/// Cup bracket with random draw for each round. No fixed bracket - each round is drawn independently.
/// </summary>
public static class CupBracket
{
    /// <summary>Draws the next round's ties from the winners of the previous round. Completely random draw.</summary>
    /// <param name="decidedTies">The previous round's ties that have been resolved.</param>
    /// <param name="random">Random number generator for the draw.</param>
    public static IReadOnlyList<(Guid Home, Guid Away)> NextRoundPairings(
        IReadOnlyCollection<CupTie> decidedTies,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(decidedTies);

        if (decidedTies.Any(tie => !tie.IsResolved))
        {
            throw new ArgumentException(
                "A tie that has not been decided cannot send anybody into the next round.",
                nameof(decidedTies));
        }

        var winners = decidedTies
            .Where(t => t.IsResolved && t.WinnerTeamId.HasValue)
            .Select(t => t.WinnerTeamId!.Value)
            .ToList();

        if (winners.Count % 2 != 0)
        {
            throw new ArgumentException(
                $"A round of {winners.Count} winners cannot be halved into a next round.", nameof(decidedTies));
        }

        // Shuffle winners randomly for the next round draw
        var shuffled = winners.OrderBy(_ => random.Next()).ToList();

        var pairings = new List<(Guid, Guid)>(shuffled.Count / 2);

        for (var index = 0; index < shuffled.Count / 2; index++)
        {
            pairings.Add((shuffled[index * 2], shuffled[index * 2 + 1]));
        }

        return pairings;
    }

    /// <summary>The champion of the cup, and the only team left when there is one tie.</summary>
    public static Guid ChampionOf(CupTie final) =>
        final.IsResolved
            ? final.WinnerTeamId!.Value
            : throw new InvalidOperationException("A final that has not been decided has no champion.");
}