using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A ground is a decision, and a decision takes time, money and a season of football.
///
/// <para>
/// These are the rules that make an expansion a plan rather than a wish. Every constant in
/// <see cref="StadiumRules"/> is a balance decision, and a balance decision nobody checks is a
/// number that survives only as long as the thing it was balanced against does.
/// </para>
/// </summary>
public class StadiumRulesTests
{
    /// <summary>
    /// The catalogue is three projects and the numbers are the ones the game agreed on.
    ///
    /// <para>
    /// Read straight out of the file rather than derived, because these are the numbers a
    /// manager budgets a club's future against. A test that recomputed them from the cost per
    /// seat would pass on every retune and would notice nothing at all.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCatalogueIsTheThreeProjectsTheGameAgreedOn()
    {
        Assert.Equal(3, StadiumRules.Projects.Length);

        Assert.Equal(
            new StadiumProject(2_000, 900_000m, 1),
            StadiumRules.ProjectOf(2_000));

        Assert.Equal(
            new StadiumProject(5_000, 2_200_000m, 2),
            StadiumRules.ProjectOf(5_000));

        Assert.Equal(
            new StadiumProject(10_000, 4_600_000m, 4),
            StadiumRules.ProjectOf(10_000));
    }

    /// <summary>
    /// Every project buys more seats than the one before it, for more money and more time.
    ///
    /// <para>
    /// The assertion is deliberately about the three numbers together and not about the cost per
    /// seat, because the cost per seat is not monotone in the catalogue: four hundred and fifty,
    /// then four hundred and forty, then four hundred and sixty. Those are the agreed prices,
    /// so the shape a club actually plans against is the one held here — more seats, more
    /// money, more months — and a comment claiming a cheaper-per-seat rule would be a rule
    /// this file does not implement.
    /// </para>
    /// </summary>
    [Fact]
    public void ABiggerProjectBuysMoreSeatsForMoreMoneyAndMoreTime()
    {
        for (var index = 1; index < StadiumRules.Projects.Length; index++)
        {
            var smaller = StadiumRules.Projects[index - 1];
            var bigger = StadiumRules.Projects[index];

            Assert.True(bigger.Seats > smaller.Seats);
            Assert.True(bigger.Cost > smaller.Cost);
            Assert.True(bigger.Rounds > smaller.Rounds);
        }
    }

    /// <summary>
    /// A bigger project takes longer, and it takes proportionally longer.
    ///
    /// A club that can afford the small one can therefore afford the big one too, given a few
    /// more months — which is what makes expansion a plan a manager can schedule rather than a
    /// bet on one round going well.
    /// </summary>
    [Fact]
    public void ABiggerProjectTakesProportionallyLonger()
    {
        var roundPerThousandSeats = StadiumRules.Projects
            .Select(project => (double)project.Rounds / (project.Seats / 1000))
            .ToList();

        Assert.Equal(0.5, roundPerThousandSeats[0], 6);
        Assert.Equal(0.4, roundPerThousandSeats[1], 6);
        Assert.Equal(0.4, roundPerThousandSeats[2], 6);

        Assert.All(StadiumRules.Projects, project => Assert.True(project.Rounds >= 1));
    }

    /// <summary>
    /// No build ever goes past the largest ground the game can build.
    ///
    /// <para>
    /// The check has to be about the whole ladder and not only the last project: from a ground
    /// of seventy-five thousand the ten-thousand project would produce eighty-five thousand, and
    /// a ceiling that only the third step obeyed would not be a ceiling.
    /// </para>
    /// </summary>
    [Fact]
    public void NoProjectBuildsPastTheLargestGround()
    {
        for (var capacity = 0; capacity <= StadiumRules.LargestCapacity; capacity += 500)
        {
            var project = StadiumRules.NextProjectFor(capacity);

            if (project is null) continue;

            Assert.True(
                capacity + project.Value.Seats <= StadiumRules.LargestCapacity,
                $"A ground of {capacity} could be built to {capacity + project.Value.Seats}.");
        }
    }

