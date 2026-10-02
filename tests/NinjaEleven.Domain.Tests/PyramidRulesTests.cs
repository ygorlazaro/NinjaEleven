using NinjaEleven.Domain.Competitions;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The chain a table is sorted by, read as data.
///
/// The sort walks <see cref="StandingTable.Chain"/> and a "regras" screen prints the same list,
/// so the two are one list by construction. What these tests hold is the claim that the list is
/// the whole truth: every criterion the engine can compare is declared, none is declared twice,
/// and the order the list says is the order the table comes out in.
/// </summary>
public class StandingChainTests
{
    [Fact]
    public void Every_criterion_the_engine_can_compare_is_declared_exactly_once()
    {
        // A sort criterion the chain does not name is a rule a manager cannot be told about, and
        // a chain entry the sort never reaches is a rule a screen would print and the game would
        // not keep. The enum is the exhaustive list of what the engine can compare, so comparing
        // it against the chain closes both gaps at once.
        var declared = StandingTable.Chain.Select(criterion => criterion.Key).ToList();

        Assert.Equal(
            Enum.GetValues<StandingCriterionKey>().OrderBy(key => key),
            declared.OrderBy(key => key));
    }

    [Fact]
    public void The_table_is_asked_points_goal_difference_and_goals_scored_and_nothing_else()
    {
        // These three are what forms the groups the head-to-head is run on, and they are the
        // only criteria that are. Everything below them is a tiebreaker between clubs the three
        // could not part, and a chain that let a card or a squad's strength form a group would be
        // asking "who is still tied?" with the wrong question.
        var whole = StandingTable.Chain
            .Where(criterion => criterion.Scope == StandingCriterionScope.Table)
            .Select(criterion => criterion.Key)
            .ToList();

        Assert.Equal(
            [
                StandingCriterionKey.Points,
                StandingCriterionKey.GoalDifference,
                StandingCriterionKey.GoalsFor
            ],
            whole);
    }

    [Fact]
    public void Points_settle_a_table_before_anything_else_is_asked()
    {
        // The club that won its only game is first whatever else it has, and the club that won
        // nothing loses on the one criterion that comes before the twenty goals it scored.
        var table = StandingTable.Sort(
            [Row(1, goalsFor: 20), Row(2, wins: 1, goalsFor: 1)],
            []);

        Assert.Equal(Team(2), table.First().TeamId);
    }

    [Fact]
    public void Goal_difference_settles_what_points_cannot()
    {
        var table = StandingTable.Sort(
            [Row(1, wins: 1, goalsFor: 1, goalsAgainst: 9), Row(2, wins: 1, goalsFor: 4, goalsAgainst: 1)],
            []);

        Assert.Equal(Team(2), table.First().TeamId);
    }

    [Fact]
    public void Goals_scored_settle_what_points_and_goal_difference_cannot()
    {
        var table = StandingTable.Sort(
            [Row(1, wins: 2, goalsFor: 3, goalsAgainst: 3), Row(2, wins: 2, goalsFor: 5, goalsAgainst: 5)],
            []);

        Assert.Equal(Team(2), table.First().TeamId);
    }

    [Fact]
    public void A_head_to_head_is_run_between_the_tied_clubs_and_not_with_the_rest_of_the_division()
    {
        // Three clubs level on points, goal difference and goals scored, and only the matches
        // between the three of them can part them. Everything each of them played against anybody
        // else has already been counted in the three numbers above, and counting it a second
        // time inside the tiebreaker is a criterion that can only ever confirm the tie.
        var table = ThreeClubsLevelOnEverything();

        // Inside the group: the first won one and drew one, the second lost one and won one, the
        // third lost one and drew one — four points, three, one. The confrontation decides, and
        // it is the confrontation between the three and not each club's run against the rest of
        // the division, which is level and settles nothing.
        Assert.Equal([Team(1), Team(2), Team(3)], table.Take(3).Select(entry => entry.TeamId));
    }

    [Fact]
    public void A_club_level_on_everything_is_still_ordered_by_something()
    {
        // The morning of matchday one: every line is zero, and a table ordered by nothing at all
        // is a table in whatever order the database happened to return. The strength of the
        // squad is the one thing known about two clubs level on everything else, so it decides.
        var table = StandingTable.Sort([Row(1, stars: 2), Row(2, stars: 4), Row(3, stars: 3)], []);

        Assert.Equal([Team(2), Team(3), Team(1)], table.Select(entry => entry.TeamId));
    }

