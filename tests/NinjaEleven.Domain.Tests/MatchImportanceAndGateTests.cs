using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The rules that decide how much a match matters, and what a crowd is worth.
///
/// These are guards on numbers rather than on prose: a test here asks what the rule decides,
/// never what it is called.
/// </summary>
public class MatchImportanceRulesTests
{
    [Fact]
    public void A_cup_tie_is_a_tie_and_a_supercup_is_the_one_match_that_cannot_be_replayed()
    {
        Assert.Equal(
            MatchImportance.VeryRelevant,
            MatchImportanceRules.ByCompetition(Enums.CompetitionType.Cup));

        Assert.Equal(
            MatchImportance.Decisive,
            MatchImportanceRules.ByCompetition(Enums.CompetitionType.SuperCup));

        Assert.Equal(
            MatchImportance.Normal,
            MatchImportanceRules.ByCompetition(Enums.CompetitionType.League));
    }

    [Fact]
    public void A_league_match_with_nothing_at_stake_is_an_ordinary_match()
    {
        // Middle of the table, eight matchdays left, nobody near either end.
        Assert.Equal(
            MatchImportance.Normal,
            MatchImportanceRules.ForLeagueMatch(6, 8, 12, 8));
    }

    [Fact]
    public void The_last_matchday_of_a_race_is_decisive_for_both_ends_of_the_table()
    {
        Assert.Equal(
            MatchImportance.Decisive,
            MatchImportanceRules.ForLeagueMatch(1, 6, 12, 0));

        Assert.Equal(
            MatchImportance.Decisive,
            MatchImportanceRules.ForLeagueMatch(6, 12, 12, 0));
    }

    [Fact]
    public void A_race_is_alive_in_its_last_three_matchdays_and_settled_after_that()
    {
        Assert.Equal(
            MatchImportance.VeryRelevant,
            MatchImportanceRules.ForLeagueMatch(2, 6, 12, MatchImportanceRules.LivelyMatchdays));

        Assert.Equal(
            MatchImportance.Normal,
            MatchImportanceRules.ForLeagueMatch(2, 6, 12, MatchImportanceRules.LivelyMatchdays + 1));
    }

    [Fact]
    public void A_club_far_from_both_ends_is_not_in_a_race_however_late_the_season_is()
    {
        // Fifth on the last day, with the second and the eleventh both in reach of nothing.
        Assert.Equal(
            MatchImportance.Normal,
            MatchImportanceRules.ForLeagueMatch(5, 5, 12, 0));
    }

    [Fact]
    public void A_club_ninth_is_in_the_relegation_fight_on_the_last_day()
    {
        Assert.Equal(
            MatchImportance.Decisive,
            MatchImportanceRules.ForLeagueMatch(9, 5, 12, 0));
    }

    [Fact]
    public void A_division_smaller_than_the_race_puts_every_club_in_it()
    {
        // Two clubs, both inside the four that are in the title race, because there is nobody
        // outside it.
        Assert.Equal(
            MatchImportance.Decisive,
            MatchImportanceRules.ForLeagueMatch(1, 2, 2, 0));
    }
}

public class AttendanceCalculatorTests
{
    /// <summary>A ground of the size asked for. A new ground is 5,000 seats whatever is asked for.</summary>
    private static Stadium Ground(int capacity)
    {
        var ground = Stadium.Create(Guid.NewGuid(), "Clube Teste");
        ground.SetCapacity(capacity);
        return ground;
    }

    [Fact]
    public void A_ground_holds_what_it_holds_and_not_one_seat_more()
    {
        var ground = Ground(200);

        // Every factor pushed as high as it goes: the best club in the top division, at the
        // end of the season, in a decider. The demand is several times what a two-hundred seat
        // ground holds, and the ground is what says no.
        var crowd = AttendanceCalculator.Calculate(
            ground,
            new AttendanceContext(
                Tier: 1,
                HomePosition: 1,
                ClubsInDivision: 12,
                HomeSupporters: 60_000,
                HomeSquadStars: 5,
                AwaySquadStars: 5,
                DivisionAverageStars: 1,
                Matchday: 22,
                TotalMatchdays: 22,
                Importance: MatchImportance.Decisive),
            randomFactor: 1.10);

        Assert.Equal(ground.Capacity, crowd);
    }

