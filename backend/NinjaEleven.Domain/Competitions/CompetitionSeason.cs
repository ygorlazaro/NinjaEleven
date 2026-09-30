namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One competition running inside one season.
///
/// The division is what makes this a division rather than a league in general: the same
/// "Campeonato Brasileiro" competition runs four times in a season, once per tier, and each
/// of those four editions has its own sixteen clubs, its own thirty matchdays and its
/// own table. A cup edition leaves the division null, because a cup is drawn from the whole
/// pyramid and does not belong to one of its tiers.
/// </summary>
public class CompetitionSeason
{
    public Guid Id { get; private set; }
    public Guid CompetitionId { get; private set; }
    public Guid SeasonId { get; private set; }

    /// <summary>The tier this edition is the table of, or null for a cup and a Supercup.</summary>
    public Guid? DivisionId { get; private set; }

    private CompetitionSeason() { }

    public static CompetitionSeason Create(Guid competitionId, Guid seasonId, Guid? divisionId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            SeasonId = seasonId,
            DivisionId = divisionId
        };

    /// <summary>Whether this edition is one tier's table, as opposed to a knockout.</summary>
    public bool IsDivision => DivisionId is not null;
}
