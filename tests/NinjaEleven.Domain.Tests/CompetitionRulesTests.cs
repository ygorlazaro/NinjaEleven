using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The shape of the world: three divisions of twelve, a cup of thirty-two over five tie-rounds
/// of two legs, and a season of twenty-two matchdays with the final on the last of them.
///
/// The numbers are asserted through the rules that use them rather than by reading the
/// constants, because a constant that is only ever checked against itself is a constant nobody
/// checked.
/// </summary>
public class CompetitionRulesTests
{
    [Fact]
    public void The_pyramid_is_three_divisions_of_twelve_and_thirty_six_clubs()
    {
        Assert.Equal(3, CompetitionRules.DivisionCount);
        Assert.Equal(12, CompetitionRules.ClubsPerDivision);
        Assert.Equal(36, CompetitionRules.TotalClubs);
    }

    [Fact]
    public void Four_clubs_go_up_and_four_come_down_so_the_pyramid_stays_the_same_size()
    {
        // The reason the two numbers are equal is not a coincidence: a division that sent up
        // more than it took down would shrink, and one that sent down more would empty out.
        Assert.Equal(CompetitionRules.PromotionSlots, CompetitionRules.RelegationSlots);
    }

    [Fact]
    public void A_division_of_twelve_is_worth_twenty_two_matchdays()
    {
        Assert.Equal(11, CompetitionRules.LeagueRoundsPerLeg);
        Assert.Equal(22, CompetitionRules.LeagueMatchDays);
    }

    [Fact]
    public void The_cup_finishes_on_the_last_matchday_of_the_season()
    {
        var days = CompetitionRules.CupMatchDays();

        Assert.Equal(CompetitionRules.CupRounds, days.Count);
        Assert.Equal(CompetitionRules.LeagueMatchDays, days[^1]);
    }

    [Fact]
    public void The_cups_rounds_are_strictly_increasing_and_stay_inside_the_season()
    {
        var days = CompetitionRules.CupMatchDays();

        for (var index = 1; index < days.Count; index++)
        {
            Assert.True(days[index] > days[index - 1],
                $"Tie-round {index + 1} is not after tie-round {index}.");
        }

        Assert.All(days, day => Assert.InRange(day, 1, CompetitionRules.LeagueMatchDays));
    }

    [Fact]
    public void A_tie_is_a_week_long_because_its_two_legs_are_a_week_apart()
    {
        var (first, second) = CompetitionRules.CupLegMatchDays(1);

        Assert.Equal(second - 1, first);
        Assert.Equal(CompetitionRules.DaysBetweenMatchDays, (second - first) * 7);
    }

    [Fact]
    public void The_final_is_the_last_two_matchdays_of_the_season()
    {
        var (first, second) = CompetitionRules.CupLegMatchDays(CompetitionRules.CupRounds);

        Assert.Equal(CompetitionRules.LeagueMatchDays, second);
        Assert.Equal(CompetitionRules.LeagueMatchDays - 1, first);
    }

    [Fact]
    public void No_tie_is_ever_drawn_before_the_season_has_started()
    {
        for (var round = 1; round <= CompetitionRules.CupRounds; round++)
        {
            var (first, second) = CompetitionRules.CupLegMatchDays(round);

            Assert.InRange(first, 1, CompetitionRules.LeagueMatchDays);
            Assert.InRange(second, 1, CompetitionRules.LeagueMatchDays);
        }
    }

    [Fact]
    public void A_window_is_numbered_in_the_order_it_is_played()
    {
        // The Supercup takes window zero so that a championship window is window one whether
        // or not anything played before it on the same day.
        Assert.Equal(0, CompetitionRules.SuperCupWindow);
        Assert.True(CompetitionRules.SuperCupWindow < CompetitionRules.ChampionshipWindow);
        Assert.True(CompetitionRules.ChampionshipWindow < CompetitionRules.CupWindow);
        Assert.Equal(3, CompetitionRules.WindowsPerMatchDay);
    }

