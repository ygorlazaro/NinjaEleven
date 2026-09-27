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
}
