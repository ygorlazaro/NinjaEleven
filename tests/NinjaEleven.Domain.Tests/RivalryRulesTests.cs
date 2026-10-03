using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What a rivalry is, worked out of the games two clubs have actually played.
///
/// <para>
/// These are the numbers the balance laboratory would ask for if a rivalry were a duel: given
/// these two sets of results, how much of a rivalry is that? Every one of them is a pair of
/// clubs a division genuinely contains — two 1-0s, two 4-0s, six games split three-three — and
/// the ordering between them is the whole claim being made about the word "rivalry".
/// </para>
/// </summary>
public class RivalryRulesTests
{
    private static RivalryFixture Game(string date, int scored, int conceded, bool home = true) =>
        new(DateTimeOffset.Parse(date), home, scored, conceded);

    /// <summary>
    /// The archetypal two-legged fixture — one win each, one goal either way — is a rivalry.
    /// </summary>
    [Fact]
    public void TwoNarrowGamesOneEachWayIsARivalry()
    {
        var games = new[] { Game("2027-02-01", 1, 0), Game("2027-05-01", 0, 1) };

        Assert.True(RivalryRules.Score(games) > 0.4, $"Scored {RivalryRules.Score(games):F3}.");
    }

    /// <summary>
    /// Two heavy defeats are not a rivalry. They are a mismatch.
    /// </summary>
    /// <remarks>
    /// This is the distinction the whole weighted reading exists to make, and it is the one a
    /// pure "who have we played most" rule gets wrong: the pair above and the pair below have
    /// played the identical number of games.
    /// </remarks>
    [Fact]
    public void TwoHeavyDefeatsAreAMismatchRatherThanARivalry()
    {
        var close = new[] { Game("2027-02-01", 1, 0), Game("2027-05-01", 0, 1) };
        var routs = new[] { Game("2027-02-01", 4, 0), Game("2027-05-01", 4, 0) };

        Assert.Equal(close.Length, routs.Length);
        Assert.True(
            RivalryRules.Score(close) > RivalryRules.Score(routs) * 2,
            $"{RivalryRules.Score(close):F3} against {RivalryRules.Score(routs):F3}.");
    }

    /// <summary>
    /// Six games split three wins to three defeats is the most evenly contested thing two clubs
    /// can do to each other, and it is the highest number on the scale.
    /// </summary>
    [Fact]
    public void SixGamesSplitEvenlyAreTheGreatestRivalryOnTheScale()
    {
        var even = new[]
        {
            Game("2027-01-01", 1, 0), Game("2027-02-01", 0, 1), Game("2027-03-01", 1, 0),
            Game("2027-04-01", 0, 1), Game("2027-05-01", 1, 0), Game("2027-06-01", 0, 1)
        };

        var oneSided = new[]
        {
            Game("2027-01-01", 1, 0), Game("2027-02-01", 2, 0), Game("2027-03-01", 1, 0),
            Game("2027-04-01", 3, 1), Game("2027-05-01", 2, 1), Game("2027-06-01", 0, 3)
        };

        Assert.True(
            RivalryRules.Score(even) > RivalryRules.Score(oneSided),
            $"{RivalryRules.Score(even):F3} against {RivalryRules.Score(oneSided):F3}.");

        // And it is the ceiling of the scale rather than past it, because a scale a number can
        // leave is not a scale.
        Assert.InRange(RivalryRules.Score(even), 0.6, RivalryRules.Ceiling);
    }

    /// <summary>
    /// Meeting more often makes a rivalry, holding everything else equal.
    /// </summary>
    [Fact]
    public void MeetingMoreOftenIsWorthMore()
    {
        var twice = new[] { Game("2027-02-01", 1, 0), Game("2027-05-01", 0, 1) };
        var fourTimes = new[]
        {
            Game("2027-01-01", 1, 0), Game("2027-02-01", 0, 1),
            Game("2027-03-01", 1, 0), Game("2027-04-01", 0, 1)
        };

        Assert.True(RivalryRules.Score(fourTimes) > RivalryRules.Score(twice));
    }

    /// <summary>
    /// A rivalry reads the same from either club.
    /// </summary>
    /// <remarks>
    /// The side a game was played at, and the order the games are listed in, are not allowed to
    /// reach the number. A rivalry that scored differently depending on whose screen you were
    /// looking at would not be a fact about the two clubs.
    /// </remarks>
    [Fact]
    public void ARivalryIsTheSameNumberFromEitherClub()
    {
        var fromOneSide = new[] { Game("2027-02-01", 3, 1), Game("2027-05-01", 0, 2) };

        var fromTheOther = fromOneSide
            .Select(game => game with
            {
                WasHome = !game.WasHome,
                GoalsFor = game.GoalsAgainst,
                GoalsAgainst = game.GoalsFor
            })
            .ToList();

        Assert.Equal(RivalryRules.Score(fromOneSide), RivalryRules.Score(fromTheOther), 6);
    }

