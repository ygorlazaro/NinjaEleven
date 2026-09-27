namespace NinjaEleven.Domain.Matches;

/// <summary>
/// The cup tie a match belongs to, in the two numbers the engine needs to know whether the
/// tie is still alive at the final whistle.
///
/// It is here, and not in the service that starts the match, because the question "is this
/// ninety minutes enough to decide the tie" is a question about football and the engine is
/// what answers it. A second leg that ends level does not finish its match: it goes to the
/// spot, and the only thing that can say so is the part of the game that is running.
///
/// The legs swap ends, so the aggregate is added by club and never by side — the same rule
/// the tie's own aggregate is written under, and it is the reason the first leg's goals are
/// carried here as they were scored rather than as this match's home and away.
/// </summary>
public sealed class CupTieFacts
{
    private CupTieFacts(
        Guid tieId,
        bool isSecondLeg,
        Guid tieHomeTeamId,
        Guid tieAwayTeamId,
        int firstLegHomeGoals,
        int firstLegAwayGoals)
    {
        TieId = tieId;
        IsSecondLeg = isSecondLeg;
        TieHomeTeamId = tieHomeTeamId;
        TieAwayTeamId = tieAwayTeamId;
        FirstLegHomeGoals = firstLegHomeGoals;
        FirstLegAwayGoals = firstLegAwayGoals;
    }

    public Guid TieId { get; }

    /// <summary>
    /// False for a first leg. A first leg can never end level in a way that sends anybody to
    /// the spot, so the engine asks about this before it asks about the score.
    /// </summary>
    public bool IsSecondLeg { get; }

    /// <summary>The club that was at home in the first leg.</summary>
    public Guid TieHomeTeamId { get; }

    /// <summary>The club that was away in the first leg, and is at home in this one.</summary>
    public Guid TieAwayTeamId { get; }

    public int FirstLegHomeGoals { get; }

    public int FirstLegAwayGoals { get; }

    /// <summary>The tie as it stands with this match's score added to it.</summary>
    /// <param name="homeScore">This match's home score.</param>
    /// <param name="awayScore">This match's away score.</param>
    public (int Home, int Away) Aggregate(int homeScore, int awayScore) =>
        this.IsSecondLeg
            ? (FirstLegHomeGoals + awayScore, FirstLegAwayGoals + homeScore)
            : (homeScore, awayScore);

    public static CupTieFacts ForSecondLeg(
        Guid tieId,
        Guid tieHomeTeamId,
        Guid tieAwayTeamId,
        int firstLegHomeGoals,
        int firstLegAwayGoals) =>
        new(tieId, true, tieHomeTeamId, tieAwayTeamId, firstLegHomeGoals, firstLegAwayGoals);
}