    [Fact]
    public void An_ordinary_top_flight_club_sells_out_a_ground_a_ninth_of_its_following()
    {
        var ground = Ground(5_000);
        ground.SetTicketPrice(Stadium.DefaultTicketPrice);

        // A mid-table top-flight club, an average opponent, halfway through the season, on an
        // ordinary matchday. Nothing special about the evening — and the ground is still full,
        // because the club has forty-five thousand people and five thousand seats.
        var context = new AttendanceContext(
            Tier: 1,
            HomePosition: 6,
            ClubsInDivision: 12,
            HomeSupporters: 45_000,
            HomeSquadStars: 3,
            AwaySquadStars: 3,
            DivisionAverageStars: 3,
            Matchday: 11,
            TotalMatchdays: 22,
            Importance: MatchImportance.Normal);

        var crowd = AttendanceCalculator.Calculate(ground, context, randomFactor: 1.0);
        var demand = AttendanceCalculator.Demand(context, ground.TicketPrice, 1.0);

        Assert.Equal(ground.Capacity, crowd);
        Assert.True(demand > crowd, "The whole point: more wanted to come than could be let in.");
    }

    /// <summary>
    /// The demand is what a club's finances turn on when its ground is big enough, and hiding
    /// it inside the attendance hid the only number that argues for a new stand.
    /// </summary>
    [Fact]
    public void How_many_people_wanted_to_come_is_not_the_same_question_as_how_many_came()
    {
        var ground = Ground(5_000);

        var context = new AttendanceContext(
            Tier: 1, HomePosition: 6, ClubsInDivision: 12, HomeSupporters: 45_000,
            HomeSquadStars: 3, AwaySquadStars: 3, DivisionAverageStars: 3,
            Matchday: 11, TotalMatchdays: 22, Importance: MatchImportance.Normal);

        Assert.Equal(
            AttendanceCalculator.Calculate(ground, context, 1.0),
            Math.Min(AttendanceCalculator.Demand(context, ground.TicketPrice, 1.0), ground.Capacity));

        // And a ground with room for all of them sells all of them, so the two numbers converge
        // exactly where the club has outgrown the argument for building.
        var roomy = Ground(80_000);

        Assert.Equal(
            AttendanceCalculator.Demand(context, roomy.TicketPrice, 1.0),
            AttendanceCalculator.Calculate(roomy, context, 1.0));
    }

    /// <summary>
    /// A bigger following fills a bigger ground, which is what the old model could not say.
    /// </summary>
    [Fact]
    public void AClubWithMoreSupportDrawsMorePeople_whatever_theGroundHolds()
    {
        var roomy = Ground(80_000);

        var modest = roomyCapacityIsNotTheAnswer
            (new AttendanceContext(1, 6, 12, 5_000, 3, 3, 3, 11, 22, MatchImportance.Normal));

        var huge = roomyCapacityIsNotTheAnswer
            (new AttendanceContext(1, 6, 12, 60_000, 3, 3, 3, 11, 22, MatchImportance.Normal));

        Assert.True(huge > modest * 5, $"{huge} is not the same crowd as {modest}.");

        static int roomyCapacityIsNotTheAnswer(AttendanceContext context) =>
            AttendanceCalculator.Demand(context, Stadium.DefaultTicketPrice, 1.0);
    }

    [Fact]
    public void A_ground_at_the_opening_price_is_not_sold_out_because_the_clubs_are_weak()
    {
        // A bottom club in the bottom division, in August, against a poor draw: the demand
        // never reaches a full house, and a ground with room in it is the honest answer.
        var ground = Ground(50_000);
        ground.SetTicketPrice(Stadium.DefaultTicketPrice);

        var crowd = AttendanceCalculator.Calculate(
            ground,
            new AttendanceContext(
                Tier: 3,
                HomePosition: 12,
                ClubsInDivision: 12,
                HomeSupporters: 1_500,
                HomeSquadStars: 1,
                AwaySquadStars: 1,
                DivisionAverageStars: 1,
                Matchday: 1,
                TotalMatchdays: 22,
                Importance: MatchImportance.Normal),
            randomFactor: 1.10);

        Assert.True(crowd < ground.Capacity, "A ground is not sold out because the rules allow it to be full.");
        Assert.True(crowd > 0);
    }

    [Fact]
    public void An_empty_ground_draws_nobody_rather_than_throwing()
    {
        var crowd = AttendanceCalculator.Calculate(
            Ground(0),
            new AttendanceContext(1, 1, 12, 30_000, 3, 3, 3, 1, 22, MatchImportance.Normal),
            randomFactor: 1);

        Assert.Equal(0, crowd);
    }

