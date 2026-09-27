namespace NinjaEleven.Application.Models;

/// <summary>
/// One line of the top scorers table.
///
/// The goals come from the players' match lines and not from a counter kept alongside them.
/// A counter and the sum of the history are two different numbers, and a screen that showed
/// the counter in one place and the sum in another would be a screen with two answers to
/// "how many has he scored".
/// </summary>
public class ScorerRow
{
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public int Age { get; init; }
    public int Goals { get; init; }

    /// <summary>Where the player stands, counted from one. Decided by the backend.</summary>
    public int Position { get; set; }
}

/// <summary>
/// A window as a manager reads it: every match that was played in it, its scoreline, and the
/// account of it the match itself gave.
/// </summary>
public class MatchdayReport
{
    public Guid RoundId { get; set; }
    public int RoundNumber { get; set; }

    /// <summary>Which window of the matchday this was: the first, or the second.</summary>
    public int Window { get; set; }

    public List<MatchdayReportEntry> Entries { get; set; } = new();
}

/// <summary>
/// One match of the window, told in the words the match used. The summary is the goals'
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

    /// <summary>What the tie stands at across both legs, when this is a cup match.</summary>
    public int? AggregateHomeGoals { get; set; }

    public int? AggregateAwayGoals { get; set; }
}
