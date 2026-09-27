namespace NinjaEleven.Domain.Players;

/// <summary>
/// What a player is worth and what he costs, in one place.
///
/// The two are the same number read twice. A club that pays a wage is buying a share of a man
/// whose price is his value, and a game where those two are unrelated is a game where a
/// manager can sign a man for nothing and a club can be robbed by accident.
///
/// The value reads three things off a player and nothing else: how good he is, how long he
/// has left, and how often the game has taken something out of him. Starvation is the age —
/// a man of forty is worth a fraction of the same man at twenty-four, and without that term
/// the market would not care whether a signing could still run. The discount is a season's
/// knocks and sendings-off, a hundredth of the price for each, because a player who keeps
/// missing matches is a player the club will be signing again in a year for somebody else.
/// </summary>
public static class PlayerValuation
{
    /// <summary>
    /// What the club is worth a hundredth of per season of a contract.
    ///
    /// It is the divisor between a price and a wage, and it is the reason a contract has a
    /// length: a man signed for five seasons costs the club five shares, and a man signed for
    /// one costs one. The club that spends on wages is spending on years, which is the decision
    /// a contract is.
    /// </summary>
    public const decimal SeasonsPerSalary = 100m;

    /// <summary>
    /// What one star of quality is worth, all else equal. The multiplier is what makes a
    /// four-star player worth about four times what the same age and the same season of
    /// injuries leave a one-star player worth.
    /// </summary>
    public const decimal StarValue = 120m;

    /// <summary>
    /// The age at which a player's price is nothing. A player of fifty cannot be worth
    /// anything, and a formula that let him be would say every old player is a bargain.
    /// </summary>
    public const int AgeFloor = 50;

    /// <summary>
    /// What one injury or one sending-off costs, in percent of the price. Both are the same
    /// thing to a club's season: a man missing games is a match the club has to spend another
    /// player on.
    /// </summary>
    public const decimal SetbackPercent = 0.1m;

    /// <summary>
    /// What another club pays on top of a player's price to take him before the last season
    /// of his contract.
    ///
    /// It is the fine for breaking a promise the club made to the player rather than to the
    /// manager: a man signed for three seasons is a club's asset for three seasons, and a
    /// rival who wants him in the first of them has to pay for the two he cannot have. The
    /// price on a card is therefore two numbers, and the difference between them is what the
    /// contract costs to break.
    ///
    /// It is a fifth rather than a round half because a fine that doubles a price is not a
    /// fine, it is a veto: at a hundred percent there is no price at which a squad could be
    /// sold out from under itself, and a number with no upper limit of its own would be one.
    /// </summary>
    public const decimal ContractPenaltyPercent = 20m;

    /// <summary>
    /// What another club has to pay for this player, in limos.
    ///
    /// The same as what he is worth when he is in the last season of his contract, and a
    /// fifth more on top of that while he is not: a manager reading a transfer list needs to
    /// see the price he would have to agree, and a price that quietly became another number
    /// at the moment of negotiation is a price nobody budgeted for.
    /// </summary>
    public static decimal AskingPrice(decimal marketValue, bool inLastSeason)
    {
        if (inLastSeason)
        {
            return marketValue;
        }

        return decimal.Round(
            marketValue * (1m + ContractPenaltyPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// What one player costs a club for one season, in limos: a hundredth of what he is
    /// worth.
    ///
    /// The contract is what the club has committed itself to over its whole run — a hundredth
    /// of the price for every season of it, and <see cref="ContractFee"/> is that figure — but
    /// a club pays a season's wage once a season, and paying the whole contract in the first
    /// year would be a club that had budgeted for a war. So the length of a contract does not
    /// enter the season's bill: it decides how long the club is committed, and the bill is
    /// what the player is worth, a hundredth, every year of it.
    /// </summary>
    public static decimal SeasonWage(decimal marketValue) => marketValue / SeasonsPerSalary;

    /// <summary>
    /// What a club has committed itself to for a player over the whole of his contract, in
    /// limos: a season's wage for every season of it.
    /// </summary>
    public static decimal ContractFee(decimal marketValue, int contractSeasons)
    {
        if (contractSeasons <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contractSeasons), contractSeasons, "A contract runs for some seasons or it is not a contract.");
        }

        return SeasonWage(marketValue) * contractSeasons;
    }

    /// <summary>
    /// What a player is worth on the market, in limos.
    /// </summary>
    public static decimal MarketValue(double stars, int age, int injuries, int redCards)
    {
        if (stars < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stars), stars, "Nobody has negative stars.");
        }

        if (injuries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(injuries), injuries, "A player cannot have been injured a negative number of times.");
        }

        if (redCards < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(redCards), redCards, "A player cannot have been sent off a negative number of times.");
        }

        // Nobody is signed at fifty. A player past the age floor is worth nothing, which is
        // what a club that wanted him would be told by anyone with a memory of last season.
        if (age >= AgeFloor)
        {
            return 0m;
        }

        // Every knock and every sending-off is a hundredth off the price, and the discount
        // stops at nothing: a player cannot be worth less than a hundredth of himself, or
        // three seasons of injuries would price a good man below a bad one.
        var discountPercent = Math.Clamp((injuries + redCards) * SetbackPercent, 0m, 99m);

        // The stars are a double because a rating is a double, and a price is not: the rating
        // is cast here, once, so every other line of the formula is decimal arithmetic and the
        // price of a player is exact down to the cent rather than exact to sixteen places.
        var price = (decimal)stars
                    * (AgeFloor - age)
                    * (100m - discountPercent)
                    * StarValue;

        return decimal.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// What a player is worth, read off him and off the season he has had.
    /// </summary>
    public static decimal MarketValue(
        Player player,
        PlayerSeasonState seasonState,
        DateOnly? referenceDate = null)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        if (seasonState is null) throw new ArgumentNullException(nameof(seasonState));

        return MarketValue(
            PlayerRating.CalculateStars(player),
            player.CalculateAge(referenceDate),
            seasonState.Injuries,
            seasonState.RedCards);
    }

    /// <summary>
    /// What a player costs a club for one season, read off him and off the season he has had.
    /// </summary>
    public static decimal SeasonWage(
        Player player,
        PlayerSeasonState seasonState,
        DateOnly? referenceDate = null)
    {
        return SeasonWage(MarketValue(player, seasonState, referenceDate));
    }
}