    /// <summary>
    /// The division is no longer a factor in a crowd.
    ///
    /// <para>
    /// This is the inversion, stated as the test that holds it. There used to be a table here
    /// saying the first division draws more than the second, multiplied into every crowd — and
    /// multiplied into a number that already carried the division, because the crowd comes from
    /// the club's own ladder. Counting it twice made a small ground look busy in the small
    /// divisions, which is the one direction in which that table was doing harm.
    /// </para>
    /// </summary>
    [Fact]
    public void The_division_moved_out_of_the_crowd_and_into_the_clubs_own_following()
    {
        var sameMatchApartFromTheClub = new[]
        {
            new AttendanceContext(1, 6, 12, 45_000, 3, 3, 3, 11, 22, MatchImportance.Normal),
            new AttendanceContext(2, 6, 12, 45_000, 3, 3, 3, 11, 22, MatchImportance.Normal),
            new AttendanceContext(3, 6, 12, 45_000, 3, 3, 3, 11, 22, MatchImportance.Normal),
            new AttendanceContext(4, 6, 12, 45_000, 3, 3, 3, 11, 22, MatchImportance.Normal)
        };

        var crowds = sameMatchApartFromTheClub
            .Select(context => AttendanceCalculator.Demand(context, Stadium.DefaultTicketPrice, 1.0))
            .ToList();

        Assert.True(crowds.Distinct().Count() == 1, "The tier still moves a crowd.");

        // And the division is told apart by the following, which is a ladder and not a table
        // that has to be kept in step with it.
        var ladder = new[] { 1, 2, 3, 4 }
            .Select(FanBaseRules.LadderFor)
            .Select(levels => levels.Middle)
            .ToList();

        Assert.Equal(ladder.OrderByDescending(middle => middle).ToList(), ladder);
    }

    [Fact]
    public void The_same_spread_separates_the_best_from_the_worst_in_any_size_of_division()
    {
        // The spread is a property of the rule, not of a division of twelve, so a division of
        // eight and a division of twenty separate their best from their worst by the same gap.
        var twelve = AttendanceCalculator.PositionFactor(1, 12) - AttendanceCalculator.PositionFactor(12, 12);
        var eight = AttendanceCalculator.PositionFactor(1, 8) - AttendanceCalculator.PositionFactor(8, 8);
        var twenty = AttendanceCalculator.PositionFactor(1, 20) - AttendanceCalculator.PositionFactor(20, 20);

        Assert.Equal(AttendanceCalculator.PositionSpread, twelve, 10);
        Assert.Equal(twelve, eight, 10);
        Assert.Equal(twelve, twenty, 10);
    }

    [Fact]
    public void A_crowd_that_is_asked_to_come_under_a_bad_draw_and_a_heavy_price_still_comes()
    {
        // A bottom club, last in the table, against an opponent three times its own strength,
        // in the first matchday, with the band of noise at its floor. It is the smallest crowd
        // the rules allow and it is still a crowd.
        var crowd = AttendanceCalculator.Calculate(
            Ground(20_000),
            new AttendanceContext(
                Tier: 3,
                HomePosition: 12,
                ClubsInDivision: 12,
                HomeSupporters: 5_000,
                HomeSquadStars: 1,
                AwaySquadStars: 5,
                DivisionAverageStars: 1.5,
                Matchday: 1,
                TotalMatchdays: 22,
                Importance: MatchImportance.Normal),
            randomFactor: AttendanceCalculator.RandomFloor);

        Assert.True(crowd > 0, "A crowd is never nothing, however bad the draw.");
        Assert.True(crowd < 20_000);
    }

    [Fact]
    public void The_noise_band_is_clamped_so_a_crowd_cannot_swing_by_half()
    {
        var below = AttendanceCalculator.Calculate(
            Ground(10_000),
            new AttendanceContext(1, 1, 12, 30_000, 3, 3, 3, 1, 22, MatchImportance.Normal),
            randomFactor: 0.01);

        var atFloor = AttendanceCalculator.Calculate(
            Ground(10_000),
            new AttendanceContext(1, 1, 12, 30_000, 3, 3, 3, 1, 22, MatchImportance.Normal),
            randomFactor: AttendanceCalculator.RandomFloor);

        Assert.Equal(atFloor, below);
    }
}

public class GateReceiptTests
{
    private static GateSplit Championship => GateSplit.For(CompetitionType.League);

    private static GateSplit Knockout => GateSplit.For(CompetitionType.Cup);

    [Fact]
    public void A_championship_gate_is_two_thirds_to_the_host_and_one_third_to_the_traveller()
    {
        var gate = GateReceipt.For(6_000, 10m, Championship);

        Assert.Equal(60_000m, gate.GrossRevenue);
        Assert.Equal(40_000m, gate.HomeRevenue);
        Assert.Equal(20_000m, gate.AwayRevenue);
    }

    [Fact]
    public void A_cup_gate_is_halved_between_the_two_clubs()
    {
        // A knockout is two clubs meeting once, and the club that happens to be drawn at home
        // has not earned two thirds of a cup night it was only lent the ground for.
        var gate = GateReceipt.For(6_000, 10m, Knockout);

        Assert.Equal(60_000m, gate.GrossRevenue);
        Assert.Equal(30_000m, gate.HomeRevenue);
        Assert.Equal(30_000m, gate.AwayRevenue);
    }

