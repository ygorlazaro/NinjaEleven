namespace NinjaEleven.Domain.Teams;

/// <summary>
/// How a club's ground is built, and what a seat costs.
///
/// <para>
/// <b>A ground is a decision, and a decision takes time.</b> The three projects below are the
/// whole catalogue: a club cannot order any number of seats it likes, at any price it likes, on
/// any timescale it likes. Each one is a fixed number of seats for a fixed price over a fixed
/// number of rounds, which is what makes an expansion a plan rather than a wish — a manager who
/// starts the ten-thousand one in September knows the seats are there in January and not before,
/// and a rival who builds in the same window finishes in the same window.
/// </para>
///
/// <para>
/// <b>The prices are set against the pyramid, not against each other.</b> A second-division
/// ground is worth a few hundred thousand and a first-division one is worth several million,
/// so a fixed price per seat would price the two ends of the country wrongly: cheap where the
/// money is and ruinous where it is not. What is constant is the shape — a bigger project costs
/// more, takes longer and buys more seats — and what is not constant is the amount, which walks
/// up the divisions.
/// </para>
///
/// <para>
/// <b>The price of a seat is not a price per seat.</b> A club is not charged by how many seats
/// it adds; it is charged what the project costs, and the middle project works out cheaper per
/// seat than either end. That is what the catalogue says and it is not corrected here, because
/// the three prices are the agreed ones — what matters is that a club can work the three of
/// them out from its own ground and pick, and that none of them is reachable by an argument
/// about how stadium building ought to work.
/// </para>
///
/// <para>
/// <b>A ground under construction still holds people.</b> It holds fewer, and the factor is a
/// domain number rather than a feeling: a club playing a season in a half-closed ground has a
/// real attendance that a manager can budget from, and a rule that simply refused to count the
/// crowd would hand the club a season of free football and a season of income nobody saw.
/// </para>
/// </summary>
public static class StadiumRules
{
    /// <summary>
    /// The most seats a ground in this game can ever hold.
    ///
    /// It is a ceiling because the ladder tops out: a first-division club that keeps winning
    /// will keep asking, and without a bound the number that comes back would keep growing with
    /// it. Eighty thousand is a Maracanã, and a club that fills one has run out of ground to
    /// build rather than run out of demand.
    /// </summary>
    public const int LargestCapacity = 80_000;

    /// <summary>
    /// How much of a ground a crowd gets while it is being worked on.
    ///
    /// Three quarters, because work at a football ground closes one stand rather than all four:
    /// a club playing a season in a building site is still playing in front of its people, and
    /// the ones who cannot get a seat are the ones who would not have come to a mid-table match
    /// anyway.
    /// </summary>
    public const double WorksAttendanceFactor = 0.75;

    /// <summary>What the seats the game sells themselves for, at the bottom of the band.</summary>
    public const decimal CheapestTicket = 5m;

    /// <summary>The dearest a seat may be, whatever a club's crowd would bear.</summary>
    public const decimal DearestTicket = 30m;

    /// <summary>
    /// What a seat costs at a ground that is full.
    ///
    /// <para>
    /// Below <see cref="DearestTicket"/> on purpose, and the doc on
    /// <see cref="TicketPriceFor"/> says why: the dearest seat on the price list exists in the
    /// curve for a club to price one exceptional match at, and a club's standing price is not
    /// that.
    /// </para>
    /// </summary>
    public const decimal FullHouseTicket = 20m;

    /// <summary>
    /// The three projects, cheapest first.
    ///
    /// <para>
    /// Read across: seats added, what it costs, and how many rounds of football the ground is a
    /// building site for. Each row is more money for more seats and more time, which is what
    /// makes a project a plan a manager can schedule: the ten-thousand one approved in September
    /// has its seats in January, and a rival who approved the same project in the same window
    /// finishes in the same window.
    /// </para>
    /// </summary>
    public static readonly StadiumProject[] Projects =
    {
        // A small stand: the answer of a club whose ground is nearly full on a good day, and the
        // only one of the three that is finished inside a single round.
        new(Seats: 2_000, Cost: 900_000m, Rounds: 1),
        new(Seats: 5_000, Cost: 2_200_000m, Rounds: 2),
        new(Seats: 10_000, Cost: 4_600_000m, Rounds: 4)
    };

