using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What a player is worth and what he costs.
///
/// The formula is one line of arithmetic and every mistake that matters is in what it reads:
/// the age that stops counting, the discount that has to have a floor, and the contract that
/// makes a wage more than a hundredth of a price.
/// </summary>
public class PlayerValuationTests
{
    private static Player PlayerOfAge(int age) => Player.Create(
        "Test Player",
        age,
        Position.MID,
        goalkeeperPower: 0,
        reflexes: 0,
        speed: 16,
        accuracy: 16,
        dribbling: 16,
        heading: 16,
        strength: 16);

    private static PlayerSeasonState StateWith(int injuries, int redCards)
    {
        var state = PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 90);

        for (var knock = 0; knock < injuries; knock++)
        {
            state.AddInjury(Injury.Light, 1);
        }

        for (var sendingOff = 0; sendingOff < redCards; sendingOff++)
        {
            state.AddRedCard();
        }

        return state;
    }

    [Fact]
    public void The_value_is_stars_times_the_years_left_times_a_hundred_times_a_hundred_and_twenty()
    {
        // 4 stars, 25 years old, 100% of the price, 120 per star.
        var value = PlayerValuation.MarketValue(stars: 4, age: 25, injuries: 0, redCards: 0);

        Assert.Equal(4 * 25 * 100 * 120, value);
        Assert.Equal(1_200_000m, value);
    }

    [Fact]
    public void A_year_of_age_is_a_year_of_price()
    {
        var younger = PlayerValuation.MarketValue(4, 25, 0, 0);
        var older = PlayerValuation.MarketValue(4, 26, 0, 0);

        // A year is a fifth of the price, because age is a fifth of the formula: four stars,
        // a hundred of the price and a hundred and twenty a star.
        Assert.Equal(younger - (4 * 100 * 120), older);
    }

    [Fact]
    public void An_injury_and_a_sending_off_cost_the_same()
    {
        var hurt = PlayerValuation.MarketValue(4, 25, injuries: 1, redCards: 0);
        var sentOff = PlayerValuation.MarketValue(4, 25, injuries: 0, redCards: 1);

        Assert.Equal(hurt, sentOff);
    }

    [Fact]
    public void Every_setback_takes_a_tenth_of_a_point_off_the_price()
    {
        var whole = PlayerValuation.MarketValue(4, 25, 0, 0);
        var hurtOnce = PlayerValuation.MarketValue(4, 25, 1, 0);
        var hurtTwice = PlayerValuation.MarketValue(4, 25, 2, 0);

        Assert.Equal(whole * 0.999m, hurtOnce);
        Assert.Equal(whole * 0.998m, hurtTwice);
    }

    [Fact]
    public void Ten_knocks_cost_a_point_of_the_price()
    {
        var whole = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(whole * 0.99m, PlayerValuation.MarketValue(4, 25, 10, 0));
    }

    [Fact]
    public void A_player_cannot_be_worth_nothing_by_being_unlucky()
    {
        // The discount stops one step short of nothing, because a season of injuries must
        // never price a good man below a bad one. A thousand knocks would ask for a hundred
        // points off and the price keeps a hundredth of itself.
        var wrecked = PlayerValuation.MarketValue(4, 25, injuries: 1_000, redCards: 0);
        var pristine = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(pristine * 0.01m, wrecked);
        Assert.True(wrecked > 0m);
    }

    [Fact]
    public void A_player_of_fifty_is_worth_nothing()
    {
        Assert.Equal(0m, PlayerValuation.MarketValue(5, 50, 0, 0));
        Assert.Equal(0m, PlayerValuation.MarketValue(5, 61, 0, 0));
    }

    [Fact]
    public void A_seasons_wage_is_a_hundredth_of_the_price()
    {
        var value = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(12_000m, PlayerValuation.SeasonWage(value));
    }

    [Fact]
    public void A_contract_is_a_seasons_wage_for_every_season_of_it()
    {
        var value = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(36_000m, PlayerValuation.ContractFee(value, 3));
        Assert.Equal(60_000m, PlayerValuation.ContractFee(value, 5));
    }

    [Fact]
    public void A_club_pays_the_same_wage_every_year_of_a_contract()
    {
        // The bill is the season's wage and not the whole contract: a club that paid three
        // years of wages in the first one would be a club that had budgeted for a war, and
        // the length of a contract decides how long the club is committed rather than what a
        // year of it costs.
        var value = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(PlayerValuation.SeasonWage(value), PlayerValuation.SeasonWage(value));
        Assert.Equal(value / 100m, PlayerValuation.ContractFee(value, 3) / 3);
    }

    [Fact]
    public void A_player_in_the_last_season_of_his_contract_costs_what_he_is_worth()
    {
        var value = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(value, PlayerValuation.AskingPrice(value, inLastSeason: true));
    }

    [Fact]
    public void A_player_with_years_left_costs_a_fifth_more_than_he_is_worth()
    {
        // The fine is what makes a contract worth something: a rival who wants a man in the
        // first of three seasons has to buy the two he cannot have.
        var value = PlayerValuation.MarketValue(4, 25, 0, 0);

        Assert.Equal(value * 1.2m, PlayerValuation.AskingPrice(value, inLastSeason: false));
    }

    [Fact]
    public void A_fine_is_a_fifth_and_not_a_halving()
    {
        // A penalty of a hundred percent would be a veto rather than a price, and a fine that
        // doubles a player's value is a rule nobody argued about.
        Assert.Equal(20m, PlayerValuation.ContractPenaltyPercent);
    }

    [Fact]
    public void An_asking_price_is_a_price_and_holds_the_cents_a_price_is_worth()
    {
        // A third of a limo is a real amount of money in a book, and rounding it away would
        // make the price on a card a number no club would ever agree to.
        Assert.Equal(19_260.00m, PlayerValuation.AskingPrice(16_050.00m, inLastSeason: false));
    }

    [Fact]
    public void A_contract_runs_for_some_seasons_or_it_is_not_a_contract()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerValuation.ContractFee(1_000_000m, 0));
    }

    [Fact]
    public void The_price_of_a_player_reads_his_season_and_not_only_his_attributes()
    {
        var player = PlayerOfAge(age: 25);

        var clean = PlayerValuation.MarketValue(player, StateWith(injuries: 0, redCards: 0));
        var knockedAbout = PlayerValuation.MarketValue(player, StateWith(injuries: 4, redCards: 2));

        Assert.True(knockedAbout < clean);
        Assert.Equal(clean * 0.994m, knockedAbout);
    }

    [Fact]
    public void A_season_counts_every_knock_and_not_only_the_worst_one()
    {
        var state = PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 90);

        state.AddInjury(Injury.Light, 1);
        state.AddInjury(Injury.Grave, 5);
        state.AddInjury(Injury.Light, 1);

        // The injury he carries is the worst of the three, and it is still one injury. The
        // count is three, and the market is told three.
        Assert.Equal(Injury.Grave, state.Injury);
        Assert.Equal(3, state.Injuries);
    }

    [Fact]
    public void A_player_with_nothing_wrong_with_him_is_worth_more_than_one_with_something()
    {
        var player = PlayerOfAge(age: 30);

        var clean = PlayerValuation.MarketValue(player, StateWith(0, 0));
        var sentOff = PlayerValuation.MarketValue(player, StateWith(0, 1));

        Assert.True(sentOff < clean);
        Assert.Equal(clean / 100m, PlayerValuation.SeasonWage(player, StateWith(0, 0)));
    }

    [Fact]
    public void A_wage_read_off_a_player_is_the_wage_read_off_his_price()
    {
        var player = PlayerOfAge(age: 25);
        var state = StateWith(1, 1);

        Assert.Equal(
            PlayerValuation.SeasonWage(PlayerValuation.MarketValue(player, state)),
            PlayerValuation.SeasonWage(player, state));
    }

    [Fact]
    public void A_free_agent_costs_a_fifth_of_what_he_is_worth()
    {
        // There is nobody to buy him from, and a man worth 1.2 million is not picked up off the
        // street for nothing: the signing fee is a share of his own value, so it rises and falls
        // with the player rather than being a round number nobody can budget around.
        Assert.Equal(240_000m, PlayerValuation.FreeAgentSigningFee(1_200_000m));
        Assert.Equal(0m, PlayerValuation.FreeAgentSigningFee(0m));
    }
}