    /// <summary>
    /// Frequency carries no weight, because a division's fixture list gives it none to carry.
    /// </summary>
    /// <remarks>
    /// Two clubs in the same division play each other twice a season and a club that has been
    /// promoted out of it plays them none. Frequency is therefore either a constant or an
    /// accident of which division two clubs are in, and a criterion that cannot tell a derby from
    /// a fixture list is not part of the answer.
    ///
    /// <para>
    /// So the five weights are a whole: they come to exactly one, and there is no sixth number
    /// set to zero. A weight of zero is still a number somebody chose and will one day raise, and
    /// this one is absent because the world has nothing to give it.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFiveWeightsAreAWholeAndFrequencyIsNotOneOfThem()
    {
        var weights = RivalryRules.RecurrenceWeight
            + RivalryRules.DecisivenessWeight
            + RivalryRules.ResultsWeight
            + RivalryRules.StreaksWeight
            + RivalryRules.RecentFormWeight;

        Assert.Equal(1.0, weights, 6);
        Assert.Equal(0.35, RivalryRules.RecurrenceWeight);
        Assert.Equal(0.25, RivalryRules.DecisivenessWeight);
        Assert.Equal(0.20, RivalryRules.ResultsWeight);
        Assert.Equal(0.12, RivalryRules.StreaksWeight);
        Assert.Equal(0.08, RivalryRules.RecentFormWeight);
    }

    /// <summary>
    /// Two clubs who have never met have no rivalry, and the scale says so rather than guessing.
    /// </summary>
    [Fact]
    public void TwoClubsThatHaveNeverMetHaveNoRivalry()
    {
        var games = Array.Empty<RivalryFixture>();

        Assert.Equal(0, RivalryRules.Score(games));
        Assert.Equal(0, RivalryRules.AfterASeason(0, games));
    }

    /// <summary>
    /// A season does not replace a rivalry; it can only grow one.
    /// </summary>
    /// <remarks>
    /// Two fixtures a season is two meetings. A club whose grudge was built over nine years would
    /// be handed a score off this season's two games and every March, and the club that mattered
    /// to him would be replaced by whoever he happened to meet.
    /// </remarks>
    [Fact]
    public void ASeasonGrowsARivalryAndNeverReplacesIt()
    {
        var nineYears = new[]
        {
            Game("2018-02-01", 1, 0), Game("2018-05-01", 0, 1), Game("2019-02-01", 1, 0),
            Game("2019-05-01", 0, 1), Game("2020-02-01", 1, 0), Game("2020-05-01", 0, 1),
            Game("2021-02-01", 1, 0), Game("2021-05-01", 0, 1), Game("2022-02-01", 1, 0)
        };

        var builtUp = 0.0;
        foreach (var season in NineYears(nineYears))
        {
            builtUp = RivalryRules.AfterASeason(builtUp, season);
        }

        // A single quiet season with the two clubs in different divisions changes nothing at all.
        var quiet = RivalryRules.AfterASeason(builtUp, Array.Empty<RivalryFixture>());
        Assert.Equal(builtUp, quiet);

        // And a season of nothing but goalless draws cannot bring a nine-year rivalry down
        // either. It may well raise it — two clubs neither of whom can score is the most
        // undecided thing a pair can do, and the rule is that a season never lowers the number.
        var dull = new[] { Game("2027-02-01", 0, 0), Game("2027-05-01", 0, 0) };
        var afterDull = RivalryRules.AfterASeason(builtUp, dull);

        Assert.True(afterDull >= builtUp, $"{builtUp:F3} became {afterDull:F3}.");

        Assert.True(builtUp > RivalryRules.Floor);
        Assert.True(builtUp <= RivalryRules.Ceiling);
    }

    /// <summary>Split nine years of fixtures into the nine seasons they were played in.</summary>
    private static List<RivalryFixture[]> NineYears(RivalryFixture[] games) =>
        Enumerable.Range(0, 9)
            .Select(season => games.Where(game => game.PlayedAt.Year == 2018 + season).ToArray())
            .ToList();

    /// <summary>
    /// A run of five straight wins is a streak, and the streak is what a manager remembers.
    /// </summary>
    [Fact]
    public void ARunOfFiveIsAStreakAndTwoAlternatingResultsAreNot()
    {
        var run = new[]
        {
            Game("2027-01-01", 1, 0), Game("2027-02-01", 1, 0), Game("2027-03-01", 1, 0),
            Game("2027-04-01", 1, 0), Game("2027-05-01", 1, 0), Game("2027-06-01", 0, 1)
        };

        var alternating = new[]
        {
            Game("2027-01-01", 1, 0), Game("2027-02-01", 0, 1), Game("2027-03-01", 1, 0),
            Game("2027-04-01", 0, 1), Game("2027-05-01", 1, 0), Game("2027-06-01", 0, 1)
        };

        Assert.Equal(1.0, RivalryRules.Streaks(run));
        Assert.Equal(0.0, RivalryRules.Streaks(alternating));
    }

    /// <summary>
    /// Four rivals, and a fifth is not one.
    /// </summary>
    /// <remarks>
    /// A number small enough to print. A screen cannot show fifteen rivals and mean any of them,
    /// and a cap that is not a number is a cap nobody has been given.
    /// </remarks>
    [Fact]
    public void AClubNamesFourRivalsAndNoMore()
    {
        Assert.Equal(4, RivalryRules.MaxRivals);
    }
}