    /// <summary>
    /// A club asking for as much as it can get is handed the largest project the catalogue has,
    /// rather than a run of small ones.
    ///
    /// <para>
    /// Two small builds would reach the same number by a different route and cost more and take
    /// longer, and the reason they are not allowed is that the answer to "make it as big as it
    /// goes" should be one decision rather than a sequence of them.
    /// </para>
    /// </summary>
    [Fact]
    public void AClubAskingForTheLargestGroundItCanGetIsHandedTheBiggestProject()
    {
        var project = StadiumRules.ProjectToReach(5_000, StadiumRules.LargestCapacity);

        Assert.NotNull(project);
        Assert.Equal(10_000, project!.Value.Seats);
    }

    /// <summary>
    /// A ground reaches the ceiling exactly, and it gets there by approving the same project
    /// again and again rather than by a rule invented to reach it in one go.
    ///
    /// <para>
    /// The interesting step is the last one. From seventy-five thousand the ten-thousand project
    /// would produce eighty-five thousand and is refused, so the five-thousand one is offered
    /// instead and the ladder lands on eighty thousand and stops. A catalogue that let the big
    /// project overshoot would have a club with more than eighty thousand seats, which is the
    /// one number in this file that was given a ceiling.
    /// </para>
    /// </summary>
    [Fact]
    public void AGroundReachesTheCeilingExactlyByBuildingAgainAndAgain()
    {
        var capacity = Stadium.DefaultCapacity;

        for (var build = 0; build < 40 && capacity < StadiumRules.LargestCapacity; build++)
        {
            var project = StadiumRules.ProjectToReach(capacity, StadiumRules.LargestCapacity);

            Assert.NotNull(project);

            capacity += project!.Value.Seats;
        }

        Assert.Equal(StadiumRules.LargestCapacity, capacity);
        Assert.Null(StadiumRules.ProjectToReach(capacity, StadiumRules.LargestCapacity));
    }

    /// <summary>
    /// A club asking for more than the catalogue can build is handed the biggest project there
    /// is, because "the largest stand you can have" is a decision a manager can act on and
    /// "thirty-nine thousand is not a number this game has" is not.
    /// </summary>
    [Fact]
    public void AClubAskingForSomethingImpossibleIsHandedTheBiggestProjectThereIs()
    {
        var project = StadiumRules.ProjectToReach(5_000, 39_000);

        Assert.NotNull(project);
        Assert.Equal(10_000, project!.Value.Seats);
    }

    /// <summary>
    /// Asking for what the ground already has is not a request to build.
    /// </summary>
    [Theory]
    [InlineData(5_000, 5_000)]
    [InlineData(5_000, 1_000)]
    [InlineData(5_000, 0)]
    public void AClubAskingForWhatItHasIsNotGivenAProject(int capacity, int wanted)
    {
        Assert.Null(StadiumRules.ProjectToReach(capacity, wanted));
    }

    /// <summary>
    /// A ground of the largest size this game builds has nothing left to build, which is what
    /// stops the largest project from being offered to a club that already has everything.
    /// </summary>
    [Fact]
    public void AGroundAsBigAsThisGameBuildsHasNothingLeftToBuild()
    {
        Assert.Null(StadiumRules.NextProjectFor(StadiumRules.LargestCapacity));
        Assert.Null(StadiumRules.NextProjectFor(StadiumRules.LargestCapacity - 1));
        Assert.NotNull(StadiumRules.NextProjectFor(StadiumRules.LargestCapacity - 10_000));
    }

    /// <summary>
    /// A club of thirty-nine thousand people is priced above a club of nine thousand, and both
    /// are priced from the game's own demand curve rather than from a number a screen typed.
    /// </summary>
    [Fact]
    public void ABiggerCrowdIsChargedMoreForItsSeat()
    {
        var small = StadiumRules.TicketPriceFor(20_000, 9_000);
        var big = StadiumRules.TicketPriceFor(60_000, 45_000);

        Assert.True(
            big > small,
            $"A crowd of 45.000 was charged {big} and a crowd of 9.000 was charged {small}.");
    }

