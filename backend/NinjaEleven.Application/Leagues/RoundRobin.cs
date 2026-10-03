using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Leagues;

/// <summary>
/// Double round-robin by the circle method: every club meets every other one twice, once
/// at home and once away.
/// </summary>
/// <remarks>
/// <para>
/// One club is fixed and the others rotate, and the second leg is the first one with the
/// sides swapped. That is what makes the balance a property of the algorithm instead of a
/// coincidence: a club that was at home in the first leg is away in the second, so over
/// the season it plays exactly as many home games as away ones. With an odd number of
/// clubs one slot is a bye and produces no fixture.
/// </para>
/// <para>
/// The sides are <b>alternated by pair index</b> and not by slot: in a round, the first
/// pair plays "first member at home", the second pair plays "second member at home", and so
/// on. That single rule is what makes a club change ends from one round to the next. The
/// circle method moves every club one slot around the circle per round, so a club that was
/// the first member of an even pair is the second member of an odd one on its way round,
/// and the parity of the pair index flips with it — the side flips with it.
/// </para>
/// <para>
/// Reading the sides off the slot instead is what a draw does when nobody thinks about it,
/// and it is how a club ends up playing fifteen home games in a row: the clubs in the
/// first half of the circle are the first member of their pair for as many rounds as they
/// stay in that half, which is most of the season. Measured on a sixteen-club division,
/// that draw leaves one club at fifteen consecutive home games followed by fifteen
/// consecutive away ones, and no club below seven.
/// </para>
/// </remarks>
public static class RoundRobin
{
    /// <summary>
    /// How many games in a row a club may play on the same side, and what the draw above
    /// is measured to achieve: never three.
    /// </summary>
    /// <remarks>
    /// Three in a row cannot be improved to two everywhere, and the reason is the shape of
    /// the problem rather than the quality of the search. A club that alternated perfectly
    /// would be decided by a single bit — "this club is at home on odd rounds" — and two
    /// such clubs could only ever meet if their bits differed. But in a single round-robin
    /// every club meets every other one, so every bit would have to differ from every other
    /// one, which is a two-colouring of a complete graph and does not exist for three clubs
    /// or more. At most two clubs can alternate without a single slip, and the draw measured
    /// here reaches that: two clubs alternate all season and every other one repeats its
    /// side three times in thirty rounds, while a whole sixteen-club division holds two
    /// occasions on which anybody plays three in a row.
    /// </remarks>
    public const int MaxConsecutiveSameSide = 3;

    public static IReadOnlyList<IReadOnlyList<(Team Home, Team Away)>> Build(IReadOnlyList<Team> teams)
    {
        if (teams.Count < 2)
        {
            throw new ArgumentException("Um campeonato precisa de pelo menos dois clubes.", nameof(teams));
        }

        var anchor = teams[0];
        var rest = teams.Skip(1).ToList();

        if (rest.Count % 2 == 0)
        {
            // Odd number of clubs: the last slot is the bye, so it never becomes a fixture.
            rest.RemoveAt(rest.Count - 1);
        }

        var moving = rest.Count;
        var firstLeg = new List<IReadOnlyList<(Team Home, Team Away)>>();

        for (var round = 0; round < moving; round++)
        {
            var pairs = new List<(Team Home, Team Away)>(moving / 2 + 1);

            for (var index = 0; index < moving / 2; index++)
            {
                pairs.Add(Faced(index, rest[index], rest[moving - 1 - index]));
            }

            // The fixed club always meets whoever is in the middle slot, with the two ends
            // of the pair swapped from one round to the next so that it changes ends too.
            var middle = rest[moving / 2];
            var withTheAnchor = round % 2 == 0
                ? (Home: middle, Away: anchor)
                : (Home: anchor, Away: middle);

            pairs.Add(Faced(moving / 2, withTheAnchor.Home, withTheAnchor.Away));

            firstLeg.Add(pairs);

            rest.Insert(0, rest[^1]);
            rest.RemoveAt(rest.Count - 1);
        }

        var bothLegs = new List<IReadOnlyList<(Team Home, Team Away)>>(firstLeg.Count * 2);
        bothLegs.AddRange(firstLeg);
        bothLegs.AddRange(firstLeg.Select(pairs => pairs.Select(pair => (pair.Away, pair.Home)).ToList()));

        return bothLegs;
    }

    /// <summary>
    /// Puts the two ends of one pair on their sides, by where the pair sits in the round.
    /// </summary>
    private static (Team Home, Team Away) Faced(int pairIndex, Team first, Team second) =>
        pairIndex % 2 == 0 ? (first, second) : (second, first);
}
