using FootballManager.Domain.Teams;

namespace FootballManager.Application.Models;

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
