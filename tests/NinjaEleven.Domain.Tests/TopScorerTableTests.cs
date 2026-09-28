using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The order of a top scorers table, and the money that hangs on it.
///
/// The chain is a rule and it is settled in one place, so these are the tests that hold it:
/// most goals, then fewest games, then fewest cards by weight, then oldest — and a pair level
/// on all four of those is not separated at all, because there is nothing left to separate it
/// with. A rule that settles a tie with a coin, a name or the order the rows came out of the
/// database pays two equally good strikers different amounts of money for saying the same
/// thing, and the money is the part a manager notices.
/// </summary>
public class TopScorerTableTests
{
    private static ScorerStanding Line(
        int goals,
        int appearances = 20,
        int yellow = 0,
        int red = 0,
        int year = 1996) =>
        ScorerStanding.From(Guid.NewGuid(), goals, appearances, yellow, red, new DateOnly(year, 1, 15));

    [Fact]
    public void TheMostGoalsComesFirstAndNothingElseIsConsulted()
    {
        var table = TopScorerTable.Rank(new[]
        {
            Line(goals: 8, appearances: 6, yellow: 1),
            Line(goals: 20, appearances: 30, yellow: 4, red: 1)
        });

        Assert.Equal(20, table[0].Goals);
        Assert.Equal(1, table[0].Position);
        Assert.Equal(2, table[1].Position);
    }

    [Fact]
    public void LevelOnGoalsIsSettledByWhicheverManPlayedFewerGames()
    {
        var table = TopScorerTable.Rank(new[]
        {
            Line(goals: 8, appearances: 20),
            Line(goals: 8, appearances: 12)
        });

        Assert.Equal(12, table[0].Appearances);
        Assert.Equal(1, table[0].Position);
        Assert.Equal(0, table[0].TiedWith);
        Assert.Equal(2, table[1].Position);
    }

    [Fact]
    public void LevelOnGoalsAndGamesIsSettledByWhicheverManWasCaughtLeast()
    {
        // A red is worth three yellows, so a man with four yellows is behind one with a single
        // red even though the red is the uglier card: the count is about who was on the pitch
        // most, and a red is what takes a man off it.
        var withARed = Line(goals: 9, appearances: 18, red: 1);
        var withFourYellows = Line(goals: 9, appearances: 18, yellow: 4);

        var table = TopScorerTable.Rank(new[] { withFourYellows, withARed });

        Assert.Equal(3, withARed.CardPoints);
        Assert.Equal(4, withFourYellows.CardPoints);
        Assert.Equal(withARed.PlayerId, table[0].PlayerId);
        Assert.Equal(1, table[0].Position);
        Assert.Equal(2, table[1].Position);
    }

    [Fact]
    public void LevelOnEverythingButTheBirthDateIsSettledByTheOlderMan()
    {
        var table = TopScorerTable.Rank(new[]
        {
            Line(goals: 11, appearances: 15, year: 2004),
            Line(goals: 11, appearances: 15, year: 1991)
        });

        Assert.Equal(new DateOnly(1991, 1, 15), table[0].BornOn);
        Assert.Equal(1, table[0].Position);
    }

    [Fact]
    public void TwoMenLevelOnTheWholeChainAreBothFirstAndBothTakeTheFirstPrize()
    {
        var lines = new[] { Line(goals: 14, appearances: 22, yellow: 3, year: 1990), Line(goals: 14, appearances: 22, yellow: 3, year: 1990) };

        var table = TopScorerTable.Rank(lines);

        Assert.All(table, line => Assert.Equal(1, line.Position));
        Assert.All(table, line => Assert.Equal(1, line.TiedWith));
        Assert.All(table, line => Assert.Equal(1, line.PrizeSlot));
    }

