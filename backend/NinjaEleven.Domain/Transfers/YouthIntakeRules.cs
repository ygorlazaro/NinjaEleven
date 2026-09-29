namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// The rule of the intake: how many young men a season opens with, how old they are and what
/// they are worth to a club that picks them up.
///
/// It is a count and not a ratio, and that is the whole argument. A ratio needs something to
/// divide, and a season's first day has nothing to divide by: nobody has retired yet, because
/// the world is being written. A market fed by a ratio therefore opened its first season with
/// no free agents at all, and a market with no free agents is a market a manager opens, reads
/// three hundred names who all belong to somebody, and closes — there is no signing to be
/// made, only purchases, and a club of twenty-three has nobody to sell to it.
///
/// So the number is fixed, and it is fixed per season rather than per world: every season
/// opens with a fresh intake, whatever happened in the one before, and the men arrive
/// unattached. A young player is somebody a club signs, not somebody a club buys: there is
/// nobody to charge, so a club that wants one of these men picks him up (see
/// <see cref="TransferWindowRules"/> for when he then walks through the door) and a club
/// that does not want him finds him on the market the same season, at his own worth.
///
/// The ages are the ones the game's own growth expects and the ones a manager reads a
/// seventeen-year-old at, and the goalkeeper share is the same share a club's own roster is
/// built with — the intake is drawn from the same pool the squads are drawn from, so the
/// men it deals are keepers where they are keepers and outfield players where they are
/// outfield players.
/// </summary>
public static class YouthIntakeRules
{
    /// <summary>
    /// How many young free agents a season opens with.
    /// 36 per division * 4 divisions = 144 total
    /// </summary>
    public const int FreeAgentsPerSeason = 144;

    /// <summary>The youngest a player arriving in the intake is.</summary>
    public const int YoungestAge = 16;

    /// <summary>The oldest a player arriving in the intake is.</summary>
    public const int OldestAge = 19;

    /// <summary>
    /// The share of the intake that is made of goalkeepers, the same share a club's own roster
    /// is built with: a market of ninety midfielders is not a market, it is one position.
    /// </summary>
    public const double GoalkeeperShare = 0.15;
}
