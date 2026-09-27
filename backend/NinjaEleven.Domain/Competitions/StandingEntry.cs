namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One finished match, as a table needs to see it: who played, how many goals, and how many
/// cards. It carries nothing about how the match felt, because a table is the one place in
/// the game where a goal and a booking are the whole story.
/// </summary>
public readonly record struct MatchResultRow(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeGoals,
    int AwayGoals,
    int HomeRedCards = 0,
    int AwayRedCards = 0,
    int HomeYellowCards = 0,
    int AwayYellowCards = 0)
{
    public Guid ScorerOf(Guid teamId) => teamId == HomeTeamId ? AwayTeamId : HomeTeamId;

    public int GoalsFor(Guid teamId) => teamId == HomeTeamId ? HomeGoals : AwayGoals;

    public int GoalsAgainst(Guid teamId) => teamId == HomeTeamId ? AwayGoals : HomeGoals;

    public int RedCardsOf(Guid teamId) => teamId == HomeTeamId ? HomeRedCards : AwayRedCards;

    public int YellowCardsOf(Guid teamId) => teamId == HomeTeamId ? HomeYellowCards : AwayYellowCards;

    public bool Involves(Guid teamId) => teamId == HomeTeamId || teamId == AwayTeamId;

    /// <summary>The three points for a win, one for a draw.</summary>
    public int PointsFor(Guid teamId)
    {
        var scored = GoalsFor(teamId);
        var conceded = GoalsAgainst(teamId);

        return scored > conceded ? 3 : scored == conceded ? 1 : 0;
    }
}

/// <summary>
/// One club's line in a table.
/// </summary>
public class StandingEntry
{
    public required Guid TeamId { get; init; }
    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }

    /// <summary>The squad strength shown next to the club's name in a table.</summary>
    public double Stars { get; init; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points => Wins * 3 + Draws;

    /// <summary>
    /// Where the club finished, counted from one. A table that is sorted has a position, and
    /// a screen that is told its position does not sort it again and get it wrong.
    /// </summary>
    public int Position { get; internal set; }
}

/// <summary>
/// The order of a table, in one place.
///
/// The order is: points, goal difference, goals scored, the head-to-head of the clubs still
/// level on all three, then the fewest red cards and the fewest yellows. The head-to-head is
/// a mini-table over the matches between the tied clubs rather than a pairwise comparison, so
/// a three-way tie is settled by all three of their matches against each other.
///
/// This lives in the domain and not in a service for the same reason the rest of the rules do:
/// a table that is sorted twice, in two places, with two different sets of tiebreakers, is a
/// table that shows one order to the manager and another to the promotion rules.
/// </summary>
public static class StandingTable
{
    /// <summary>
    /// Builds one line per club from the results, then orders them. Every club is a
    /// participant, whether it has played or not, so a table on the first matchday is a table
    /// rather than an empty screen.
    /// </summary>
    /// <param name="teamIds">The clubs in the division, with their squad strength.</param>
    /// <param name="results">Every finished match of the division.</param>
    public static IReadOnlyList<StandingEntry> Build(
        IReadOnlyCollection<(Guid TeamId, double Stars)> teamIds,
        IReadOnlyCollection<MatchResultRow> results)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        ArgumentNullException.ThrowIfNull(results);

        var rows = teamIds
            .Select(entry => BuildRow(entry.TeamId, entry.Stars, results))
            .ToList();

        return Sort(rows, results);
    }

    private static StandingEntry BuildRow(Guid teamId, double stars, IReadOnlyCollection<MatchResultRow> results)
    {
        var played = 0;
        var wins = 0;
        var draws = 0;
        var losses = 0;
        var goalsFor = 0;
        var goalsAgainst = 0;
        var yellows = 0;
        var reds = 0;

        foreach (var result in results)
        {
            if (!result.Involves(teamId)) continue;

            played++;
            goalsFor += result.GoalsFor(teamId);
            goalsAgainst += result.GoalsAgainst(teamId);
            yellows += result.YellowCardsOf(teamId);
            reds += result.RedCardsOf(teamId);

            var points = result.PointsFor(teamId);
            if (points == 3) wins++;
            else if (points == 1) draws++;
            else losses++;
        }

        return new StandingEntry
        {
            TeamId = teamId,
            Played = played,
            Wins = wins,
            Draws = draws,
            Losses = losses,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
            YellowCards = yellows,
            RedCards = reds,
            Stars = stars
        };
    }

    /// <summary>
    /// Orders a set of lines and stamps each one with the position it ended in. Grouping the
    /// lines that are level on points, goal difference and goals scored, and breaking only
    /// those groups by head-to-head, is what makes the head-to-head the last resort it is
    /// supposed to be.
    /// </summary>
    public static IReadOnlyList<StandingEntry> Sort(
        IReadOnlyCollection<StandingEntry> rows,
        IReadOnlyCollection<MatchResultRow> results)
    {
        var ordered = rows
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.GoalDifference)
            .ThenByDescending(row => row.GoalsFor)
            .ToList();

        var sorted = new List<StandingEntry>(ordered.Count);
        var index = 0;

        while (index < ordered.Count)
        {
            var end = index + 1;
            while (end < ordered.Count && LevelOnEverything(ordered[index], ordered[end])) end++;

            var group = ordered.GetRange(index, end - index);
            sorted.AddRange(group.Count == 1 ? group : BreakTie(group, results));
            index = end;
        }

        for (var position = 0; position < sorted.Count; position++)
        {
            sorted[position].Position = position + 1;
        }

        return sorted;
    }

    private static bool LevelOnEverything(StandingEntry left, StandingEntry right) =>
        left.Points == right.Points
        && left.GoalDifference == right.GoalDifference
        && left.GoalsFor == right.GoalsFor;

    private static IEnumerable<StandingEntry> BreakTie(
        IReadOnlyCollection<StandingEntry> group,
        IReadOnlyCollection<MatchResultRow> results)
    {
        var groupIds = group.Select(row => row.TeamId).ToHashSet();
        var mutual = results.Where(result => groupIds.Contains(result.HomeTeamId)).ToList();

        return group
            .OrderByDescending(row => Sum(mutual, row.TeamId, (scored, conceded) => scored > conceded ? 3 : scored == conceded ? 1 : 0))
            .ThenByDescending(row => Sum(mutual, row.TeamId, (scored, conceded) => scored - conceded))
            .ThenByDescending(row => Sum(mutual, row.TeamId, (scored, _) => scored))
            .ThenBy(row => row.RedCards)
            .ThenBy(row => row.YellowCards)
            .ThenByDescending(row => row.GoalsFor)
            // Squad strength last, and it is here because a table is ordered even when nothing
            // has separated the clubs in it. On the morning of matchday one every line is zero
            // for every club, and a table ordered by nothing at all is a table in whatever
            // order the database happened to return: the manager's own club could be first
            // because he is the strongest or last because his id sorts low, and neither is a
            // fact about football. The strength of the squad is the one thing actually known
            // about two clubs level on everything else, so it decides — as the last resort it
            // is, behind every result.
            .ThenByDescending(row => row.Stars)
            .ToList();
    }

    private static int Sum(
        IReadOnlyCollection<MatchResultRow> results,
        Guid teamId,
        Func<int, int, int> selector)
    {
        var total = 0;
        foreach (var result in results)
        {
            if (!result.Involves(teamId)) continue;
            total += selector(result.GoalsFor(teamId), result.GoalsAgainst(teamId));
        }

        return total;
    }
}
