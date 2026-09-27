using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A page of a club's book and the three numbers above it.
///
/// The page and the summary arrive together because they are asked about together, and a
/// screen that had to fetch the totals separately could draw a balance and an income from two
/// different moments of a matchday that is still being played.
/// </summary>
public class FinanceLedgerDto
{
    /// <summary>What the club has, from the last line written in its whole book.</summary>
    public decimal Balance { get; init; }

    /// <summary>What came in over the filter, without the two lines that state a balance.</summary>
    public decimal Income { get; init; }

    /// <summary>What went out over the filter, as a positive figure.</summary>
    public decimal Expenses { get; init; }

    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }

    public IReadOnlyList<FinanceMovementDto> Movements { get; init; } = Array.Empty<FinanceMovementDto>();
}

/// <summary>One line of a club's book.</summary>
public class FinanceMovementDto
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public int SeasonNumber { get; init; }
    public string SeasonName { get; init; } = string.Empty;

    /// <summary>
    /// The day of the season, or null for a line that belongs to no day: the capital a club
    /// is founded on, and the balance it is handed to open a season.
    /// </summary>
    public int? MatchDayNumber { get; init; }

    /// <summary>The kind, as a name. The client maps it to a mark and to words.</summary>
    public string Kind { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }

    /// <summary>Whether this line states a balance rather than moving one.</summary>
    public bool StatesABalance { get; init; }
}