    [Fact]
    public void A_matchday_plays_the_supercup_then_the_championship_then_the_cup()
    {
        // The order is the rule: every division's round plays in the same wave, and the cup
        // follows the day. A cup leg played before the championship of the same day would be
        // a leg taken by a side that had not yet run its legs that week.
        Assert.Equal(
            new[] { CompetitionType.SuperCup, CompetitionType.League, CompetitionType.Cup },
            CompetitionRules.MatchdayWaves);

        Assert.Equal(0, CompetitionRules.WaveOf(CompetitionType.SuperCup));
        Assert.Equal(1, CompetitionRules.WaveOf(CompetitionType.League));
        Assert.Equal(2, CompetitionRules.WaveOf(CompetitionType.Cup));

        // The window a competition is played in and the wave it is played in are the same
        // fact told twice, and a screen that read one of them may read the other.
        foreach (var type in CompetitionRules.MatchdayWaves)
        {
            Assert.Equal(CompetitionRules.WaveOf(type), CompetitionRules.WindowOf(type));
        }
    }

    [Fact]
    public void The_supercup_is_the_first_match_of_the_new_season()
    {
        // The cup's first tie-round is not until the fifth matchday at the earliest, so
        // matchday one is free, and the Supercup takes window zero: it is played before the
        // championship of the same day, because it is the first football of the season.
        var (firstCupLeg, _) = CompetitionRules.CupLegMatchDays(1);

        Assert.Equal(1, CompetitionRules.SuperCupMatchDay);
        Assert.Equal(0, CompetitionRules.SuperCupWindow);
        Assert.True(CompetitionRules.SuperCupMatchDay < firstCupLeg);
    }

    [Fact]
    public void A_round_that_is_not_a_tie_round_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CompetitionRules.CupLegMatchDays(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CompetitionRules.CupLegMatchDays(CompetitionRules.CupRounds + 1));
    }

    [Fact]
    public void The_top_division_has_a_champion_and_no_promotion_race()
    {
        // There is no division above the first, so nobody in it is going up: first place is the
        // title, second to fourth are simply safe, and the four that go down are the bottom four
        // as they are in every division. A first division painted as a promotion race would be
        // promising four clubs a place that does not exist.
        const int count = CompetitionRules.ClubsPerDivision;

        Assert.Equal(TableZone.Champion, CompetitionRules.ZoneFor(1, 1, count));
        Assert.Equal(TableZone.Safe, CompetitionRules.ZoneFor(1, 2, count));
        Assert.Equal(TableZone.Safe, CompetitionRules.ZoneFor(1, CompetitionRules.PromotionSlots, count));
        Assert.Equal(TableZone.Relegation, CompetitionRules.ZoneFor(1, count - CompetitionRules.RelegationSlots + 1, count));
        Assert.Equal(TableZone.Relegation, CompetitionRules.ZoneFor(1, count, count));
    }

    [Fact]
    public void A_middle_division_is_a_race_in_both_directions()
    {
        // Tier 2 is the only place in the pyramid with a division above and a division below, so
        // it is the only table where four clubs are going up and four are going down at once.
        const int count = CompetitionRules.ClubsPerDivision;

        Assert.Equal(TableZone.Promotion, CompetitionRules.ZoneFor(2, 1, count));
        Assert.Equal(TableZone.Promotion, CompetitionRules.ZoneFor(2, CompetitionRules.PromotionSlots, count));
        Assert.Equal(TableZone.Safe, CompetitionRules.ZoneFor(2, CompetitionRules.PromotionSlots + 1, count));
        Assert.Equal(TableZone.Relegation, CompetitionRules.ZoneFor(2, count - CompetitionRules.RelegationSlots + 1, count));
        Assert.Equal(TableZone.Relegation, CompetitionRules.ZoneFor(2, count, count));
    }

    [Fact]
    public void The_last_division_relegates_nobody_because_there_is_nothing_below_it()
    {
        // The cup is thirty-two of the pyramid's thirty-six clubs, ranked by tier and then by
        // position, so the four that finish last in the last division are the four the bracket
        // has no room for. That is the band, said in the game's words: a fourth division that
        // does not exist is not somewhere these clubs can be sent.
        const int count = CompetitionRules.ClubsPerDivision;
        var lowest = CompetitionRules.Tiers().Count;

        Assert.Equal(TableZone.Promotion, CompetitionRules.ZoneFor(lowest, 1, count));
        Assert.Equal(TableZone.Safe, CompetitionRules.ZoneFor(lowest, count - CompetitionRules.RelegationSlots - 1, count));
        Assert.Equal(TableZone.CupExclusion, CompetitionRules.ZoneFor(lowest, count - CompetitionRules.RelegationSlots + 1, count));
        Assert.Equal(TableZone.CupExclusion, CompetitionRules.ZoneFor(lowest, count, count));
    }

    [Fact]
    public void A_band_is_the_move_the_season_ends_with()
    {
        // The band on a row and the movement at the end of the season are read from the same
        // numbers, so a club painted as going down cannot finish the season staying where it is.
        const int count = CompetitionRules.ClubsPerDivision;

        foreach (var tier in CompetitionRules.Tiers())
        {
            var standings = Enumerable.Range(1, count)
                .Select(position => new StandingEntry { TeamId = Guid.NewGuid() })
                .ToList();

            var movement = DivisionMovement.From(tier, standings);

            foreach (var club in movement.Movements)
            {
                var zone = CompetitionRules.ZoneFor(tier, club.Position, count);

                Assert.Equal(club.IsPromoted, zone is TableZone.Promotion);
                Assert.Equal(club.IsRelegated, zone is TableZone.Relegation);
            }
        }
    }

    [Fact]
    public void A_cup_has_no_promotion_or_relegation_bands_because_it_has_no_tier()
    {
        // A knockout is addressed by no tier, so the table must say the bands are not there
        // rather than guessing them — a cup table that pretended to promote is a table that
        // lied about the rules of its own competition.
        Assert.Equal(TableZone.None, CompetitionRules.ZoneFor(null, 1, 12));
        Assert.Equal(TableZone.None, CompetitionRules.ZoneFor(null, 12, 12));
    }
}

