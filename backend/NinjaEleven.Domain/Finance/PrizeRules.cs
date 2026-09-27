namespace NinjaEleven.Domain.Finance;

/// <summary>
/// What a club is paid for a season and for a cup run, in one place.
///
/// Every one of these numbers is a rule of the game and not a setting of a screen, and they
/// live together for the same reason the engine's numbers live in <c>MatchRules</c>: a
/// championship purse buried in the middle of a season-closing service is a purse nobody can
/// find when the pyramid changes.
///
/// **The two competitions pay in different shapes, and the difference is the point.** A
/// championship is a table of twelve clubs over twenty-two matchdays, so its money is shared
/// out by the weight of a position: the champion takes a great deal, the last club takes a
/// little, and every club in the division takes something — including the four going down,
/// because a club that is relegated has still been in the competition all season and a prize
/// that stopped at the relegation line would punish a club for finishing where it could not
/// have finished otherwise. A cup is a knockout of clubs that leave one at a time, so its money
/// is paid on the way out: there is nothing to share, only a consolation for the round that
/// knocked you out, a runner-up's cheque and the winner's.
/// </summary>
public static class PrizeRules
{
    /// <summary>
    /// What the first division's whole table is paid out of at the end of the season.
    /// </summary>
    public const decimal FirstDivisionPurse = 30_000_000m;

    /// <summary>What the second division's whole table is paid out of.</summary>
    public const decimal SecondDivisionPurse = 20_000_000m;

    /// <summary>What the third division's whole table is paid out of.</summary>
    public const decimal ThirdDivisionPurse = 10_000_000m;

    /// <summary>
    /// The base of the geometric weight a position carries: the champion's weight is
    /// <c>1.3^11</c> and the last club's is <c>1.3^0</c>, one.
    /// </summary>
    public const decimal WeightBase = 1.3m;

    /// <summary>The purse a division's table is paid out of.</summary>
    public static decimal PurseForTier(int tier) => tier switch
    {
        1 => FirstDivisionPurse,
        2 => SecondDivisionPurse,
        _ => ThirdDivisionPurse
    };

    /// <summary>
    /// The weight of a finishing position: <c>1.3 ^ (clubs - position)</c>.
    /// </summary>
    /// <remarks>
    /// It is a geometric weight and not an arithmetic one so that the gap between the champion
    /// and the runner-up is the same size as the gap between the eleventh and the twelfth.
    /// An arithmetic scale pays the top of the table a bonus that flattens the whole middle
    /// into a block of nearly equal cheques, and a championship in which second place is worth
    /// the same as ninth is a championship where there is nothing to play for until the last
    /// matchday.
    /// </remarks>
    /// <param name="position">Where the club finished, counted from one.</param>
    /// <param name="clubsInDivision">How many clubs the division has.</param>
    public static decimal WeightOf(int position, int clubsInDivision)
    {
        if (position < 1 || position > clubsInDivision || clubsInDivision < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                $"A position is counted from one and there are {clubsInDivision} clubs.");
        }

        var exponent = clubsInDivision - position;
        var weight = 1m;

        for (var power = 0; power < exponent; power++)
        {
            weight *= WeightBase;
        }

        return weight;
    }

    /// <summary>
    /// The whole weight of a division's table, which is the denominator every share is
    /// measured against.
    /// </summary>
    public static decimal TotalWeight(int clubsInDivision)
    {
        var total = 0m;

        for (var position = 1; position <= clubsInDivision; position++)
        {
            total += WeightOf(position, clubsInDivision);
        }

        return total;
    }

    /// <summary>
    /// A division's whole purse, split between its clubs, the champion's share first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shares are worked out from the unrounded weight and rounded to the cent, and the
    /// rounding is then settled by the last club: it is paid whatever is left of the purse
    /// after the other eleven have been paid theirs. Without that, twelve roundings can add up
    /// to a purse one limo short of the one the rules say — a limo here, a limo there, and a
    /// championship that has quietly stopped paying its whole purse out.
    /// </para>
    /// <para>
    /// The last club carrying the remainder is not a favour to it. It is the only position a
    /// reader will never check, and a share that is a rounding different from the published
    /// one is a share that a manager finds by adding up the other eleven.
    /// </para>
    /// </remarks>
    /// <param name="clubsInDivision">How many clubs the division has.</param>
    /// <param name="purse">What the division's table is paid out of.</param>
    public static IReadOnlyList<decimal> ChampionshipPrizes(int clubsInDivision, decimal purse)
    {
        if (clubsInDivision < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clubsInDivision), clubsInDivision, "A division has at least one club in it.");
        }

        var total = TotalWeight(clubsInDivision);
        var shares = new decimal[clubsInDivision];
        var paid = 0m;

        for (var position = 1; position < clubsInDivision; position++)
        {
            shares[position - 1] = Round(
                WeightOf(position, clubsInDivision) / total * purse);

            paid += shares[position - 1];
        }

        shares[clubsInDivision - 1] = purse - paid;

        return shares;
    }

    /// <summary>
    /// What one club is paid for a finishing position.
    /// </summary>
    /// <remarks>
    /// It reads the whole split rather than working out this one share on its own, so the
    /// figure a trophy carries and the figure in the club's book are the same figure. A title
    /// worth seven million two hundred and thirty-four thousand limos and a book line for
    /// seven million two hundred and thirty-four thousand and one limo are two different
    /// amounts of money for the same evening.
    /// </remarks>
    public static decimal ChampionshipPrize(int position, int clubsInDivision, decimal purse) =>
        ChampionshipPrizes(clubsInDivision, purse)[position - 1];

    private static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>What a club knocked out among the last thirty-two is paid.</summary>
    public const decimal CupLastThirtyTwoLoser = 10_000m;

    /// <summary>What a club knocked out among the last sixteen is paid.</summary>
    public const decimal CupLastSixteenLoser = 200_000m;

    /// <summary>What a club knocked out among the last eight is paid.</summary>
    public const decimal CupQuarterFinalLoser = 500_000m;

    /// <summary>What a club knocked out among the last four is paid.</summary>
    public const decimal CupSemiFinalLoser = 1_000_000m;

    /// <summary>What the finalist that lost the final is paid.</summary>
    public const decimal CupFinalLoser = 3_000_000m;

    /// <summary>What the cup is worth.</summary>
    public const decimal CupChampionPrize = 5_000_000m;

    /// <summary>
    /// What a club is paid for going out of the cup in a tie-round.
    /// </summary>
    /// <remarks>
    /// The consolation grows steeply as the round does, and it is not a share of anything: the
    /// club that goes out among the last thirty-two is paid three hundredth of what the finalist
    /// is paid, and the difference between the two is the prize for having been in the
    /// competition at all. A cup where the first-round loser is paid the same as the finalist
    /// would be telling a club that its run made no difference.
    ///
    /// A round the cup does not have pays nothing, rather than the largest consolation as a
    /// default: a method whose fall-through branch is a payment is a method that will pay it
    /// for a round that was never played.
    /// </remarks>
    /// <param name="tieRound">Which of the five tie-rounds the club went out in.</param>
    public static decimal CupConsolation(int tieRound) => tieRound switch
    {
        1 => CupLastThirtyTwoLoser,
        2 => CupLastSixteenLoser,
        3 => CupQuarterFinalLoser,
        4 => CupSemiFinalLoser,
        5 => CupFinalLoser,
        _ => 0m
    };
}