    [Fact]
    public void A_table_is_a_table_on_the_first_matchday_and_nobody_is_dropped_from_it()
    {
        // Every club is a participant whether it has played or not, so a division that has drawn
        // its fixtures is already a table with a position for each of them.
        var table = StandingTable.Build(
            [new ValueTuple<Guid, double>(Team(1), 3), new ValueTuple<Guid, double>(Team(2), 1)],
            []);

        Assert.Equal([Team(1), Team(2)], table.Select(entry => entry.TeamId));
        Assert.All(table, entry => Assert.Equal(0, entry.Played));
    }

    /// <summary>
    /// A division of nine clubs in which the first three finish the season level on all three of
    /// the numbers the whole table is asked, and level on the cards too, so the only thing left
    /// that can part them is the confrontation between the three of them.
    /// </summary>
    private static IReadOnlyList<StandingEntry> ThreeClubsLevelOnEverything()
    {
        // Among themselves the three play a cycle and a draw: first beats second, second beats
        // third, and first and third draw. Then each of them is given two results against the
        // other six, tuned so all three end on four points, two goals for and three against — a
        // tie the head-to-head exists to settle and which nothing else can.
        var results = new[]
        {
            Fixture(1, 2, 1, 0), Fixture(2, 3, 1, 0), Fixture(1, 3, 1, 1),
            Fixture(1, 9, 0, 1), Fixture(1, 8, 0, 1),
            Fixture(2, 7, 1, 1), Fixture(2, 6, 0, 1),
            Fixture(3, 5, 1, 0), Fixture(3, 4, 0, 1)
        };

        var table = StandingTable.Build(
            Enumerable.Range(1, 9).Select(id => (Team(id), Stars: 0d)).ToList(),
            results);

        var tied = table.Where(entry => entry.Points == 4).ToList();
        Assert.Equal(3, tied.Count);
        Assert.All(tied, entry => Assert.Equal(2, entry.GoalsFor));
        Assert.All(tied, entry => Assert.Equal(-1, entry.GoalDifference));
        Assert.All(tied, entry => Assert.Equal(0, entry.YellowCards));
        Assert.All(tied, entry => Assert.Equal(0, entry.RedCards));

        return table;
    }

    private static Guid Team(int id) => new(id, 0, 0, new byte[8]);

    private static StandingEntry Row(
        int id,
        int wins = 0,
        int draws = 0,
        int goalsFor = 0,
        int goalsAgainst = 0,
        double stars = 0) => new()
        {
            TeamId = Team(id),
            Wins = wins,
            Draws = draws,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
            Stars = stars
        };

    private static MatchResultRow Fixture(int home, int away, int homeGoals, int awayGoals) =>
        new(Team(home), Team(away), homeGoals, awayGoals);
}

/// <summary>
/// The pyramid's rules, read as data for a screen.
///
/// The bands are worked out by running the season's own movement over a table of the right size,
/// so what these hold is the one thing a formula would get wrong: the first division has nowhere
/// to promote to and the last has nowhere to be relegated from, and a "regras" page that
/// promised either would be promising a season the game does not play.
/// </summary>
public class PyramidRulesTests
{
    private const int Clubs = CompetitionRules.ClubsPerDivision;

    [Fact]
    public void The_pyramid_says_how_many_divisions_it_has_and_how_many_clubs_are_in_each()
    {
        Assert.Equal(CompetitionRules.DivisionCount, PyramidRules.DivisionCount);
        Assert.Equal(Clubs, PyramidRules.ClubsPerDivision);
        Assert.Equal(CompetitionRules.DivisionCount, PyramidRules.Divisions().Count);
        Assert.All(PyramidRules.Divisions(), division => Assert.Equal(Clubs, division.Clubs));
    }

    [Fact]
    public void The_top_division_has_a_title_and_nothing_to_promote_to()
    {
        // There is no division above the first, so the band that would be an access is simply not
        // there. A "regras" page that showed one would be promising the champion of the country
        // a season above it that does not exist.
        var first = PyramidRules.Describe(1);

        Assert.DoesNotContain(first.Bands, band => band.Kind == DivisionBandKind.Promotion);
        Assert.Contains(first.Bands, band => band.Kind == DivisionBandKind.Relegation);
        Assert.Contains(first.Bands, band => band.Kind == DivisionBandKind.Title);
    }

    [Fact]
    public void The_last_division_relegates_nobody_because_there_is_nothing_below_it()
    {
        var lowest = PyramidRules.Describe(CompetitionRules.Tiers().Count);

        Assert.DoesNotContain(lowest.Bands, band => band.Kind == DivisionBandKind.Relegation);
        Assert.Contains(lowest.Bands, band => band.Kind == DivisionBandKind.Promotion);
    }