    /// <summary>
    /// How many seats a club's ground is built to in a division, when the world is drawn.
    ///
    /// <para>
    /// A ground belongs to a club, the same way a crowd does, and it is sized to the division
    /// the club is in. One size for all sixty-four was never neutral: it handed a fourth
    /// division club a first-division ground, which filled to a tenth and left the whole bottom
    /// of the pyramid economically dead — a gate of a few hundred limos is not a season a club
    /// can pay wages out of. Sizing the ground to the ladder is also what puts every division in
    /// the same position: a median club fills about three quarters of its own ground and its
    /// best club does not fit in it, which is the pressure to build that the pyramid is supposed
    /// to be under everywhere and not only at the top.
    /// </para>
    ///
    /// <para>
    /// The first division keeps five thousand seats on purpose. That is the ground whose
    /// median club sells out several times a season and whose best club cannot get half its
    /// following through the turnstiles, and it is the case the rest of the ladder is scaled
    /// from.
    /// </para>
    /// </summary>
    /// <param name="tier">The division, counted from one.</param>
    public static int CapacityFor(int tier) => tier switch
    {
        1 => 5_000,
        2 => 4_000,
        3 => 2_000,
        // A division below the bottom of the pyramid is a smaller league, and the grounds keep
        // getting smaller rather than stopping at a number that was only ever meant for four.
        _ => Math.Max(500, 1_200 - (tier - 4) * 300)
    };

    /// <summary>
    /// The project a club would start at a given capacity, or null when the ground is as big as
    /// this game builds.
    /// </summary>
    /// <param name="capacity">How many seats the ground has today.</param>
    public static StadiumProject? NextProjectFor(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        return TheLargestProjectThatFits(capacity, int.MaxValue);
    }

    /// <summary>
    /// The smallest stand that takes a ground of this size up by anything, or null when even
    /// the smallest one would pass the ceiling.
    /// </summary>
    /// <remarks>
    /// <see cref="NextProjectFor"/> is the largest stand that fits, and that is the right
    /// question for a manager: it hands him a real choice between a small stand and a large one.
    /// It is the wrong question for a club nobody is running, because the answer is always "as
    /// big as possible" and a crowd that wants eleven thousand seats would be handed twenty
    /// thousand — eight thousand of them empty, and every one of them paid for at the gate that
    /// was supposed to pay for the stand.
    ///
    /// <para>
    /// So a club that is being turned away builds the largest stand its crowd can fill, and a
    /// club whose crowd has outgrown the catalogue's biggest single step builds the smallest
    /// one and gets closer. That is how a ground doubles over a season rather than in one
    /// decision.
    /// </para>
    /// </remarks>
    /// <param name="capacity">How many seats the ground has today.</param>
    public static StadiumProject? SmallestStepFor(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        return Projects
            .Where(project => capacity + project.Seats <= LargestCapacity)
            .Select(project => (StadiumProject?)project)
            .OrderBy(project => project!.Value.Seats)
            .FirstOrDefault();
    }

    /// <summary>
    /// The project a club should start to reach a wanted capacity, or null when the ground is
    /// already that big or when nothing in the catalogue reaches it without passing the ceiling.
    ///
    /// <para>
    /// It is one project, not a programme, and a club wanting a large ground gets there by
    /// approving the same project again and again across a season and a few more. That is how a
    /// ground is actually built, and it is also what keeps the ceiling meaningful: from
    /// seventy-five thousand seats the ten-thousand project is refused, and the five-thousand
    /// one is offered, which lands the ladder exactly on eighty thousand rather than near it.
    /// </para>
    ///
    /// <para>
    /// The largest project that fits is chosen over the smallest that reaches, so a club asking
    /// for as much as it can get is handed a real decision rather than a run of small ones that
    /// arrive at the same number by a longer and dearer route.
    /// </para>
    /// </summary>
    /// <param name="capacity">How many seats the ground has today.</param>
    /// <param name="wanted">How many seats the club is asking for.</param>
    public static StadiumProject? ProjectToReach(int capacity, int wanted)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(wanted);