    [Fact]
    public void APairLevelForSecondIsPaidTheSecondPrizeAndTheThirdPrizeIsPaidToNobody()
    {
        var table = TopScorerTable.Rank(new[]
        {
            Line(goals: 20, appearances: 22, year: 1990),
            Line(goals: 14, appearances: 22, year: 1990),
            Line(goals: 14, appearances: 22, year: 1990),
            Line(goals: 9, appearances: 22, year: 1990)
        });

        var second = table.Where(line => line.Position == 2).ToList();
        var fourth = table.Single(line => line.Position == 4);

        Assert.Equal(2, second.Count);
        Assert.All(second, line => Assert.Equal(2, line.PrizeSlot));
        Assert.Equal(0, fourth.PrizeSlot);
    }

    [Fact]
    public void AThirdStrikerIsStillPaidWhenNobodyAboveHimIsLevelWithAnyone()
    {
        var table = TopScorerTable.Rank(new[]
        {
            Line(goals: 20, appearances: 22),
            Line(goals: 15, appearances: 22),
            Line(goals: 11, appearances: 22),
            Line(goals: 8, appearances: 22)
        });

        Assert.Equal(3, table.Single(line => line.Position == 3).PrizeSlot);
        Assert.All(table.Where(line => line.Position == 4), line => Assert.Equal(0, line.PrizeSlot));
    }

    [Fact]
    public void APlayerWithNoBirthDateIsNotTreatedAsTheYoungestManInTheCountry()
    {
        var lines = new[]
        {
            ScorerStanding.From(Guid.NewGuid(), 9, 18, 0, 0, bornOn: null),
            ScorerStanding.From(Guid.NewGuid(), 9, 18, 0, 0, bornOn: null)
        };

        var table = TopScorerTable.Rank(lines);

        Assert.All(table, line => Assert.Equal(1, line.Position));
    }

    [Fact]
    public void TheArtilhariaIsPaidTenFiveAndThreePerCentOfTheChampionsOwnPrize()
    {
        const decimal purse = 30_000_000m;
        const int clubs = 12;
        var champion = PrizeRules.ChampionshipPrize(1, clubs, purse);

        Assert.Equal(0.10m, PrizeRules.TopScorerRate(1));
        Assert.Equal(0.05m, PrizeRules.TopScorerRate(2));
        Assert.Equal(0.03m, PrizeRules.TopScorerRate(3));
        Assert.Equal(decimal.Round(champion * 0.10m, 2), PrizeRules.TopScorerPrize(1, clubs, purse));
        Assert.Equal(decimal.Round(champion * 0.05m, 2), PrizeRules.TopScorerPrize(2, clubs, purse));
        Assert.Equal(decimal.Round(champion * 0.03m, 2), PrizeRules.TopScorerPrize(3, clubs, purse));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(12)]
    public void APlaceWithNoPrizeOnItIsPaidNothingRatherThanTheSmallestShare(int place) =>
        Assert.Equal(0m, PrizeRules.TopScorerPrize(place, 12, 30_000_000m));

    [Fact]
    public void TheArtilhariaIsNewMoneyAndIsNotTakenOffTheChampionsCheque()
    {
        const decimal purse = 30_000_000m;
        const int clubs = 12;

        var champion = PrizeRules.ChampionshipPrize(1, clubs, purse);
        var paidOut = Enumerable.Range(1, clubs)
            .Sum(position => PrizeRules.ChampionshipPrize(position, clubs, purse));

        // The purse is still the purse: the artilharia is money on top of it, and a division
        // that had to fund three extra prizes out of its own purse would be a division paying
        // the champion for the privilege of also having a good striker.
        Assert.Equal(purse, decimal.Round(paidOut, 2));
        Assert.True(PrizeRules.TopScorerPrize(1, clubs, purse) > 0m);
        Assert.Equal(champion, PrizeRules.ChampionshipPrize(1, clubs, purse));
    }

    [Fact]
    public void TheThirdDivisionsArtilhariaIsWorthAThirdOfWhatTheFirstDivisionsIs()
    {
        var first = PrizeRules.TopScorerPrize(1, 12, PrizeRules.PurseForTier(1));
        var third = PrizeRules.TopScorerPrize(1, 12, PrizeRules.PurseForTier(3));

        Assert.Equal(decimal.Round(first / 3m, 0), decimal.Round(third, 0));
    }
}
