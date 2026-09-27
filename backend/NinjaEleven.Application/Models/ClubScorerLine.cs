namespace NinjaEleven.Application.Models;

/// <summary>
/// One player's goals for one club in one season, as the match lines add them up.
///
/// This is the shape a query returns: a player, a club, and what he did. There is no name and
/// no age here, because a line that carried a name would be a line built by a query per
/// player, and a scorers table over a division is a hundred of those. The name, the age and
/// whether the man is still at the club are put on by the service, which is where a decision
/// about a player belongs.
///
/// **The goals are the sum of the match lines and never a counter.** A counter kept alongside
/// the lines and a sum read out of them are two different numbers the day one of them drifts,
/// and a scorers table that printed the counter in one place and the sum in another would have
/// two answers to "how many has he scored". An own goal is a defender's goal and is kept in
/// its own column, so a centre-back's own goals never appear as his scoring.
/// </summary>
public class ClubScorerLine
{
    public required Guid PlayerId { get; init; }

    /// <summary>The club he was playing for when he scored them.</summary>
    public required Guid TeamId { get; init; }

    /// <summary>Goals scored for the club, summed from the match lines.</summary>
    public int Goals { get; init; }

    /// <summary>Goals he put through his own net, kept apart because they are not his scoring.</summary>
    public int OwnGoals { get; init; }

    /// <summary>Games started for the club in that season.</summary>
    public int Started { get; init; }

    /// <summary>Games he came off the bench for. Kept apart: a manager wants to know both.</summary>
    public int CameOn { get; init; }
}
