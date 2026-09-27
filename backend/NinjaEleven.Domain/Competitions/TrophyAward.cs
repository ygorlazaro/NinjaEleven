namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// Which of the three trophies a place is worth.
/// </summary>
public enum TrophyKind
{
    /// <summary>First place. The title.</summary>
    Champion = 1,

    /// <summary>Second place.</summary>
    RunnerUp = 2,

    /// <summary>Third place.</summary>
    Third = 3
}

/// <summary>
/// A trophy on a club's shelf.
///
/// A trophy is a fact about a club and a season, so it is kept and not recomputed: the table
/// of a season that has been played five years ago still says who won it, and a club that
/// changed divisions afterwards still has the one it won. The division is recorded next to
/// the competition because "champion of the 3rd division" and "champion of the 1st" are
/// different claims, and the third place of a Supercup does not exist because a Supercup has
/// two clubs in it.
/// </summary>
public class TrophyAward
{
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid SeasonId { get; private set; }

    /// <summary>The competition it was won in, which is a division's edition or the cup's.</summary>
    public Guid CompetitionSeasonId { get; private set; }

    /// <summary>The tier, when the competition is a division's table.</summary>
    public Guid? DivisionId { get; private set; }

    public TrophyKind Kind { get; private set; }

    /// <summary>First, second or third. The number a screen sorts trophies by.</summary>
    public int Position { get; private set; }

    /// <summary>
    /// The money that came with it, and zero when nothing did. The column exists before the
    /// first prize is paid out on purpose: a shelf that has to be rebuilt to start paying for
    /// it is a shelf that will be rebuilt wrong, and a prize is not something to invent later
    /// into a structure that cannot hold one.
    /// </summary>
    public decimal PrizeMoney { get; private set; }

    public DateTimeOffset WonAt { get; private set; }

    private TrophyAward() { }

    public static TrophyAward Create(
        Guid teamId,
        Guid seasonId,
        Guid competitionSeasonId,
        Guid? divisionId,
        TrophyKind kind,
        decimal prizeMoney = 0m)
    {
        if (prizeMoney < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prizeMoney), prizeMoney, "A prize is money paid out; it cannot be negative.");
        }

        return new TrophyAward
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            SeasonId = seasonId,
            CompetitionSeasonId = competitionSeasonId,
            DivisionId = divisionId,
            Kind = kind,
            Position = (int)kind,
            PrizeMoney = prizeMoney,
            WonAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// The three trophies of a competition, for the clubs in the order they finished. A
    /// competition with fewer than three entrants is only given the places it has, so a
    /// Supercup hands out a title and nothing else.
    /// </summary>
    public static IReadOnlyList<TrophyAward> ForPodium(
        Guid seasonId,
        Guid competitionSeasonId,
        Guid? divisionId,
        IReadOnlyList<Guid> orderedTeamIds)
    {
        ArgumentNullException.ThrowIfNull(orderedTeamIds);

        var kinds = new[]
        {
            TrophyKind.Champion,
            TrophyKind.RunnerUp,
            TrophyKind.Third
        };

        return orderedTeamIds
            .Take(kinds.Length)
            .Select((teamId, index) =>
                TrophyAward.Create(teamId, seasonId, competitionSeasonId, divisionId, kinds[index]))
            .ToList();
    }
}
