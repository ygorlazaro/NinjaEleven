using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One line of the classification table. The backend is the only place allowed to
/// compute it: the frontend renders it as it arrives.
/// </summary>
public class StandingRow
{
    public required Guid TeamId { get; init; }
    public Team? Team { get; set; }

    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public double Stars { get; init; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points => Wins * 3 + Draws;
}

/// <summary>
/// One line of the top scorers table, derived from the season state of the players.
/// </summary>
public class ScorerRow
{
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public int Age { get; init; }
    public int Goals { get; init; }
}

/// <summary>
/// A round as a manager reads it: every match that was played, its scoreline, and the
/// account of it the match itself gave.
/// </summary>
public class MatchdayReport
{
    public Guid RoundId { get; set; }
    public int RoundNumber { get; set; }
    public List<MatchdayReportEntry> Entries { get; set; } = new();
}

/// <summary>
/// One match of the round, told in the words the match used. The summary is the goals'
/// own narration read back from the events, never a fresh account of a match that has
/// already been narrated once.
/// </summary>
public class MatchdayReportEntry
{
    public Guid FixtureId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string HomeShortName { get; set; } = string.Empty;
    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public string AwayShortName { get; set; } = string.Empty;
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public string Summary { get; set; } = string.Empty;
}