        if (wanted <= capacity) return null;

        return TheLargestProjectThatFits(capacity, wanted);
    }

    /// <summary>
    /// The biggest project that keeps a ground of this size under both the ceiling and the size
    /// the club asked for.
    /// </summary>
    private static StadiumProject? TheLargestProjectThatFits(int capacity, int ceiling)
    {
        // The nullable projection is the whole of the empty answer: a lookup on a record struct
        // returns its zero value when it finds nothing, and `StadiumProject(0, 0, 0)` handed
        // back as a project would be a club offered a free stand with no seats in it.
        return Projects
            .Where(project =>
                capacity + project.Seats <= StadiumRules.LargestCapacity &&
                capacity + project.Seats <= ceiling)
            .Select(project => (StadiumProject?)project)
            .OrderByDescending(project => project!.Value.Seats)
            .FirstOrDefault();
    }

    /// <summary>
    /// The project named by its seat count, or null when the catalogue has no such project.
    /// </summary>
    public static StadiumProject? ProjectOf(int seats) =>
        Projects
            .Where(project => project.Seats == seats)
            .Select(project => (StadiumProject?)project)
            .FirstOrDefault();

    /// <summary>
    /// The price a club should charge, as a decision of the game and not a number a screen
    /// typed.
    ///
    /// <para>
    /// It follows how full the ground is. A club whose people fit through the turnstiles has
    /// something to charge for and charges for it; a club playing in front of half its seats
    /// charges less, because the alternative is a price that prices away the second half as
    /// well. The ground is in the number for that reason — a crowd of twelve thousand is a full
    /// house in a five-thousand-seat ground and a poor afternoon in an eighty-thousand-seat one,
    /// and those are two different clubs financially even when the support is identical.
    /// </para>
    ///
    /// <para>
    /// <b>This is what makes the expansion loop balance itself.</b> A club that builds ten
    /// thousand seats and does not grow its crowd goes from full to half full, so its seat
    /// falls in price, so the gate roughly holds what it was. A club that does not build and
    /// does grow its crowd sees its occupancy rise to one and stops, which is the signal the
    /// manager is being given to spend the money. Without it, a big crowd in a small ground
    /// would raise prices for ever and no club would ever have a reason to build anything.
    /// </para>
    ///
    /// <para>
    /// The top of the band is deliberately below the dearest seat on the price list. A ground
    /// that is full is a ground that has made its ceiling, and a club that has reached it has
    /// run out of reason to charge more rather than run out of demand — charging the full thirty
    /// limos at a ground that is already full would shrink the crowd by half to prove a point
    /// about the club's standing.
    /// </para>
    /// </summary>
    /// <param name="capacity">How many seats the ground has.</param>
    /// <param name="crowd">
    /// How many people want to come, before the price is known. This is the club's own
    /// following — the crowd it plays to rather than the one a good day might produce.
    /// </param>
    public static decimal TicketPriceFor(int capacity, int crowd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(crowd);

        if (capacity <= 0 || crowd <= 0) return TicketPriceRules.ReferencePrice;

        var occupancy = Math.Clamp((double)crowd / capacity, 0.0, 1.0);

        var priced = (double)CheapestTicket + ((double)(FullHouseTicket - CheapestTicket) * occupancy);

        // Half a limos, because a manager budgets from this number and a price of 16.37 is not
        // one a gate can take on the way in.
        return (decimal)Math.Round(priced / 0.5, MidpointRounding.AwayFromZero) * 0.5m;
    }
}

/// <summary>
/// One of the three ways a club may build its ground.
/// <param name="Seats">How many seats the project adds.</param>
/// <param name="Cost">What the project costs.</param>
/// <param name="Rounds">
/// How many rounds of football the ground is a building site for, counted from the round the
/// work started after.
/// </param>
public readonly record struct StadiumProject(int Seats, decimal Cost, int Rounds);