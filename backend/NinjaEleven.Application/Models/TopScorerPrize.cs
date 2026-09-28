namespace NinjaEleven.Application.Models;

/// <summary>
/// One place in a competition's artilharia, and the money that goes with it.
/// </summary>
/// <remarks>
/// The place and the prize are two numbers and they are not always the same: two strikers level
/// on goals, games, cards and age are both <em>second</em>, both take the second prize, and the
/// third prize is then paid to nobody. A screen that had one number would have to choose between
/// saying "third" about a man who is not third and paying a man who won second less than the
/// prize for second — so it carries both.
/// </remarks>
public class TopScorerPrizeRow
{
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public int Age { get; init; }

    /// <summary>
    /// The club the prize is paid to. It is the club he scored for in this competition, and not
    /// merely the club he plays for now: the money is a reward for a season's goals scored in
    /// these colours, and a striker sold in January did not score them in the ones he wears.
    /// </summary>
    public required Guid TeamId { get; init; }

    public string? TeamName { get; init; }
    public string? TeamPrimaryColor { get; init; }
    public string? TeamSecondaryColor { get; init; }

    /// <summary>Where he stands, counted from one, shared with anyone the chain could not part.</summary>
    public int Position { get; init; }

    /// <summary>Which of the three prizes this is: one, two or three.</summary>
    public int PrizeSlot { get; init; }

    /// <summary>How many other players share this position, and zero when nobody does.</summary>
    public int TiedWith { get; init; }

    public int Goals { get; init; }

    /// <summary>Games he played in this competition, which is the second thing the order looks at.</summary>
    public int Appearances { get; init; }

    /// <summary>
    /// The cards, weighed — a yellow is one and a red is three — which is the third thing the
    /// order looks at. It is on the row so a manager can see the number the order was settled on
    /// rather than having to work it out from the two columns behind it.
    /// </summary>
    public int CardPoints { get; init; }

    /// <summary>The share of the champion's prize this place carries: 0.10, 0.05 or 0.03.</summary>
    public decimal Rate { get; init; }

    /// <summary>
    /// What the player's club is paid, or null when the competition has no champion's prize to
    /// take a share of.
    /// </summary>
    /// <remarks>
    /// It is null rather than zero. A prize that cannot be worked out is a thing the backend
    /// says it cannot work out, and a screen that showed a number there would be showing a
    /// number it invented. It is never smaller than it should be either: the money is new and
    /// comes out of nobody's share, so a club that won the title and has the top scorer is paid
    /// the title and this.
    /// </remarks>
    public decimal? Amount { get; init; }
}

/// <summary>One place's share, said on its own so a legend does not have to know the rules.</summary>
public record TopScorerRate(int Place, decimal Rate);

/// <summary>
/// What a competition pays its artilharia, and who is paid.
/// </summary>
/// <remarks>
/// It carries the shares as well as the amounts, because the two are the same rule read twice:
/// the amounts are the shares of one title, and a legend that printed only the amounts would say
/// what a striker is paid without saying what the prize is a share of. The base is null for a
/// cup, whose men are paid out of their own divisions' titles rather than out of one purse — and
/// the rows say whose title paid each of them.
/// </remarks>
public class TopScorerPrizeList
{
    public required Guid CompetitionSeasonId { get; init; }
    public Guid SeasonId { get; init; }

    /// <summary>"Copa do Brasil", or the division's own name when this is a table.</summary>
    public required string CompetitionName { get; init; }

    /// <summary>1 is the top of the pyramid, and null for a cup.</summary>
    public int? Tier { get; init; }

    /// <summary>
    /// The champion's prize these shares are taken from, and null for a cup. It is the whole of
    /// the arithmetic in one number: a first-division artilharia is ten per cent of a seven
    /// million title, and a third-division one is ten per cent of a much smaller title.
    /// </summary>
    public decimal? BaseAmount { get; init; }

    /// <summary>The three shares, first place first, whether or not anyone took them.</summary>
    public IReadOnlyList<TopScorerRate> Rates { get; init; } = Array.Empty<TopScorerRate>();

    public IReadOnlyList<TopScorerPrizeRow> Winners { get; init; } = Array.Empty<TopScorerPrizeRow>();
}