    /// <summary>
    /// The price is one the game has: a curve anchor or a half-limos step between two of them.
    ///
    /// <para>
    /// This is what a manager budgets from. A price of 13.37 would be the exact top of the
    /// revenue curve and would be a number no gate could take, and a club that had to round it
    /// itself would round it differently from the club next door.
    /// </para>
    /// </summary>
    [Fact]
    public void ASeatIsPricedAtAPriceAGateCanActuallyTake()
    {
        var anchors = TicketPriceRules.Anchors
            .Select(anchor => anchor.Price)
            .ToHashSet();

        foreach (var crowd in new[] { 1_000, 4_500, 9_000, 15_000, 30_000, 45_000, 60_000, 120_000 })
        {
            var price = StadiumRules.TicketPriceFor(5_000, crowd);

            Assert.True(
                price >= StadiumRules.CheapestTicket && price <= StadiumRules.DearestTicket,
                $"A crowd of {crowd} was priced at {price}, which is off the list.");

            var onTheList = anchors.Contains(price) || (price * 2) % 1m == 0m;

            Assert.True(onTheList, $"A crowd of {crowd} was priced at {price}, which is not on the list.");
        }
    }

    /// <summary>
    /// A crowd nobody has, or a ground with no seats in it, is priced at the reference rather
    /// than at the bottom of the list — because there is nothing to price and the honest answer
    /// is the price the game normalised its curve to.
    /// </summary>
    [Theory]
    [InlineData(5_000, 0)]
    [InlineData(0, 9_000)]
    [InlineData(0, 0)]
    public void AGroundOrACrowdOfNothingIsPricedAtTheReference(int capacity, int crowd)
    {
        Assert.Equal(TicketPriceRules.ReferencePrice, StadiumRules.TicketPriceFor(capacity, crowd));
    }

    /// <summary>
    /// A club that builds seats it does not fill charges less for them, which is what makes the
    /// expansion loop settle instead of running away.
    ///
    /// <para>
    /// This is the property the whole rule exists for. Without it, building is free money: the
    /// crowd is the same, the capacity is bigger, and the gate takes exactly what it took before
    /// — so a manager would build on every ground in the country on the first day of a season
    /// and the ceiling would be the only thing stopping him. With it, a ground that has doubled
    /// and a crowd that has not is a ground playing in front of empty seats, and the seat costs
    /// less, and the manager sees the mistake on the club's own page.
    /// </para>
    /// </summary>
    [Fact]
    public void BuildingSeatsAClubCannotFillCostsItTheGate()
    {
        var crowd = 30_000;

        var packed = StadiumRules.TicketPriceFor(30_000, crowd);
        var stretched = StadiumRules.TicketPriceFor(60_000, crowd);

        Assert.True(
            stretched < packed,
            $"The same crowd paid {packed} for a full ground and {stretched} for a half-empty one.");

        // And the band is the whole of it: a packed ground is at the top of the standing price
        // and an empty one at the bottom.
        Assert.Equal(StadiumRules.FullHouseTicket, packed);
        Assert.Equal(StadiumRules.CheapestTicket, StadiumRules.TicketPriceFor(60_000, 1));
    }

