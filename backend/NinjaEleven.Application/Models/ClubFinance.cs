using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A page of a club's book and the three numbers that sit above it.
///
/// The page and the summary come back together because they are asked about together: a
/// manager who opens his books to a season is looking at that season's spending and at what
/// it left him with, and a screen that had to make a second call for the totals would show
/// the two halves of one answer at two different moments.
/// </summary>
public class FinanceLedger
{
    /// <summary>What the club has, from the last line written in its whole book.</summary>
    public decimal Balance { get; init; }

    /// <summary>What came in over the filter, excluding the two lines that state a balance.</summary>
    public decimal Income { get; init; }

    /// <summary>What went out over the filter, as a positive figure.</summary>
    public decimal Expenses { get; init; }

    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }

    public IReadOnlyList<FinanceLedgerLine> Movements { get; init; } = Array.Empty<FinanceLedgerLine>();
}

/// <summary>One line of a club's book, as a client reads it.</summary>
public class FinanceLedgerLine
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }

    /// <summary>
    /// The number of the season, and the name beside it, so a line from a season the manager
    /// is not looking at still says where it came from.
    /// </summary>
    public int SeasonNumber { get; init; }
    public string SeasonName { get; init; } = string.Empty;

    public int? MatchDayNumber { get; init; }
    public FinanceMovementKind Kind { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }

    /// <summary>
    /// Whether this line states a balance rather than moving one. It travels with the line so
    /// a client can hold the founding capital and a carried balance out of a season's income
    /// without having to know the kinds by heart.
    /// </summary>
    public bool StatesABalance { get; init; }
}