public class SeasonIdentityTests
{
    [Fact]
    public void A_season_is_named_by_its_number_and_never_by_hand()
    {
        var first = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var ninth = Season.Create(9, new DateOnly(2034, 1, 1), new DateOnly(2034, 12, 31));

        Assert.Equal("Temporada I", first.Name);
        Assert.Equal("Temporada IX", ninth.Name);
    }

    [Fact]
    public void The_season_after_this_one_is_the_same_length_of_calendar()
    {
        var season = Season.Create(3, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30));
        var next = season.Next();

        Assert.Equal(4, next.Number);
        Assert.Equal(season.EndDate.DayNumber - season.StartDate.DayNumber + 1,
            next.EndDate.DayNumber - next.StartDate.DayNumber + 1);
        Assert.Equal(season.EndDate.AddDays(1), next.StartDate);
    }

    [Fact]
    public void There_is_no_season_before_the_first_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Season.Create(0, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void A_season_that_never_started_cannot_be_finished()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Throws<InvalidOperationException>(season.Finish);

        season.Start();
        season.Finish();

        Assert.Equal(SeasonStatus.Finished, season.Status);
    }

    [Fact]
    public void A_finished_season_cannot_start_again()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        season.Finish();

        Assert.Throws<InvalidOperationException>(season.Start);
    }
}

public class StandingTableTests
{
    private static (Guid, double) Seed(int index) => (Guid.NewGuid(), 3 + (index % 3));

    [Fact]
    public void Every_club_is_in_the_table_before_a_single_ball_is_played()
    {
        var seeds = Enumerable.Range(1, 12).Select(Seed).ToList();

        var table = StandingTable.Build(seeds, Array.Empty<MatchResultRow>());

        Assert.Equal(12, table.Count);
        Assert.All(table, entry => Assert.Equal(0, entry.Played));
        Assert.Equal(Enumerable.Range(1, 12), table.Select(entry => entry.Position));
    }

