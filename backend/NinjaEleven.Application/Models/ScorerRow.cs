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

/// <summary>
/// One line of a club's scorers table: a man of that club, his goals in a season, and
/// whether he is still there.
/// </summary>
/// <remarks>
/// The last field is the one that turns a list of numbers into a list of men. A scorer who
/// left the club is a fact the club's history is made of, and a screen that only ever showed
/// the men still under contract would quietly delete the club's history every time a
/// transfer window opened — and a club's all-time scorers list is the one page that must never
/// lose a name. So the answer is carried on the line, decided by the backend, and the client
/// only chooses which lines it is showing.
/// </remarks>
public class ClubScorerRow
{
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public int Age { get; init; }

    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Started { get; init; }
    public int CameOn { get; init; }

    /// <summary>
    /// Whether the player is still at the club: he has a membership that has not ended.
    /// Decided here and not by the client, which cannot know a contract from a shirt.
    /// </summary>
    public bool IsStillAtClub { get; init; }

    /// <summary>Where the player stands in the club's table, counted from one.</summary>
    public int Position { get; set; }

    /// <summary>
    /// Goals per appearance, or null when he never appeared.
    ///
    /// It is worked out here and not in the client because a number a screen invents is a
    /// number two screens may invent differently, and this one is the difference between a
    /// striker who scores every week and one who scored eight times in a season he was used
    /// twice. A player with no appearance has no rate and is given null rather than a zero:
    /// zero goals a game is a fact about a striker, and nothing at all is a fact about a man
    /// who never played.
    /// </summary>
    public double? GoalsPerAppearance { get; set; }
}