    /// <summary>
    /// A ground that is being built on holds three quarters of a crowd.
    ///
    /// <para>
    /// The factor takes a quarter off the demand and leaves the capacity alone, which is the
    /// whole of what a closure is: the club still owns five thousand seats and cannot sell the
    /// ones that are behind the scaffolding. A full house during a rebuild is therefore 3.750
    /// people and not 6.250, and a test that only checked the ratio would not notice which of
    /// the two numbers had moved.
    /// </para>
    /// </summary>
    [Fact]
    public void AGroundUnderConstructionHoldsThreeQuartersOfTheCrowd()
    {
        var context = AnOrdinaryMatch();

        var whole = AttendanceCalculator.Calculate(AGroundOf(20_000), context, 1.0);
        var building = AttendanceCalculator.Calculate(
            AGroundOf(20_000), context with { WorksUnderway = true }, 1.0);

        Assert.Equal(0.75, AttendanceCalculator.WorksFactor(true), 6);
        Assert.Equal(1.0, AttendanceCalculator.WorksFactor(false), 6);

        // Three quarters of the crowd, and never more seats than the ground has.
        Assert.InRange(building, 0, 20_000);
        Assert.True(building <= whole);

        if (whole > 0)
        {
            Assert.Equal((int)Math.Round(whole * 0.75), building);
        }
    }

    private static Stadium AGroundOf(int capacity)
    {
        var stadium = Stadium.Create(Guid.NewGuid(), "Clube");
        stadium.SetCapacity(capacity);

        return stadium;
    }

    private static AttendanceContext AnOrdinaryMatch() => new(
        Tier: 1,
        HomePosition: 1,
        ClubsInDivision: 16,
        HomeSupporters: 100_000,
        HomeSquadStars: 3.0,
        AwaySquadStars: 3.0,
        DivisionAverageStars: 3.0,
        Matchday: 20,
        TotalMatchdays: 30,
        Importance: MatchImportance.Relevant);
}

/// <summary>
/// A project takes rounds of football and not days, so its progress is a fact about the
/// calendar rather than a timestamp.
///
/// <para>
/// The two mistakes this covers are both invisible until the world has moved on: a project that
/// finishes on the day it was paid for, and a project that adds its seats twice. Neither is a
/// wrong number on a row — both are a ground that grew without anybody building anything.
/// </para>
/// </summary>
public class StadiumConstructionTests
{
    private static StadiumConstruction AProjectOf(int seats, int startedAfterRound, int round) =>
        StadiumConstruction.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            StadiumRules.ProjectOf(seats)!.Value,
            startedAfterRound,
            DateTimeOffset.UnixEpoch);

    [Fact]
    public void AProjectIsNotFinishedOnTheDayItIsPaidFor()
    {
        var construction = AProjectOf(2_000, startedAfterRound: 10, round: 0);

        // Approved after round ten, so it is finished after round eleven — and certainly not
        // after round ten.
        Assert.Equal(11, construction.FinishesAfterRound);
        Assert.False(construction.IsDue(10));
        Assert.True(construction.IsDue(11));
        Assert.Equal(1, construction.RoundsRemaining(10));
        Assert.Equal(0, construction.RoundsRemaining(11));
    }

    [Fact]
    public void ALongerProjectTakesLongerToArrive()
    {
        var quick = AProjectOf(2_000, startedAfterRound: 0, round: 0);
        var slow = AProjectOf(10_000, startedAfterRound: 0, round: 0);

        Assert.True(slow.FinishesAfterRound > quick.FinishesAfterRound);
        Assert.True(slow.IsDue(4));
        Assert.False(slow.IsDue(3));
    }

    /// <summary>
    /// A ground's work is finished exactly once, and the second caller is told so.
    ///
    /// <para>
    /// The world closes its windows more than once, so the settlement that adds the seats runs
    /// again for a window a manager or another process already finished. A close that answered
    /// nothing would be the one that adds them twice.
    /// </para>
    /// </summary>
    [Fact]
    public void AProjectIsFinishedOnceAndTheSecondCallerIsTold()
    {
        var construction = AProjectOf(5_000, startedAfterRound: 0, round: 0);

        Assert.False(construction.IsFinished);
        Assert.True(construction.Complete(DateTimeOffset.UnixEpoch.AddDays(2)));
        Assert.True(construction.IsFinished);

        Assert.False(construction.Complete(DateTimeOffset.UnixEpoch.AddDays(9)));
        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddDays(2),
            construction.CompletedAt);
    }

    /// <summary>
    /// The project keeps the catalogue's own price and its own time, even though only its seats
    /// are stored — so a rule retuned between two seasons reaches both clubs equally rather
    /// than leaving sixty-four rows disagreeing with the file.
    /// </summary>
    [Fact]
    public void AProjectReadsItsPriceAndItsTimeFromTheCatalogue()
    {
        foreach (var project in StadiumRules.Projects)
        {
            var construction = StadiumConstruction.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                project,
                startedAfterRound: 0,
                DateTimeOffset.UnixEpoch);

            Assert.Equal(project.Cost, construction.Cost);
            Assert.Equal(project.Rounds, construction.Rounds);
            Assert.Equal(project.Seats, construction.Seats);
        }
    }

    /// <summary>
    /// A club may only build something the catalogue has.
    /// </summary>
    [Fact]
    public void AClubMayNotBuildAProjectTheCatalogueDoesNotHave()
    {
        var invented = new StadiumProject(Seats: 7_000, Cost: 3_000_000m, Rounds: 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => StadiumConstruction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            invented, startedAfterRound: 0, DateTimeOffset.UnixEpoch));
    }
}

