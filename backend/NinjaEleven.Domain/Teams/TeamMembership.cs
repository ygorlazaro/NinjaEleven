namespace NinjaEleven.Domain.Teams;

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

    /// <summary>
    /// How many seasons the club has signed him for, which is what his wage is a share of.
    /// It is kept rather than derived from the dates because a contract is a promise about the
    /// future and the dates only say when it started and when it was called off: a
    /// three-season contract signed halfway through a season still owes three.
    /// </summary>
    public int ContractSeasons { get; private set; }

    /// <summary>
    /// The season the club signed him in, counted from one. It is what the contract's clock
    /// is read against, and it is kept rather than worked out from <see cref="StartDate"/>
    /// because a season is not a fixed number of days: a calendar year that contains the
    /// twenty-ninth of February is a day longer than the one before it, and a contract counted
    /// in days drifts by a day every time the calendar does, which is how a three-season deal
    /// becomes a two-season deal without anybody deciding anything.
    /// </summary>
    public int StartSeasonNumber { get; private set; }

    private TeamMembership() { }

    public static TeamMembership Create(
        Guid playerId,
        Guid teamId,
        DateOnly startDate,
        int contractSeasons = Finance.FinanceRules.DefaultContractSeasons,
        int startSeasonNumber = 1)
    {
        if (contractSeasons <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contractSeasons), contractSeasons, "A contract runs for some seasons or it is not a contract.");
        }

        if (startSeasonNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startSeasonNumber),
                startSeasonNumber,
                "A contract is signed in a season; the world has no season before the first.");
        }

        return new TeamMembership
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            TeamId = teamId,
            StartDate = startDate,
            EndDate = null,
            ContractSeasons = contractSeasons,
            StartSeasonNumber = startSeasonNumber
        };
    }

    public bool IsActiveOn(DateOnly date) => date >= StartDate && (EndDate is null || date <= EndDate);

    /// <summary>
    /// How many seasons of his contract are left, counted in seasons.
    ///
    /// A contract signed in the season it was made for three is finished at the end of the
    /// third, and the arithmetic is the whole rule: how far the season being played is from
    /// the one he was signed in, taken off the seasons he was promised.
    ///
    /// It never goes below zero. A contract whose seasons are all spent is a player who is
    /// free, and a negative number of seasons left would be a question the contract cannot
    /// answer.
    /// </summary>
    public int SeasonsLeft(int currentSeasonNumber) =>
        Math.Max(0, StartSeasonNumber + ContractSeasons - currentSeasonNumber);

    /// <summary>
    /// Whether this is the last season of his contract, which is what decides what another
    /// club has to pay for him.
    ///
    /// A club in the final year of a contract can sign him without paying a year's notice to
    /// whoever holds it, and a club that still owes him two more seasons has to buy the years
    /// as well as the man. That difference is a price, and it is the only thing that stops a
    /// squad being sold out from under itself in the summer.
    /// </summary>
    public bool IsInHisLastSeason(int currentSeasonNumber) => SeasonsLeft(currentSeasonNumber) <= 1;

    public void End(DateOnly endDate)
    {
        if (endDate < StartDate)
        {
            throw new ArgumentException("A membership cannot end before it starts.", nameof(endDate));
        }

        EndDate = endDate;
    }
}
