namespace FootballManager.Domain.Competitions;

/// <summary>
/// Binds a <see cref="Competition"/> to a <see cref="Seasons.Season"/>, i.e. one
/// concrete edition of a competition.
/// </summary>
public class CompetitionSeason
{
    public Guid Id { get; private set; }
    public Guid CompetitionId { get; private set; }
    public Guid SeasonId { get; private set; }

    private CompetitionSeason() { }

    public static CompetitionSeason Create(Guid competitionId, Guid seasonId)
    {
        return new CompetitionSeason
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            SeasonId = seasonId
        };
    }
}
