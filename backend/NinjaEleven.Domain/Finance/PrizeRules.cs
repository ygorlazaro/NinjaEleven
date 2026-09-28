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

    /// <summary>
    /// How many of a competition's top scorers are paid, first place included.
    /// </summary>
    public const int TopScorerPlaces = 3;

    /// <summary>What the first top scorer takes, as a share of the champion's own prize.</summary>
    public const decimal TopScorerFirstRate = 0.10m;

    /// <summary>What the second top scorer takes.</summary>
    public const decimal TopScorerSecondRate = 0.05m;

    /// <summary>What the third top scorer takes.</summary>
    public const decimal TopScorerThirdRate = 0.03m;

    /// <summary>
    /// The share of the champion's prize that goes with a place in the artilharia, and zero for
    /// a place there is none of.
    /// </summary>
    /// <remarks>
    /// A place past the third pays nothing rather than the smallest share as a default: a method
    /// whose fall-through branch is a payment is a method that will pay the fourth-best striker
    /// in the country for a prize nobody announced.
    /// </remarks>
    public static decimal TopScorerRate(int place) => place switch
    {
        1 => TopScorerFirstRate,
        2 => TopScorerSecondRate,
        3 => TopScorerThirdRate,
        _ => 0m
    };

    /// <summary>
    /// What a top scorer is paid for a place in the artilharia: a share of what the champion of
    /// that division is paid for winning it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The champion's prize is the base and not the whole purse, so the artilharia scales with
    /// the division it belongs to: the first division's top striker is paid a share of a title
    /// worth many millions, the third division's a share of a much smaller one, and the two
    /// tables are read in the same words and answered in the money of their own level.
    /// </para>
    /// <para>
    /// The money is new money and comes out of nobody's share. It is not taken off the
    /// champion's cheque, because a title is paid for a season's football and a prize for scoring
    /// is paid for a season's goals: a manager whose club wins the division and whose striker
    /// wins the artilharia is paid twice, and the club that finished second with the same striker
    /// is paid once. The division's purse is the measure of the prize and nothing more.
    /// </para>
    /// </remarks>
    /// <param name="place">Which of the prizes, counted from one.</param>
    /// <param name="clubsInDivision">How many clubs the division's title was paid across.</param>
    /// <param name="purse">What the division's table was paid out of.</param>
    public static decimal TopScorerPrize(int place, int clubsInDivision, decimal purse) =>
        TopScorerShareOf(place, ChampionshipPrize(1, clubsInDivision, purse));

    /// <summary>
    /// A place in any competition's artilharia, taken from that competition's own title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The base is the title of the competition the goals were scored in, which is the whole
    /// rule: the cup's artilharia is a share of the cup's own champion's prize, so a
    /// third-division forward who tops the cup's scoring is paid the same as a first-division
    /// one who does — the cup runs across the pyramid, and its prize is the cup's, not the
    /// pyramid's. Reading a cup striker's share out of his own club's division would price the
    /// same goals three different ways according to which club he happened to play for, which
    /// is a rule about divisions wearing the words of a rule about a cup.
    /// </para>
    /// <para>
    /// Like the division's, it is new money and not a slice of anyone's cheque: a club that wins
    /// the cup with its own top scorer on the sheet is paid the title and the artilharia, because
    /// winning the cup and scoring in it are two things the club did.
    /// </para>
    /// </remarks>
    /// <param name="place">Which of the prizes, counted from one.</param>
    /// <param name="championPrize">What the champion of that competition is paid.</param>
    public static decimal TopScorerShareOf(int place, decimal championPrize) =>
        place is >= 1 and <= TopScorerPlaces
            ? Round(championPrize * TopScorerRate(place))
            : 0m;
}
