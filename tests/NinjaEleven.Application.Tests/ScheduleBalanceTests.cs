using NinjaEleven.Application.Leagues;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A season of fourteen games per club, seven at home and seven away, is a promise the
/// schedule has to keep on its own: the circle method only balances the two legs if the
/// fixed club alternates sides.
/// </summary>
/// <remarks>
/// And a promise about the <i>order</i> as well: the draw turns each club's home games
/// over from one round to the next, so nobody plays fifteen home games in a row. Both
/// properties are measured here rather than assumed, because a draw that only balances
/// the two legs is exactly how a season ends up with a club at home every week in August.
/// </remarks>
public class ScheduleBalanceTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void Every_club_plays_the_same_number_of_home_and_away_games(int clubCount)
    {
        var clubs = BuildTeams(clubCount);
        var rounds = RoundRobin.Build(clubs);

        foreach (var club in clubs)
        {
            var home = rounds.Sum(round => round.Count(pair => pair.Home == club));
            var away = rounds.Sum(round => round.Count(pair => pair.Away == club));

            Assert.Equal(clubCount - 1, home);
            Assert.Equal(clubCount - 1, away);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void Every_pair_meets_once_at_home_and_once_away(int clubCount)
    {
        var clubs = BuildTeams(clubCount);
        var rounds = RoundRobin.Build(clubs);
        var fixtures = rounds.SelectMany(round => round).ToList();

        foreach (var left in clubs)
        {
            foreach (var right in clubs.Where(club => club != left))
            {
                Assert.Equal(1, fixtures.Count(pair => pair.Home == left && pair.Away == right));
                Assert.Equal(1, fixtures.Count(pair => pair.Home == right && pair.Away == left));
            }
        }
    }

    [Fact]
    public void Eight_clubs_play_four_games_in_each_of_fourteen_rounds()
    {
        var rounds = RoundRobin.Build(BuildTeams(8));

        Assert.Equal(14, rounds.Count);
        Assert.All(rounds, round => Assert.Equal(4, round.Count));
        Assert.Equal(56, rounds.Sum(round => round.Count));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void A_round_never_shows_a_club_twice(int clubCount)
    {
        var rounds = RoundRobin.Build(BuildTeams(clubCount));

        Assert.All(rounds, round =>
        {
            var playing = round.Select(pair => pair.Home).Concat(round.Select(pair => pair.Away)).ToList();

            Assert.Equal(playing.Count, playing.Distinct().Count());
            Assert.True(playing.Count <= clubCount);
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(18)]
    public void No_club_plays_more_than_three_games_in_a_row_on_the_same_side(int clubCount)
    {
        var clubs = BuildTeams(clubCount);
        var rounds = RoundRobin.Build(clubs);

        foreach (var club in clubs)
        {
            var sides = SidesOf(club, rounds);
            var longest = LongestStreak(sides);

            Assert.True(
                longest <= RoundRobin.MaxConsecutiveSameSide,
                $"{club.Name} jogou {longest} partidas seguidas com o mesmo mando: {string.Join(string.Empty, sides)}");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(16)]
    public void A_club_repeats_its_side_at_most_three_times_in_a_season(int clubCount)
    {
        // Perfect alternation is unattainable (see RoundRobin.MaxConsecutiveSameSide), and
        // this is the floor the draw reaches: at most two clubs alternate all season and
        // every other one repeats its side three times. This is the guard on the rule that
        // does the work — a draw that is merely "not fifteen in a row" is not good enough.
        var clubs = BuildTeams(clubCount);
        var rounds = RoundRobin.Build(clubs);

        foreach (var club in clubs)
        {
            var sides = SidesOf(club, rounds);
            var repeats = sides.Zip(sides.Skip(1), (was, now) => was == now).Count(same => same);

            Assert.True(
                repeats <= 3,
                $"{club.Name} repetiu o mando {repeats} vezes: {string.Join(string.Empty, sides)}");
        }
    }

    [Fact]
    public void The_same_clubs_always_produce_the_same_schedule()
    {
        var clubs = BuildTeams(16);
        var first = RoundRobin.Build(clubs);
        var second = RoundRobin.Build(clubs);

        Assert.Equal(
            first.SelectMany(round => round).Select(pair => (pair.Home.Id, pair.Away.Id)),
            second.SelectMany(round => round).Select(pair => (pair.Home.Id, pair.Away.Id)));
    }

    [Fact]
    public void A_sixteen_club_division_holds_two_streaks_of_three_and_no_more()
    {
        // The whole thing as one number for the size the pyramid actually uses: in a whole
        // division, sixteen clubs and thirty rounds, the draw produces two occasions on
        // which a club plays three times in a row on the same side, and no more.
        var clubs = BuildTeams(16);
        var rounds = RoundRobin.Build(clubs);

        var streaks = clubs.Sum(club => StreaksOfThree(SidesOf(club, rounds)));

        Assert.Equal(2, streaks);
    }

    private static string[] SidesOf(Team club, IReadOnlyList<IReadOnlyList<(Team Home, Team Away)>> rounds) =>
        rounds.Select(round => round.Any(pair => pair.Home == club) ? "H" : "A").ToArray();

    private static int LongestStreak(IReadOnlyList<string> sides)
    {
        var longest = 0;
        var run = 0;

        for (var index = 0; index < sides.Count; index++)
        {
            run = index > 0 && sides[index] == sides[index - 1] ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static int StreaksOfThree(IReadOnlyList<string> sides)
    {
        var streaks = 0;

        for (var index = 2; index < sides.Count; index++)
        {
            if (sides[index] == sides[index - 1] && sides[index] == sides[index - 2])
            {
                streaks++;
            }
        }

        return streaks;
    }

    private static List<Team> BuildTeams(int count) =>
        Enumerable.Range(1, count)
            .Select(index => Team.Create($"Clube {index}", $"C{index}", "#112233", "#445566", 70))
            .ToList();
}