    [Fact]
    public void Every_band_a_division_shows_is_the_move_the_season_ends_with()
    {
        // The bands are read from `DivisionMovement.From` rather than from a formula, and this is
        // what holds that: whatever the page paints on a row, the close of the season does to a
        // club in that row. A band that disagreed with the movement would be a manager budgeting
        // his season against a table the game does not finish.
        foreach (var division in PyramidRules.Divisions())
        {
            var movement = MovementOf(division.Tier);

            var promotion = division.Bands.SingleOrDefault(band => band.Kind == DivisionBandKind.Promotion);
            var relegation = division.Bands.SingleOrDefault(band => band.Kind == DivisionBandKind.Relegation);

            Assert.Equal(movement.Promoted.Count, WidthOf(promotion));
            Assert.Equal(movement.Relegated.Count, WidthOf(relegation));

            foreach (var club in movement.Movements)
            {
                if (club.IsPromoted)
                    Assert.Equal(DivisionBandKind.Promotion, BandAt(division, club.Position).Kind);
                else if (club.IsRelegated)
                    Assert.Equal(DivisionBandKind.Relegation, BandAt(division, club.Position).Kind);
                else if (club.Position > 1)
                    Assert.Equal(DivisionBandKind.Safe, BandAt(division, club.Position).Kind);
            }
        }
    }

    [Fact]
    public void A_division_says_which_division_the_clubs_in_each_band_start_the_next_season_in()
    {
        foreach (var division in PyramidRules.Divisions())
        {
            var promotion = division.Bands.SingleOrDefault(band => band.Kind == DivisionBandKind.Promotion);
            var relegation = division.Bands.SingleOrDefault(band => band.Kind == DivisionBandKind.Relegation);

            if (promotion is not null)
            {
                Assert.Equal(division.Tier - 1, promotion.ToTier);
                Assert.Equal(CompetitionRules.DivisionName(division.Tier - 1), promotion.ToDivisionName);
            }

            if (relegation is not null)
            {
                Assert.Equal(division.Tier + 1, relegation.ToTier);
                Assert.Equal(CompetitionRules.DivisionName(division.Tier + 1), relegation.ToDivisionName);
            }
        }
    }

    [Fact]
    public void Every_division_has_a_champion_and_the_top_one_says_what_its_title_adds()
    {
        // The champion of the third division is a champion: a trophy, the largest share of the
        // third division's purse and a place in the division above. A band that showed the title
        // only at the top of the pyramid would be saying the other three have none.
        foreach (var division in PyramidRules.Divisions())
        {
            var title = Assert.Single(division.Bands, band => band.Kind == DivisionBandKind.Title);

            Assert.Equal(1, title.FromPosition);
            Assert.Equal(1, title.ToPosition);
        }

        Assert.Contains("Supercopa", TitleOf(1));
        Assert.DoesNotContain("Supercopa", TitleOf(2));
    }

    [Fact]
    public void The_chain_a_screen_prints_is_the_chain_the_table_is_sorted_by()
    {
        // The list the "regras" page draws and the list the sort walks are the same object, so
        // there is nothing to keep in step: a tiebreaker added to the game is on the page the
        // next time the page is drawn, and one taken out of the game takes its sentence with it.
        Assert.Same(StandingTable.Chain, PyramidRules.TieBreakers);
    }

    private static string TitleOf(int tier) =>
        PyramidRules.Describe(tier).Bands.Single(band => band.Kind == DivisionBandKind.Title).Meaning;

    /// <summary>
    /// What the season's own movement does to a full table of a division, worked out by the same
    /// call the close of the season makes.
    /// </summary>
    private static DivisionMovement MovementOf(int tier) =>
        DivisionMovement.From(
            tier,
            Enumerable.Range(1, Clubs)
                .Select(position => new StandingEntry { TeamId = Team(tier, position) })
                .ToList());

    private static Guid Team(int tier, int position) => new(tier, (short)position, 0, new byte[8]);

    private static int WidthOf(DivisionBand? band) =>
        band is null ? 0 : band.ToPosition - band.FromPosition + 1;

    private static DivisionBand BandAt(DivisionRule division, int position)
    {
        var band = division.Bands.SingleOrDefault(candidate => candidate.Kind != DivisionBandKind.Title
            && position >= candidate.FromPosition
            && position <= candidate.ToPosition);

        // First place is never in this band: a club that finished first either went up, went
        // down or is the champion, and all three are said on their own line.
        Assert.NotNull(band);

        return band!;
    }
}