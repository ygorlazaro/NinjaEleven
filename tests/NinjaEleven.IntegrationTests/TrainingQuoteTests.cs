using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// The sheet a manager plans a week with: every man, every attribute, and the price of a
/// session on each.
///
/// <para>
/// The prices are the part worth holding. They are sent rather than recomputed, because a
/// client working them out would be a second implementation of the same curve — and the one
/// number a manager is about to trust is the number on the button he is about to press.
/// </para>
/// </summary>
public class TrainingQuoteTests
{
    [Fact]
    public async Task ASquadIsQuotedInOneCallWithAPriceForEveryAttribute()
    {
        using var world = await TrainingWorld.GivenAsync(Clubs());

        var quotes = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        Assert.Equal(Clubs().Count, quotes.Players.Count);
        Assert.Equal(quotes.Players.Sum(player => player.Energy), quotes.SquadEnergy);

        Assert.All(quotes.Players, player =>
        {
            Assert.Equal(DevelopmentRules.All.Count, player.Attributes.Count);
        });
    }

    [Fact]
    public async Task ThePriceOnTheSheetIsThePriceTheSessionCharges()
    {
        // The one thing that must not drift: a cell that quotes 12 and a session that spends
        // 15 is a manager who pressed a button and lost three more energy than he was told,
        // and he has no way to find out which of the two numbers was the lie.
        using var world = await TrainingWorld.GivenAsync(Clubs());

        var quotes = await world.Training().QuoteAsync(TrainingWorld.TheClub);
        var player = quotes.Players.First();
        var cell = player.Attributes.First(attribute => attribute.Cost is not null);

        var result = await world.Training().TrainAsync(player.PlayerId, Enum.Parse<PlayerAttribute>(cell.Attribute));

        Assert.Equal(cell.Cost, result.EnergySpent);
    }

    [Fact]
    public async Task AnAttributeAtTheCeilingIsQuotedAsUntrainable()
    {
        // Null and not a number: a price of zero would offer a free session that the backend
        // then refuses, and a price at all would tell the manager he has something to buy.
        using var world = await TrainingWorld.GivenAsync(Clubs());

        var quotes = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        var finished = quotes.Players
            .SelectMany(player => player.Attributes.Select(attribute => (player, attribute)))
            .Where(pair => pair.attribute.Value >= pair.player.Potential)
            .ToList();

        Assert.All(finished, pair =>
        {
            Assert.Null(pair.attribute.Cost);
        });
    }

    [Fact]
    public async Task AGoalkeeperAttributeOnAnOutfielderIsQuotedAsUntrainable()
    {
        using var world = await TrainingWorld.GivenAsync(Clubs());

        var quotes = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        var outfielders = quotes.Players.Where(player => player.Position != nameof(Position.GK));

        Assert.NotEmpty(outfielders);
        Assert.All(outfielders, player =>
        {
            Assert.Null(player.Attributes
                .First(attribute => attribute.Attribute == nameof(PlayerAttribute.Reflexes)).Cost);
            Assert.Null(player.Attributes
                .First(attribute => attribute.Attribute == nameof(PlayerAttribute.GoalkeeperPower)).Cost);
        });
    }

    [Fact]
    public async Task APlayerWithNoSeasonIsNotOnTheSheet()
    {
        // The sheet is the club's men, not every man who has ever worn its colours. A quote
        // built from the player table would price a player who left in 2019, and a manager
        // would be offered a session on a man the club cannot register.
        using var world = await TrainingWorld.GivenAsync(
            Clubs(),
            withoutASeason: TrainingWorld.APlayer("Fora de season", 24, Position.ATT, 80));

        var quotes = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        Assert.DoesNotContain(quotes.Players, player => player.Name == "Fora de season");
    }

    [Fact]
    public async Task ASheetWithoutASeasonIsRefusedRatherThanGuessedAt()
    {
        using var world = await TrainingWorld.GivenAsync([], seasons: 0);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => world.Training().QuoteAsync(TrainingWorld.TheClub));

        Assert.Equal("SeasonRequired", error.Code);
    }

    // --- Helpers ------------------------------------------------------------------

    private static List<Player> Clubs() => new()
    {
        TrainingWorld.APlayer("Zagueiro", 23, Position.DEF, 78),
        TrainingWorld.APlayer("Meia", 26, Position.MID, 84),
        TrainingWorld.APlayer("Goleiro", 29, Position.GK, 80)
    };
}
