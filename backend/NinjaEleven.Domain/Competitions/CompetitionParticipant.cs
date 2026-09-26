namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// A club taking part in one edition of a competition. This indirection allows the
/// same club to compete in several competitions at the same time.
/// </summary>
public class CompetitionParticipant
{
    public Guid Id { get; private set; }
    public Guid CompetitionSeasonId { get; private set; }
    public Guid TeamId { get; private set; }

    private CompetitionParticipant() { }

    public static CompetitionParticipant Create(Guid competitionSeasonId, Guid teamId)
    {
        return new CompetitionParticipant
        {
            Id = Guid.NewGuid(),
            CompetitionSeasonId = competitionSeasonId,
            TeamId = teamId
        };
    }
}
