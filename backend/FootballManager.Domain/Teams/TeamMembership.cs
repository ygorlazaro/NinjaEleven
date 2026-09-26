namespace FootballManager.Domain.Teams;

/// <summary>
/// Contract link between a player and a club. Keeping the link as its own entity is
/// what makes transfers possible without changing the player identity.
/// </summary>
public class TeamMembership
{
    public Guid Id { get; private set; }
    public Guid PlayerId { get; private set; }
    public Guid TeamId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    private TeamMembership() { }

    public static TeamMembership Create(Guid playerId, Guid teamId, DateOnly startDate)
    {
        return new TeamMembership
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            TeamId = teamId,
            StartDate = startDate,
            EndDate = null
        };
    }

    public bool IsActiveOn(DateOnly date) => date >= StartDate && (EndDate is null || date <= EndDate);

    public void End(DateOnly endDate)
    {
        if (endDate < StartDate)
        {
            throw new ArgumentException("A membership cannot end before it starts.", nameof(endDate));
        }

        EndDate = endDate;
    }
}
