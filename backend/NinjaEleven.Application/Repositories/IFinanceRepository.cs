using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for a club's books. It only stores and reads the lines of them: what a gate
/// receipt is worth and what a squad costs is decided in the domain, not here.
/// </summary>
public interface IFinanceRepository
{
    Task AddAsync(FinanceMovement movement, CancellationToken cancellationToken = default);

    /// <summary>
    /// The last line written in a club's book, which is where its balance comes from and the
    /// number the next line continues from. Asks for the newest line rather than summing the
    /// book, because a club's balance is a fact about its last line and summing a career of
    /// movements to answer it would be a different answer every time one was forgotten.
    /// </summary>
    Task<FinanceMovement?> GetLastAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The last line in each of these clubs' books, keyed by club.
    ///
    /// <para>
    /// The same answer as <see cref="GetLastAsync"/>, asked of a whole set at once. An NPC
    /// pass that asks every club in the country whether it can afford a stand has sixty-four
    /// questions to ask, and asking them one at a time is the same N+1 as everywhere else:
    /// sixty-four round trips on a table with a row per line of every club's history.
    /// </para>
    ///
    /// <para>
    /// A club with an empty book is absent from the answer rather than present with a balance
    /// of zero. A club that has never been written to has no balance, and a zero here would be
    /// read as a bankrupt one — which is a decision the rest of this would then make on a
    /// number nobody wrote.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, FinanceMovement>> GetLastForTeamsAsync(
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a club's book already carries a line of this kind for a season. It is what
    /// makes a line that must happen once per season happen once, with no timer and no flag.
    /// </summary>
    Task<bool> ExistsInSeasonAsync(
        Guid teamId,
        Guid seasonId,
        FinanceMovementKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a club's book already carries a line of this kind for a match. It is what
    /// makes a line that happens once per match happen once: a match settled twice — by the
    /// tick that blew the whistle and by one that arrives after it — has to leave one line in
    /// the book and not two.
    ///
    /// The kind is part of the question because a match writes more than one line. A club
    /// that has been paid its gate has been paid for the match, and asking only about the
    /// match would answer for the wages as well and silently refuse to pay them.
    /// </summary>
    Task<bool> ExistsForMatchAsync(
        Guid teamId,
        Guid matchId,
        FinanceMovementKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a club's book already carries this exact payment: the season, what kind of
    /// money it is, and what it was the payment for.
    ///
    /// The reference is the third half of the question and it is not optional. A club is paid
    /// a championship purse and a cup consolation in the same season, and both are prize
    /// money, so a guard that asked only about the kind would refuse the second one — while a
    /// guard that asked nothing would pay the first again the moment a season was closed
    /// twice.
    /// </summary>
    Task<bool> ExistsWithReferenceAsync(
        Guid teamId,
        Guid seasonId,
        FinanceMovementKind kind,
        string reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of a club's book, newest line first, optionally narrowed to one season.
    /// </summary>
    Task<IReadOnlyList<FinanceMovement>> ListAsync(
        Guid teamId,
        Guid? seasonId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>How many lines the book holds under the same filter the page was read with.</summary>
    Task<int> CountAsync(
        Guid teamId,
        Guid? seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What came in and what went out over a filter, with the two lines that state a balance
    /// left out. The founding capital and a balance carried from the season before are money
    /// the club has, not money it earned, and a total that counted them would be a total of
    /// something else.
    /// </summary>
    Task<FinanceTotals> TotalsAsync(
        Guid teamId,
        Guid? seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every line a club wrote between two days of the season, oldest first.
    ///
    /// <para>
    /// This is the read behind the weekly statement, and it is a read of a *span* rather than
    /// a page because a statement that added up only the first twenty lines of a busy week
    /// would be a statement about the wrong week. The two bounds are inclusive on both ends,
    /// so a week of seven days is seven days and a single day is a single day, and the lines
    /// that state a balance rather than moving one are left in: the caller decides what a
    /// statement says about the capital a club was founded on.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<FinanceMovement>> ListBetweenMatchDaysAsync(
        Guid teamId,
        Guid seasonId,
        int fromMatchDay,
        int toMatchDay,
        CancellationToken cancellationToken = default);
}

/// <summary>What came in and what went out, in limos.</summary>
public readonly record struct FinanceTotals(decimal Income, decimal Expenses)
{
    public static FinanceTotals None => new(0m, 0m);
}