    [Fact]
    public void A_played_match_moves_both_clubs_and_nobody_else()
    {
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        var seeds = new List<(Guid, double)> { (home, 3), (away, 3) };

        var table = StandingTable.Build(seeds, new[] { new MatchResultRow(home, away, 2, 1) });

        var homeLine = table.Single(entry => entry.TeamId == home);
        var awayLine = table.Single(entry => entry.TeamId == away);

        Assert.Equal(3, homeLine.Points);
        Assert.Equal(0, awayLine.Points);
        Assert.Equal(1, homeLine.Wins);
        Assert.Equal(1, awayLine.Losses);
        Assert.All(table, entry => Assert.Equal(1, entry.Played));
    }

    [Fact]
    public void A_draw_gives_a_point_each_and_nobody_the_wins_column()
    {
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();

        var table = StandingTable.Build(
            new List<(Guid, double)> { (home, 3), (away, 3) },
            new[] { new MatchResultRow(home, away, 1, 1) });

        Assert.All(table, entry =>
        {
            Assert.Equal(1, entry.Points);
            Assert.Equal(1, entry.Draws);
            Assert.Equal(0, entry.Wins);
            Assert.Equal(0, entry.Losses);
        });
    }

    [Fact]
    public void A_table_that_nothing_has_been_played_in_is_ordered_by_the_squads()
    {
        // Every line of every club is zero, so points, goal difference, goals, head-to-head
        // and cards all say the same thing: nothing. The one thing that is actually known about
        // two clubs level on all of it is how strong their squads are, and a table that left
        // the order to the database would be a table whose first row was the strongest club or
        // whichever id sorted lowest.
        var strong = Guid.NewGuid();
        var middling = Guid.NewGuid();
        var weak = Guid.NewGuid();

        var table = StandingTable.Build(
            new List<(Guid, double)> { (weak, 2.0), (strong, 4.5), (middling, 3.0) },
            Array.Empty<MatchResultRow>());

        Assert.Equal(strong, table[0].TeamId);
        Assert.Equal(middling, table[1].TeamId);
        Assert.Equal(weak, table[2].TeamId);
    }

    [Fact]
    public void A_result_still_beats_the_squad_when_there_is_one_to_beat_it_with()
    {
        // The weaker squad wins, and the table says so: strength is the last resort, not the
        // first one.
        var strong = Guid.NewGuid();
        var weak = Guid.NewGuid();

        var table = StandingTable.Build(
            new List<(Guid, double)> { (strong, 4.5), (weak, 2.0) },
            new[] { new MatchResultRow(weak, strong, 1, 0) });

        Assert.Equal(weak, table[0].TeamId);
    }

    [Fact]
    public void A_club_cannot_be_credited_with_a_match_it_did_not_play_in()
    {
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        var idle = Guid.NewGuid();

        var table = StandingTable.Build(
            new List<(Guid, double)> { (home, 3), (away, 3), (idle, 3) },
            new[] { new MatchResultRow(home, away, 5, 0) });

        var idleLine = table.Single(entry => entry.TeamId == idle);
        Assert.Equal(0, idleLine.Played);
        Assert.Equal(0, idleLine.GoalsAgainst);
    }

    [Fact]
    public void A_game_being_played_counts_when_it_is_asked_for_and_not_before()
    {
        // The live table is the same table with one more result in it. There is no second set
        // of rules here, which is why both tables are built by this one method.
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        var seeds = new List<(Guid, double)> { (away, 3), (home, 3) };

        var official = StandingTable.Build(seeds, Array.Empty<MatchResultRow>());
        var live = StandingTable.Build(seeds, new[] { new MatchResultRow(home, away, 1, 0) });

        // Both clubs are level before the whistle, so both tables put the stronger squad first
        // — and the result is what moves the away club to the top of the live one.
        Assert.Equal(away, official[0].TeamId);
        Assert.Equal(home, live[0].TeamId);
    }

    [Fact]
    public void Cards_are_counted_against_the_club_that_was_booked()
    {
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();

        var table = StandingTable.Build(
            new List<(Guid, double)> { (home, 3), (away, 3) },
            new[] { new MatchResultRow(home, away, 1, 0, HomeRedCards: 1, AwayYellowCards: 2) });

        var homeLine = table.Single(entry => entry.TeamId == home);
        var awayLine = table.Single(entry => entry.TeamId == away);

        Assert.Equal(1, homeLine.RedCards);
        Assert.Equal(2, awayLine.YellowCards);
    }
}
