namespace NinjaEleven.Domain.Finance;

/// <summary>
/// One line of a club's books.
///
/// The line is the truth and the balance is read off it: every row carries the balance the
/// club was left with, so a page read anywhere in the history says what the club had on that
/// day, and a season's closing balance is a fact rather than a sum somebody has to add up
/// again. That is also why the balance is stored instead of derived: a club's second season
/// opens on what the first one closed with, and a ledger that could only be read from the
/// beginning would have to be summed in memory to answer a question about page forty.
///
/// A negative balance is allowed. A club that has spent more than it took has gone into
/// debt, and a game that refused to write the line would leave the books correct and the
/// club fictional: the manager would be told he is fine while his players are not being paid.
/// </summary>
public class FinanceMovement
{
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid SeasonId { get; private set; }

    /// <summary>
    /// The sequence this line was written in, counted per club. It is what orders a day's
    /// movements: the day itself is a matchday, and two payments made on the same matchday
    /// have no other way to say which came first.
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>
    /// The day of the season the movement belongs to, or null for one that belongs to no day —
    /// the capital a club is founded on, and the balance it is handed to open a season.
    /// </summary>
    public int? MatchDayNumber { get; private set; }

    public FinanceMovementKind Kind { get; private set; }
    public string Description { get; private set; } = string.Empty;

    /// <summary>Money in when positive, money out when negative, in limos.</summary>
    public decimal Amount { get; private set; }

    /// <summary>What the club had once this line had been written.</summary>
    public decimal BalanceAfter { get; private set; }

    /// <summary>The match that moved the money, when a match did.</summary>
    public Guid? MatchId { get; private set; }

    /// <summary>
    /// What this line is the payment for, when it is a payment for a thing rather than for a
    /// match: the round a club went out of the cup in, the position it finished the division
    /// in, the tie it won.
    /// </summary>
    /// <remarks>
    /// It exists because a club is paid more than one prize in a season and each of them has
    /// to happen once. A guard that only asked "has this club been paid a prize this season"
    /// would refuse the second one, and a guard that asked nothing would pay the first twice
    /// the moment a season was closed a second time. What the prize was *for* is the thing
    /// that makes it the same prize.
    /// </remarks>
    public string? Reference { get; private set; }

    private FinanceMovement() { }

    /// <summary>
    /// A line of the book, given what the club had before it.
    ///
    /// A line that states a balance rather than moving one is written here without touching
    /// the balance: the seed and the carried balance both say what the club has, and a book
    /// that added them would double the club's money on the way in.
    /// </summary>
    public static FinanceMovement Create(
        Guid teamId,
        Guid seasonId,
        int sequence,
        int? matchDayNumber,
        FinanceMovementKind kind,
        string description,
        decimal amount,
        decimal balanceBefore,
        Guid? matchId = null,
        string? reference = null)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("A movement belongs to a club.", nameof(teamId));
        }

        if (seasonId == Guid.Empty)
        {
            throw new ArgumentException("A movement belongs to a season.", nameof(seasonId));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "A club's first line of the book is line one.");
        }

        if (matchDayNumber is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchDayNumber), matchDayNumber, "A season has no day before the first.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A line of the book says what it is for.", nameof(description));
        }

        // A line of income has no sign to argue about: a negative gate receipt is a receipt
        // that took money in, and reading it as a cost would be the wrong sign twice.
        if (amount == 0m && !StatesABalance(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "A movement moves money or states a balance, and this one does neither.");
        }

        return new FinanceMovement
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            SeasonId = seasonId,
            Sequence = sequence,
            MatchDayNumber = matchDayNumber,
            Kind = kind,
            Description = description.Trim(),
            Amount = StatesABalance(kind) ? balanceBefore : amount,
            BalanceAfter = StatesABalance(kind) ? balanceBefore : balanceBefore + amount,
            MatchId = matchId,
            Reference = reference
        };
    }

    /// <summary>
    /// The capital a club is founded on, written as the first line of its book.
    ///
    /// It is a line rather than a column on the club because a club's money is its history:
    /// a balance with no line behind it is a number somebody typed, and this one is the same
    /// kind of thing as every other movement, which means the same code reads all of it.
    /// </summary>
    public static FinanceMovement Seed(Guid teamId, Guid seasonId, decimal capital)
    {
        if (capital < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capital), capital, "A club is not founded in debt.");
        }

        return OpenWithBalance(
            teamId,
            seasonId,
            sequence: 1,
            FinanceMovementKind.Seed,
            "Capital inicial do clube",
            capital);
    }

    /// <summary>
    /// The line that opens a season on a balance the club was already holding.
    ///
    /// The line belongs to the season being opened, and the amount is the balance itself: the
    /// whole point of the line is to say "this is what you started with", and a version that
    /// took an amount would be free to be wrong about it. The two kinds that state a balance
    /// are the founding capital and the balance carried from the season before, and neither
    /// of them is income — a season that counted the money it was handed as money it earned
    /// would be reporting a fortune nobody earned.
    /// </summary>
    public static FinanceMovement OpenWithBalance(
        Guid teamId,
        Guid seasonId,
        int sequence,
        FinanceMovementKind kind,
        string description,
        decimal balance,
        Guid? matchId = null)
    {
        if (kind != FinanceMovementKind.Seed && kind != FinanceMovementKind.CarryOver)
        {
            throw new ArgumentException(
                "Only the capital a club is founded on and a carried balance open a book.",
                nameof(kind));
        }

        return Create(
            teamId,
            seasonId,
            sequence,
            matchDayNumber: null,
            kind,
            description,
            amount: balance,
            balanceBefore: balance);
    }

    /// <summary>Whether this line states a balance rather than moving one.</summary>
    public static bool StatesABalance(FinanceMovementKind kind) =>
        kind is FinanceMovementKind.Seed or FinanceMovementKind.CarryOver;

    /// <summary>
    /// Whether a line of this kind is worth interrupting a manager for.
    ///
    /// <para>
    /// One kind is, and the rule for saying so is not "it is large" — it is "the ledger
    /// already says it and the box would only be saying it again in worse words". A gate
    /// receipt, a slice of the wage bill and a training fee arrive on every matchday and every
    /// one of them is a line in a statement that is already on its own screen, so telling the
    /// manager about them one at a time is thirty identical messages a week about money the
    /// game is already accounting for. A manager reads the week in one statement, which is why
    /// the rule is asked here rather than left to each writer.
    /// </para>
    ///
    /// <para>
    /// A prize is the opposite case: it arrives twice or three times a season, it is the
    /// competition paying the club rather than the club paying itself, and there is no other
    /// screen that says the money arrived. Same for nothing else, deliberately — a sponsor's
    /// deal is reported by the sponsor, and a transfer by the market, because what a manager
    /// wants to know about those is who and why, not what the line was called.
    /// </para>
    /// </summary>
    public static bool IsWorthAMessage(FinanceMovementKind kind) =>
        kind is FinanceMovementKind.PrizeMoney;

    /// <summary>Whether this line is money coming in, ignoring the two that state a balance.</summary>
    public bool IsIncome => !StatesABalance(Kind) && Amount > 0m;

    /// <summary>Whether this line is money going out, ignoring the two that state a balance.</summary>
    public bool IsExpense => !StatesABalance(Kind) && Amount < 0m;
}
