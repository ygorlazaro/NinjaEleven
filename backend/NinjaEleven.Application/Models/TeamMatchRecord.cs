namespace NinjaEleven.Application.Models;

/// <summary>
/// One finished match of a club, resolved into what a manager wants to see when he looks a
/// club up: who it was against, where, how it ended and on what date.
///
/// The result is not stored here. It is a reading of the two scores and the side the club
/// was on, because a result stored beside the score is a second answer to a question the
/// score already answers, and the two drift apart the first time a match is corrected.
/// </summary>
public class TeamMatchRecord
{
    public Guid MatchId { get; init; }
    public string OpponentName { get; init; } = string.Empty;

    /// <summary>
    /// The club that was on the other side of it. A name is a door, and a door needs the id
    /// of the thing it opens: an opponent listed as text is an opponent nobody can look up.
    /// </summary>
    public Guid OpponentTeamId { get; init; }

    public bool IsHome { get; init; }

    /// <summary>The club's own goals, and the opponent's. Ordered for the club, not for the fixture.</summary>
    public int GoalsFor { get; init; }

    public int GoalsAgainst { get; init; }
    public int RoundNumber { get; init; }
    public DateTimeOffset PlayedAt { get; init; }

    // Head-to-head specific fields
    public string? SeasonName { get; init; }
    public string? CompetitionName { get; init; }
    public string? PhaseName { get; init; }
    public int? Attendance { get; init; }

    // Stadium info
    public string? StadiumName { get; init; }
}

/// <summary>
/// The whole story of one rivalry, counted.
///
/// <para>
/// It is not the last few meetings added up: those are a page the manager reads to remember
/// the fixture, and a rivalry's ledger is every meeting ever played. A club that has beaten
/// someone nine times out of ten and lost the last four has a record of six wins and a
/// recent run of four defeats, and a screen that printed the second one as the first would
/// be answering a question nobody asked.
/// </para>
/// </summary>
public class HeadToHeadSummary
{
    /// <summary>Meetings the two clubs have played, all of them, in any competition.</summary>
    public int Played { get; init; }

    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }

    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }

    /// <summary>
    /// Goals for less goals against, computed rather than carried. A stored difference is a
    /// number that can be wrong; a difference of two numbers that are both on the same row
    /// cannot.
    /// </summary>
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
