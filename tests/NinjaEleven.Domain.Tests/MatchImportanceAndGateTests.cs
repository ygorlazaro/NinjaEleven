using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Competitions;
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
    public void An_ordinary_top_flight_match_draws_about_half_the_ground()
    {
        var ground = Ground(50_000);
        ground.SetTicketPrice(Stadium.DefaultTicketPrice);

        // A mid-table top-flight club, an average opponent, halfway through the season, on an
        // ordinary matchday. This is the crowd a ground is actually sized for, and it is worth
        // knowing that the rules put it in a believable place rather than at a full house.
        var crowd = AttendanceCalculator.Calculate(
            ground,
            new AttendanceContext(
                Tier: 1,
                HomePosition: 6,
                ClubsInDivision: 12,
                HomeSquadStars: 3,
                AwaySquadStars: 3,
                DivisionAverageStars: 3,
                Matchday: 11,
                TotalMatchdays: 22,
                Importance: MatchImportance.Normal),
            randomFactor: 1.0);

        var share = (double)crowd / ground.Capacity;

        Assert.InRange(share, 0.30, 0.70);
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
            new AttendanceContext(1, 1, 12, 3, 3, 3, 1, 22, MatchImportance.Normal),
            randomFactor: 1);

        Assert.Equal(0, crowd);
    }

    [Fact]
    public void The_top_division_draws_more_than_the_one_below_it()
    {
        var top = AttendanceCalculator.DivisionFactor(1);
        var second = AttendanceCalculator.DivisionFactor(2);
        var third = AttendanceCalculator.DivisionFactor(3);

        Assert.True(top > second, "A 1ª Divisão should draw more than a 2ª.");
        Assert.True(second > third, "A 2ª Divisão should draw more than a 3ª.");
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
            new AttendanceContext(1, 1, 12, 3, 3, 3, 1, 22, MatchImportance.Normal),
            randomFactor: 0.01);

        var atFloor = AttendanceCalculator.Calculate(
            Ground(10_000),
            new AttendanceContext(1, 1, 12, 3, 3, 3, 1, 22, MatchImportance.Normal),
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
