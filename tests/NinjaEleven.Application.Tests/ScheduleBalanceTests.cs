using NinjaEleven.Application.Leagues;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A season of fourteen games per club, seven at home and seven away, is a promise the
/// schedule has to keep on its own: the circle method only balances the two legs if the
/// fixed club alternates sides.
/// </summary>
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

    private static List<Team> BuildTeams(int count) =>
        Enumerable.Range(1, count)
            .Select(index => Team.Create($"Clube {index}", $"C{index}", "#112233", "#445566", 70))
            .ToList();
}
