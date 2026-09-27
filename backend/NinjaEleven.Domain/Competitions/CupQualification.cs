using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// Which thirty-two clubs are in the cup, and how they are paired in the first round.
///
/// The rule is one method and it takes the last season's finished tables, because a cup drawn
/// from the season it belongs to cannot be drawn at the start of it. A club's place in the
/// pyramid and its finish in its division are both inputs, so the best-placed club in the
/// third division is in the cup on the same terms as the champion of the first — which is the
/// only way a club that was promoted on the strength of one season is not immediately back
/// where it started.
///
/// The pairing is seeded: the clubs are ranked, and the strongest meets the weakest. Seeding
/// exists so the first round is not decided by the alphabet, and it is why a division's
/// strongest club and its eleventh-placed club do not meet in the first round of a cup.
/// </summary>
public static class CupQualification
{
    /// <summary>Where a club sits in the seed list, and therefore how it enters the bracket.</summary>
    /// <param name="TeamId">The club.</param>
    /// <param name="Tier">The tier it finished in. 1 is the top.</param>
    /// <param name="DivisionPosition">Where it finished in its division, counted from one.</param>
    public readonly record struct Seed(Guid TeamId, int Tier, int DivisionPosition)
    {
        /// <summary>Lower is better: the champion of the top division is the first seed.</summary>
        public int Rank => ((Tier - 1) * 1000) + DivisionPosition;
    }

    /// <summary>
    /// Ranks every club in the pyramid and takes the best thirty-two.
    /// </summary>
    /// <param name="standingsByTier">The finished table of each tier, best first.</param>
    public static IReadOnlyList<Seed> Rank(IReadOnlyDictionary<int, IReadOnlyList<StandingEntry>> standingsByTier)
    {
        ArgumentNullException.ThrowIfNull(standingsByTier);

        if (standingsByTier.Count == 0)
        {
            throw new ArgumentException("There is nobody to seed from.", nameof(standingsByTier));
        }

        return standingsByTier
            .SelectMany(entry => entry.Value.Select((standing, index) =>
                new Seed(standing.TeamId, entry.Key, standing.Position > 0 ? standing.Position : index + 1)))
            .OrderBy(seed => seed.Rank)
            .Take(CompetitionRules.CupSize)
            .ToList();
    }

    /// <summary>
    /// Pairs the seeds for the first round: first against last, second against second-last,
    /// and so on. The club listed first in each pair is at home in the first leg.
    /// </summary>
    public static IReadOnlyList<(Guid Home, Guid Away)> FirstRoundPairings(IReadOnlyList<Seed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        if (seeds.Count < 2)
        {
            throw new ArgumentException("A cup tie needs two clubs.", nameof(seeds));
        }

        if (seeds.Count % 2 != 0)
        {
            throw new ArgumentException(
                $"A cup is drawn with an even number of clubs, and {seeds.Count} is not one.",
                nameof(seeds));
        }

        var pairings = new List<(Guid, Guid)>(seeds.Count / 2);

        for (var index = 0; index < seeds.Count / 2; index++)
        {
            pairings.Add((seeds[index].TeamId, seeds[seeds.Count - 1 - index].TeamId));
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
/// How a finished tie turns into the next round's pairings.
///
/// The winner of one tie plays the winner of the tie beside it, so the bracket is a real
/// bracket: a club that reaches a final has been through the half of the draw that put it
/// there, and the two finalists are the winners of the two halves. Pairing by adjacency of
/// the previous round is what makes that true, and it is why the final is two specific clubs
/// rather than whichever two happened to win most ties.
/// </summary>
public static class CupBracket
{
    /// <summary>Draws the next round's ties from the ties that were just decided.</summary>
    /// <param name="decidedTies">The previous round's ties, in the order they were played.</param>
    public static IReadOnlyList<(Guid Home, Guid Away)> NextRoundPairings(
        IReadOnlyCollection<CupTie> decidedTies)
    {
        ArgumentNullException.ThrowIfNull(decidedTies);

        if (decidedTies.Any(tie => !tie.IsResolved))
        {
            throw new ArgumentException(
                "A tie that has not been decided cannot send anybody into the next round.",
                nameof(decidedTies));
        }

        var ordered = decidedTies.OrderBy(tie => tie.RoundNumber).ToList();

        if (ordered.Count % 2 != 0)
        {
            throw new ArgumentException(
                $"A round of {ordered.Count} ties cannot be halved into a next round.", nameof(decidedTies));
        }

        var pairings = new List<(Guid, Guid)>(ordered.Count / 2);

        for (var index = 0; index < ordered.Count / 2; index++)
        {
            // The home club of the new tie is whoever was at home in the earlier tie, so the
            // leg that decides a tie on penalties is not also the leg that decides who is at
            // home in the next one.
            var earlier = ordered[index * 2];
            var later = ordered[index * 2 + 1];

            pairings.Add((earlier.WinnerTeamId!.Value, later.WinnerTeamId!.Value));
        }

        return pairings;
    }

    /// <summary>The champion of the cup, and the only team left when there is one tie.</summary>
    public static Guid ChampionOf(CupTie final) =>
        final.IsResolved
            ? final.WinnerTeamId!.Value
            : throw new InvalidOperationException("A final that has not been decided has no champion.");
}