/// <summary>
/// How big a ground is, and what a crowd does to it.
///
/// <para>
/// These are the measurements the model was built for, held as assertions. A ladder of
/// supports and a ladder of grounds are two separate tables that have to agree with each other
/// or the bottom of the pyramid gets a first-division stadium and a gate of three hundred
/// limos — and neither table on its own would notice.
/// </para>
/// </summary>
public class GroundSizeTests
{
    /// <summary>
    /// A ground belongs to a club, so it is sized to the division the club is in.
    ///
    /// <para>
    /// One size for all sixty-four was never neutral. It handed a fourth-division club a
    /// first-division ground, which filled to a tenth of itself and left the bottom of the
    /// pyramid economically dead.
    /// </para>
    /// </summary>
    [Fact]
    public void AGroundIsSizedToItsDivisionAndTheLadderGoesDownThePyramid()
    {
        var capacities = Enumerable.Range(1, 5)
            .Select(StadiumRules.CapacityFor)
            .ToList();

        for (var index = 1; index < capacities.Count; index++)
        {
            Assert.True(
                capacities[index] < capacities[index - 1],
                $"Tier {index + 1} has {capacities[index]} seats and tier {index} has {capacities[index - 1]}.");
        }

        // And the first division keeps five thousand, because that is the ground whose best
        /// following cannot get through the turnstiles.
        Assert.Equal(5_000, StadiumRules.CapacityFor(1));
    }

    /// <summary>
    /// Every division has a club that nearly fills its own ground.
    ///
    /// <para>
    /// This is the pressure the pyramid is supposed to be under everywhere and not only at the
    /// top. It is "nearly fills" rather than "overflows" because only the first division
    /// actually overflows at the sizes the ladder gives them: a fourth-division club of seven
    /// thousand gets to about four fifths of its twelve-hundred seats, which is a ground sized
    /// right and a ground worth arguing about. A division where even its best club sat in a
    /// half-empty ground would be a division with nothing to spend its money on.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryDivisionHasAClubThatNearlyFillsItsOwnGround()
    {
        foreach (var tier in CompetitionRules.Tiers())
        {
            var capacity = StadiumRules.CapacityFor(tier);
            var bestClub = FanBaseRules.SeedFor(tier, 1.0);

            var occupancy = SeasonAverage(bestClub, tier, capacity) / capacity;

            Assert.True(
                occupancy >= 0.80,
                $"The best club in tier {tier} filled {occupancy:P0} of a ground of {capacity:N0}.");
        }
    }