    [Fact]
    public void The_supercup_is_a_knockout_and_pays_the_same_as_a_cup_tie()
    {
        Assert.Equal(
            GateSplit.For(CompetitionType.Cup),
            GateSplit.For(CompetitionType.SuperCup));
    }

    [Fact]
    public void A_share_that_does_not_divide_is_still_added_up_to_the_whole()
    {
        // 1 limo, 3,333 seats: a third of 33,330 limos is not a number of limos.
        var gate = GateReceipt.For(3_333, 10m, Championship);

        Assert.Equal(gate.GrossRevenue, gate.HomeRevenue + gate.AwayRevenue);
        Assert.Equal(33_330m, gate.GrossRevenue);
    }

    [Fact]
    public void An_empty_ground_earns_nobody_anything()
    {
        var gate = GateReceipt.For(0, 10m, Championship);

        Assert.Equal(0m, gate.GrossRevenue);
        Assert.Equal(0m, gate.HomeRevenue);
        Assert.Equal(0m, gate.AwayRevenue);
    }

    [Fact]
    public void A_negative_crowd_is_refused_rather_than_earning_money()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GateReceipt.For(-1, 10m, Championship));
    }
}

/// <summary>
/// The two numbers a ground is judged by, and the one rule about how they came to be.
/// </summary>
public class GatePressureTests
{
    private static Stadium Ground(int capacity)
    {
        var ground = Stadium.Create(Guid.NewGuid(), "Clube Teste");
        ground.SetCapacity(capacity);
        return ground;
    }

    private static AttendanceContext AFullHouse(int supporters) => new(
        Tier: 1,
        HomePosition: 1,
        ClubsInDivision: 16,
        HomeSupporters: supporters,
        HomeSquadStars: 5,
        AwaySquadStars: 5,
        DivisionAverageStars: 3,
        Matchday: 30,
        TotalMatchdays: 30,
        Importance: MatchImportance.Normal);

    /// <summary>
    /// A match keeps what the crowd wanted as well as what the ground would take.
    ///
    /// <para>
    /// This is the whole of the expansion system. Attendance is capped by the capacity, so a
    /// sold-out ground of five thousand reads exactly like a comfortably large one, and a club
    /// being turned away every week is indistinguishable from a club whose ground is the right
    /// size — unless the number the ceiling was applied to is kept beside it.
    /// </para>
    ///
    /// <para>
    /// So the match records both, and the rule is that they are about <em>one evening</em>: the
    /// same seed, the same draw, the same night. A demand recomputed later from the club's
    /// following has to invent the table position and the matchday the match was played in, and
    /// an invented number printed as the engine's is the one thing a manager must never be shown.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(200, 60_000)]
    [InlineData(5_000, 45_000)]
    [InlineData(5_000, 12_000)]
    [InlineData(80_000, 5_000)]
    public void A_match_records_what_the_crowd_wanted_and_what_the_ground_took(
        int capacity,
        int supporters)
    {
        var ground = Ground(capacity);
        var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CompetitionType.League);

        match.KickOff(seed: 20_260_103, ground, AFullHouse(supporters));

        Assert.NotNull(match.Demand);
        Assert.True(match.Attendance <= capacity, $"{match.Attendance} into a ground of {capacity}.");

        // Attendance is the demand with the wall in front of it, and never above it.
        Assert.True(
            match.Attendance <= match.Demand,
            $"{match.Attendance} came through and {match.Demand} wanted in.");

        // And when the ground was not the constraint, the two are the same number — a club
        // nobody is being turned away from has no pressure to relieve.
        if (match.Demand <= capacity)
        {
            Assert.Equal(match.Demand, match.Attendance);
        }
    }

    /// <summary>
    /// A ground that turns people away is a ground with something to say about it.
    /// </summary>
    [Fact]
    public void AGroundThatTurnsPeopleAwaySaysSo()
    {
        var ground = Ground(2_000);
        var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CompetitionType.League);

        match.KickOff(seed: 20_260_103, ground, AFullHouse(60_000));

        Assert.Equal(2_000, match.Attendance);
        Assert.True(match.Demand > 2_000, $"Only {match.Demand} wanted in.");
    }

    /// <summary>
    /// The same evening twice is the same two numbers twice.
    /// </summary>
    [Fact]
    public void AReplayedMatchFillsTheSameSeatsAndWantsTheSamePeople()
    {
        var ground = Ground(5_000);
        var first = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CompetitionType.League);
        var second = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CompetitionType.League);

        first.KickOff(seed: 7, ground, AFullHouse(45_000));
        second.KickOff(seed: 7, ground, AFullHouse(45_000));

        Assert.Equal(first.Attendance, second.Attendance);
        Assert.Equal(first.Demand, second.Demand);
    }
}
