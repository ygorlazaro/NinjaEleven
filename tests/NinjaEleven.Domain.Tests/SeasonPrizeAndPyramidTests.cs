using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The money a season pays, and the pyramid it rearranges.
/// </summary>
public class SeasonPrizeAndPyramidTests
{
    [Fact]
    public void AChampionOfTwelveTakesTheGreatestShareAndARelegatedClubIsStillPaid()
    {
        const decimal purse = 30_000_000m;
        const int clubs = 12;

        var champion = PrizeRules.ChampionshipPrize(1, clubs, purse);
        var second = PrizeRules.ChampionshipPrize(2, clubs, purse);
        var eleventh = PrizeRules.ChampionshipPrize(11, clubs, purse);
        var twelfth = PrizeRules.ChampionshipPrize(clubs, clubs, purse);

        Assert.True(champion > second, "The champion takes more than the runner-up.");
        Assert.True(second > eleventh);
        Assert.True(eleventh > twelfth);

        // The twelfth place is a relegated club and it is paid like anybody else: it played
        // twenty-two matchdays. A prize that stopped at the relegation line is a prize for
        // staying, which is a different competition.
        Assert.True(twelfth > 0m);

        // And the whole purse is paid out, to the decimal. A division's money that adds up to
        // less than the division's purse is money that went missing between the rules and the
        // book.
        var total = Enumerable.Range(1, clubs)
            .Sum(position => PrizeRules.ChampionshipPrize(position, clubs, purse));

        Assert.Equal(purse, decimal.Round(total, 2));
    }

    [Fact]
    public void ADivisionOfEightSplitsTheSamePurseWithoutLosingAPennyToRounding()
    {
        const decimal purse = 10_000_000m;
        const int clubs = 8;

        var total = Enumerable.Range(1, clubs)
            .Sum(position => PrizeRules.ChampionshipPrize(position, clubs, purse));

        Assert.Equal(purse, decimal.Round(total, 2));
    }

    [Theory]
    [InlineData(1, 10_000)]
    [InlineData(2, 200_000)]
    [InlineData(3, 500_000)]
    [InlineData(4, 1_000_000)]
    [InlineData(5, 3_000_000)]
    [InlineData(6, 0)]
    [InlineData(0, 0)]
    public void TheCupPaysWhatItSaidItPaysAndNothingBeyondTheFinal(int tieRound, decimal expected)
    {
        Assert.Equal(expected, PrizeRules.CupConsolation(tieRound));
    }

    [Fact]
    public void TheFinalPaysTheRunnerUpMoreThanTheChampionOfANewerRoundWould()
    {
        Assert.True(PrizeRules.CupFinalLoser > PrizeRules.CupConsolation(4));
        Assert.Equal(5_000_000m, PrizeRules.CupChampionPrize);
    }

    [Fact]
    public void AClubInADivisionIsPaidInTheBooksItBelongsTo()
    {
        Assert.Equal(30_000_000m, PrizeRules.PurseForTier(1));
        Assert.Equal(20_000_000m, PrizeRules.PurseForTier(2));
        Assert.Equal(10_000_000m, PrizeRules.PurseForTier(3));

        // A tier past the bottom is the bottom purse rather than a crash: the pyramid is a
        // closed set, and a caller that names a tier nobody has is asking about a division at
        // the bottom of the country, not about a division that does not exist.
        Assert.Equal(10_000_000m, PrizeRules.PurseForTier(9));
    }

    [Fact]
    public void ADivisionOfTwelveFinishesTheSeasonWithTwelveClubsInIt()
    {
        var table = StandingsFor(12, reversed: true);

        var movement = DivisionMovement.From(1, table);

        Assert.Equal(12, movement.Movements.Count);
        Assert.Empty(movement.Promoted);
        Assert.Equal(CompetitionRules.RelegationSlots, movement.Relegated.Count);
        // Eight stay and four go down, and the four that go down are the bottom four, so the
        // table's order decides who is relegated rather than a rule that reads the list.
        Assert.Equal(8, movement.Movements.Count(m => m.ToTier == 1));
        Assert.All(
            movement.Relegated.Select(teamId => movement.Movements.Single(m => m.TeamId == teamId)),
            relegated => Assert.True(relegated.Position > 8));
    }

    [Fact]
    public void TheChampionOfASecondDivisionGoesUpEvenThoughTheDivisionHasNoChampionItCanKeep()
    {
        var table = StandingsFor(12, reversed: true);

        var movement = DivisionMovement.From(2, table);

        Assert.Equal(table[0].TeamId, movement.Promoted[0]);
        Assert.Equal(1, movement.Movements.First(m => m.TeamId == table[0].TeamId).ToTier);
    }

    [Fact]
    public void NobodyIsRelegatedOutOfTheBottomDivisionBecauseThereIsNothingBelowIt()
    {
        var table = StandingsFor(12, reversed: true);

        var movement = DivisionMovement.From(3, table);

        Assert.DoesNotContain(movement.Movements, movement => movement.ToTier > 3);
        Assert.Equal(8, movement.Staying.Count);
        Assert.Equal(CompetitionRules.PromotionSlots, movement.Promoted.Count);
        Assert.Equal(table[0].TeamId, movement.Promoted[0]);

        // The champion of the third division goes up into the second: a tier's number counts
        // down as you climb, and winning the bottom division and staying in it would make the
        // title worthless.
        Assert.Equal(2, movement.Movements.First(m => m.TeamId == table[0].TeamId).ToTier);
    }

    [Fact]
    public void ThePyramidIsAClosedSystemWithNobodyLeftOutAndNobodyArrivingFromNowhere()
    {
        var tables = new Dictionary<int, IReadOnlyList<StandingEntry>>
        {
            [1] = StandingsFor(12, reversed: true),
            [2] = StandingsFor(12, reversed: true),
            [3] = StandingsFor(12, reversed: true)
        };

        var movements = tables.ToDictionary(pair => pair.Key, pair => DivisionMovement.From(pair.Key, pair.Value));

        var everybody = movements.Values.SelectMany(m => m.Movements).ToList();

        Assert.Equal(36, everybody.Count);
        Assert.Equal(36, everybody.Select(m => m.TeamId).Distinct().Count());
        Assert.All(everybody, movement =>
        {
            Assert.InRange(movement.ToTier, 1, 3);
            Assert.Equal(12, everybody.Count(other => other.ToTier == movement.ToTier));
        });
    }

    [Fact]
    public void ADivisionWithNobodyInItCannotBeReorganised()
    {
        Assert.Throws<ArgumentException>(() => DivisionMovement.From(1, Array.Empty<StandingEntry>()));
    }

    private static IReadOnlyList<StandingEntry> StandingsFor(int clubs, bool reversed)
    {
        var teams = Enumerable.Range(0, clubs).Select(index => Guid.NewGuid()).ToList();
        if (reversed)
        {
            teams.Reverse();
        }

        return teams
            .Select((teamId, index) => new StandingEntry
            {
                TeamId = teamId,
                Played = 22,
                Wins = 22 - index,
                Draws = 0,
                Losses = index,
                GoalsFor = 40 - index * 2,
                GoalsAgainst = 10 + index,
                YellowCards = 0,
                RedCards = 0,
                Stars = 3
            })
            .ToList();
    }
}