    /// <summary>
    /// And every division's ordinary club has a ground worth filling.
    ///
    /// <para>
    /// The other half of the same ladder. A ground that a median club cannot fill is a ground
    /// the club was not built for, and the answer to that is a smaller ground rather than a
    /// smaller club — which is what sizing the ladder is for.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryDivisionsOrdinaryClubHasAGroundWorthFilling()
    {
        foreach (var tier in CompetitionRules.Tiers())
        {
            var capacity = StadiumRules.CapacityFor(tier);
            var medianClub = FanBaseRules.SeedFor(tier, 0.5);

            // A season's average, which is what "worth filling" has to mean: one good afternoon
            // is not a ground.
            var average = SeasonAverage(medianClub, tier, capacity);

            Assert.True(
                average > capacity / 3.0,
                $"The average club in tier {tier} drew {average:N0} into a ground of {capacity:N0}.");
        }
    }

    /// <summary>
    /// The first division is the case the whole model was built for: a ground that is full and
    /// a following that will not fit in it.
    ///
    /// <para>
    /// Note what the number cannot be. Attendance is people in seats, so it is never above a
    /// hundred per cent of capacity — and a model that reported it as such would be reporting a
    /// ground that does not exist. What is above capacity is the <em>demand</em>, and that is
    /// the number a club's ground is judged on: the ground says no, the demand says how often.
    /// </para>
    /// </summary>
    [Fact]
    public void AFirstDivisionClubFillsItsGroundAndTurnsPeopleAway()
    {
        var capacity = StadiumRules.CapacityFor(1);
        var medianClub = FanBaseRules.SeedFor(tier: 1, strengthPercentile: 0.5);

        var average = SeasonAverage(medianClub, 1, capacity);
        var wanted = SeasonAverage(medianClub, 1, int.MaxValue / 4);

        // Every seat of it, on a season average — not a perfect hundred per cent, because a
        // club that finishes fifteenth in March does not sell out. What has to be true is that
        // the ground is the binding constraint all season, not on the good days only.
        Assert.InRange(average / capacity, 0.95, 1.00);

        Assert.True(
            wanted > average * 1.10,
            $"A median first-division club wanted {wanted:N0} and sold {average:N0}.");

        // The share of the turnstile queue, which is the argument for ten thousand seats.
        var turnedAway = (wanted - average) / wanted;
        Assert.InRange(turnedAway, 0.10, 0.35);
    }

    /// <summary>How many people want to come over a whole season, for a given ground.</summary>
    private static double SeasonAverage(int supporters, int tier, int capacity)
    {
        double total = 0;
        var matches = 0;

        for (var position = 1; position <= CompetitionRules.ClubsPerDivision; position++)
        for (var matchday = 1; matchday <= CompetitionRules.LeagueMatchDays; matchday++)
        {
            total += Math.Min(Demand(supporters, AnOrdinarySeasonIn(tier) with
            {
                HomePosition = position,
                Matchday = matchday
            }), capacity);

            matches++;
        }

        return total / matches;
    }

    /// <summary>
    /// One match of an ordinary season: a mid-table position is settled per call, an average
    /// opponent, a normal match, and the reference price so the price is not being measured
    /// twice.
    /// </summary>
    private static AttendanceContext AnOrdinarySeasonIn(int tier) => new(
        Tier: tier,
        HomePosition: 8,
        ClubsInDivision: CompetitionRules.ClubsPerDivision,
        HomeSupporters: 0,
        HomeSquadStars: 3.0,
        AwaySquadStars: 3.0,
        DivisionAverageStars: 3.0,
        Matchday: CompetitionRules.LeagueMatchDays / 2,
        TotalMatchdays: CompetitionRules.LeagueMatchDays,
        Importance: MatchImportance.Normal);

    /// <summary>
    /// The uncapped demand, which is the number the ground is judged on.
    /// </summary>
    private static double Demand(int supporters, AttendanceContext context) =>
        AttendanceCalculator.Demand(
            context with { HomeSupporters = supporters },
            Stadium.DefaultTicketPrice,
            randomFactor: 1.0);
}