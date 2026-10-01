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

    /// <summary>
    /// The number this man wears for this club, or null while he has not been given one.
    ///
    /// <para>
    /// It lives on the contract and not on the player, because it is not part of who he is.
    /// A number belongs to a dressing room: the striker who is sold leaves his shirt on its
    /// peg, and the man bought in his place is given whatever is free on the day he signs. A
    /// number on the player would travel with him and two clubs would end up with a squad
    /// wearing the same shirts, which is the one thing the number is there to prevent.
    /// </para>
    ///
    /// <para>
    /// Null is the honest "not dealt yet", and it is a state the domain allows rather than
    /// one it works around: a world seeded before shirts existed has memberships with no
    /// number, and the seeder fills them in rather than the column being invented per row.
    /// A live contract with no number is still a club that has not finished dressing its
    /// squad, and <see cref="HasShirtNumber"/> is how that is said.
    /// </para>
    /// </summary>
    public int? ShirtNumber { get; private set; }

    /// <summary>Whether this contract has been given a shirt.</summary>
    public bool HasShirtNumber => ShirtNumber.HasValue;

    /// <summary>
    /// What the club pays him for one season, fixed for as long as this contract runs.
    ///
    /// <para>
    /// It is on the contract and not worked out from his attributes on every payday, and the
    /// reason is that a player who scores thirty goals does not become twice as expensive
    /// halfway through the season he is already under contract for. A wage that moved with the
    /// form would make the wage bill a thing the club watches rather than a thing it agreed,
    /// and a manager would be paying a striker for last month every time he scored. The number
    /// is settled when the deal is signed and when it is renewed, and between those two moments
    /// the club knows exactly what the season costs.
    /// </para>
    ///
    /// <para>
    /// It still changes, and it changes in the one direction that is honest: at a renewal, the
    /// player's age and his attributes are read again and the new figure is what the next
    /// seasons cost. A man who has improved gets paid more because he is better, and a man
    /// coming off a bad season gets paid less — which is a decision somebody has to make and
    /// take, rather than a number that quietly moves on its own.
    /// </para>
    /// </summary>
    public decimal Wage { get; private set; }

    private TeamMembership() { }

    public static TeamMembership Create(
        Guid playerId,
        Guid teamId,
        DateOnly startDate,
        int contractSeasons = Finance.FinanceRules.DefaultContractSeasons,
        int startSeasonNumber = 1,
        int? shirtNumber = null,
        decimal wage = 0m)
    {
        if (shirtNumber is { } number && !ShirtNumberRules.IsValid(number))
        {
            throw new ArgumentOutOfRangeException(
                nameof(shirtNumber), number, "A shirt number is a number a player wears.");
        }

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

        if (wage < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wage), wage, "A wage is not a debt.");
        }

        return new TeamMembership
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            TeamId = teamId,
            StartDate = startDate,
            EndDate = null,
            ContractSeasons = contractSeasons,
            StartSeasonNumber = startSeasonNumber,
            ShirtNumber = shirtNumber,
            Wage = wage
        };
    }

    /// <summary>
    /// Puts this man in a shirt.
    ///
    /// <para>
    /// The range is checked here and the clash is checked by whoever is holding the club's
    /// other contracts, because only they can see the whole dressing room. That split is not
    /// a convenience: a number outside one to ninety-nine is nonsense whoever asks for it, so
    /// it is refused by the object itself, while a number that another man is already wearing
    /// is not nonsense at all until you know about the other man.
    /// </para>
    ///
    /// <para>
    /// Taking the number a man already wears is allowed and does nothing. A manager who taps
    /// a number that has not changed should not be told he has changed it.
    /// </para>
    /// </summary>
    public void WearNumber(int number)
    {
        if (!ShirtNumberRules.IsValid(number))
        {
            throw new ArgumentOutOfRangeException(
                nameof(number), number, "A shirt number is a number a player wears.");
        }

        ShirtNumber = number;
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

    /// <summary>
    /// Agrees what a season of this contract costs.
    ///
    /// <para>
    /// It is separate from <see cref="Renew"/> because the two are different events. A signing
    /// and a renewal both agree a wage, and so does a world that has just grown a column and
    /// has to price the contracts it already had; only the first two of those also move the
    /// clock. A method called "renew" that quietly left the seasons alone would be a verb lying
    /// about what it did, and the seeder is exactly the caller that would be doing it.
    /// </para>
    /// </summary>
    public void AgreeWage(decimal wage)
    {
        if (wage < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(wage), wage, "A wage is not a debt.");
        }

        Wage = wage;
    }

    /// <summary>
    /// Signs him again, for as many seasons as the manager says and at whatever the wage is
    /// today.
    ///
    /// <para>
    /// The seasons are counted from the season being played rather than added to what is left,
    /// because a renewal is a decision about the future and not an extension of a tally. A man
    /// with one season left who is renewed for two has two seasons left afterwards, and a man
    /// with none who is renewed for three has three — which is the same arithmetic either way
    /// and is the reason <see cref="ContractSeasons"/> is written rather than incremented.
    /// </para>
    ///
    /// <para>
    /// The wage is a number the caller has just worked out from the man as he is today, and it
    /// is passed in rather than calculated here because a contract does not know what a player's
    /// attributes are worth: that is the valuation's business, and the entity that owns a wage
    /// is not the one that decides it. A renewal is also the only moment the wage moves, which
    /// is the whole of what <see cref="Wage"/> promises.
    /// </para>
    /// </summary>
    public void Renew(int seasons, int currentSeasonNumber, decimal wage, DateOnly on)
    {
        if (seasons < ContractRules.FewestRenewableSeasons || seasons > ContractRules.MostRenewableSeasons)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seasons),
                seasons,
                $"A renewal runs for {ContractRules.FewestRenewableSeasons} to {ContractRules.MostRenewableSeasons} seasons.");
        }

        if (currentSeasonNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentSeasonNumber),
                currentSeasonNumber,
                "The world has no season before the first.");
        }

        if (wage < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(wage), wage, "A wage is not a debt.");
        }

        // The clock is read against the season the renewal was signed in, so the man is owed
        // exactly the seasons that were agreed from this point on.
        StartSeasonNumber = currentSeasonNumber;
        ContractSeasons = seasons;
        AgreeWage(wage);
        StartDate = on;
    }

    /// <summary>
    /// Whether this contract has run out, which is the one thing a season boundary asks it.
    ///
    /// <para>
    /// A contract is over when the seasons it was promised are all in the past. It is checked
    /// against the season that has just finished, because that is the boundary the club is
    /// standing on when it asks: a deal signed for one season in season one is spent at the end
    /// of season one, not at the end of season two.
    /// </para>
    ///
    /// <para>
    /// The season asked about has therefore already been played, and that is the whole reason
    /// this reads one season further on than <see cref="SeasonsLeft"/> would. Those two answers
    /// are about different moments — one counts the season it is given and the other counts
    /// what is left after it — and reading both off the same number is how a club keeps a man
    /// for a season longer than it paid for.
    /// </para>
    /// </summary>
    public bool HasRunOut(int lastSeasonPlayed) => SeasonsLeft(lastSeasonPlayed + 1) <= 0;

    public void End(DateOnly endDate)
    {
        if (endDate < StartDate)
        {
            throw new ArgumentException("A membership cannot end before it starts.", nameof(endDate));
        }

        EndDate = endDate;
    }
}
