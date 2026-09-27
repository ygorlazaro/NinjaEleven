using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One line of the classification table, as a screen reads it.
///
/// The line is a carrier: the numbers and the order were decided by the domain's
/// <c>StandingTable</c>, and a screen is told the position instead of being trusted to sort.
/// A table that is sorted again in the browser is a table that can disagree with the promotion
/// rules, and a manager who is told his club is ninth by the promotion pass and tenth by the
/// screen has been given two answers to one question.
/// </summary>
public class StandingRow
{
    public required Guid TeamId { get; init; }
    public Team? Team { get; set; }

    /// <summary>Where the club stands, counted from one. Decided by the backend.</summary>
    public int Position { get; init; }

    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }

    /// <summary>
    /// The squad strength, averaged over the whole squad. Not the eleven that happened to be
    /// on the pitch: a club's strength is what it is, whether or not anybody is injured.
    /// </summary>
    public double Stars { get; init; }

    /// <summary>
    /// Which band of the table this line is in — promotion, relegation, safe, or none for a
    /// knockout. Decided by the backend from its own rules so a screen that paints the bands
    /// paints the same ones the season's end will move clubs by.
    /// </summary>
    public TableZone Zone { get; init; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points => Wins * 3 + Draws;
}

/// <summary>
/// A table as a manager reads it: where the clubs are, and where they would be if the games
/// still being played went a certain way.
/// </summary>
public class CompetitionStandings
{
    public required Guid CompetitionSeasonId { get; init; }
    public Guid? SeasonId { get; init; }
    public Guid? DivisionId { get; init; }

    /// <summary>1 is the top of the pyramid. Null for a competition with no division.</summary>
    public int? Tier { get; init; }

    public required string CompetitionName { get; init; }

    /// <summary>
    /// The table of the games that are over. This is the official one: it is what promotion,
    /// relegation and the trophies are worked out from, and it does not move until a game
    /// ends.
    /// </summary>
    public IReadOnlyList<StandingRow> Official { get; init; } = Array.Empty<StandingRow>();

    /// <summary>
    /// The table counting the games in progress at their current score.
    ///
    /// Every unfinished game of the competition is in it, not only the manager's own: a table
    /// that projects one match and ignores the three happening at the same time is a table
    /// that is wrong in a way nobody can see, because the only way to notice is to watch a
    /// game the manager is not in. The order is worked out by the same rules and the same
    /// tiebreakers as the official one — the difference is the results, not the arithmetic.
    /// </summary>
    public IReadOnlyList<StandingRow> Projected { get; init; } = Array.Empty<StandingRow>();

    /// <summary>Whether any game in the competition is still being played.</summary>
    public bool HasLiveMatches { get; init; }
}
