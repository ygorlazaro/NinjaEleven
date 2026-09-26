using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Leagues;

/// <summary>
/// Double round-robin by the circle method: every club meets every other one twice, once
/// at home and once away.
/// </summary>
/// <remarks>
/// One club is fixed and the others rotate, and the second leg is the first one with the
/// sides swapped. That is what makes the balance a property of the algorithm instead of a
/// coincidence: a club that was at home in the first leg is away in the second, so over
/// the season it plays exactly as many home games as away ones. With an odd number of
/// clubs one slot is a bye and produces no fixture.
/// </remarks>
public static class RoundRobin
{
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
                pairs.Add((rest[index], rest[moving - 1 - index]));
            }

            // The fixed club always meets whoever is in the middle slot, alternating sides.
            var middle = rest[moving / 2];
            pairs.Add(round % 2 == 0 ? (middle, anchor) : (anchor, middle));

            firstLeg.Add(pairs);

            rest.Insert(0, rest[^1]);
            rest.RemoveAt(rest.Count - 1);
        }

        var bothLegs = new List<IReadOnlyList<(Team Home, Team Away)>>(firstLeg.Count * 2);
        bothLegs.AddRange(firstLeg);
        bothLegs.AddRange(firstLeg.Select(pairs => pairs.Select(pair => (pair.Away, pair.Home)).ToList()));

        return bothLegs;
    }
}
