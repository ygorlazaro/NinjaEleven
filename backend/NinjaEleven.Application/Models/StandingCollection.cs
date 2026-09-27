using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

/// <summary>
/// Everything one competition edition's table is made of, gathered once.
///
/// The table itself and the crowd of a match in that table both need the same four things:
/// which clubs are in it, what has already been decided, what is being played right now, and
/// how strong each club is. Reading those a second time through a second set of queries would
/// be two answers to one question, and the two answers would start differing the moment one
/// of the games ended.
/// </summary>
/// <param name="View">Which edition this is, and which tier its table is.</param>
/// <param name="Seeds">The clubs and the squad strength their line starts from.</param>
/// <param name="Finished">The games that are over, and count.</param>
/// <param name="InProgress">The games being played right now, at their current score.</param>
/// <param name="Clubs">The clubs themselves, for a screen that has to draw a name and a badge.</param>
public record StandingCollection(
    CompetitionSeasonView View,
    IReadOnlyList<(Guid TeamId, double Stars)> Seeds,
    IReadOnlyList<MatchResultRow> Finished,
    IReadOnlyList<MatchResultRow> InProgress,
    IReadOnlyDictionary<Guid, Team>? Clubs = null)
{
    public int ClubsInDivision => Seeds.Count;

    /// <summary>The average squad strength of the division, which the crowd's opponent factor is measured against.</summary>
    public double AverageStars => Seeds.Count == 0
        ? 0
        : Seeds.Average(seed => seed.Stars);

    public double StarsOf(Guid teamId) =>
        Seeds.FirstOrDefault(seed => seed.TeamId == teamId).Stars;

    /// <summary>Where a club stands right now, or zero when it is not in this table.</summary>
    public int PositionOf(Guid teamId) =>
        StandingTable.Build(Seeds, Finished.Concat(InProgress).ToList())
            .FirstOrDefault(entry => entry.TeamId == teamId)?.Position ?? 0;
}
