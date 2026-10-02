using NinjaEleven.Domain.Inbox;

namespace NinjaEleven.Application.Repositories;

public interface IInboxMessageRepository
{
    /// <summary>
    /// A page of a club's box, newest first, out of everything in it or of one kind of it.
    ///
    /// The order is the order the messages arrived and nothing else is applied to it. A box
    /// sorted by category would put today's result above a transfer that was agreed an hour
    /// ago, and a manager reads the top of his own mail the way he reads the top of a paper:
    /// by when it got there. The filter narrows which lines are in the page and never reorders
    /// them — a manager who filters by "Partida" is still reading by when it happened.
    /// </summary>
    Task<IReadOnlyList<InboxMessage>> ListAsync(
        Guid recipientTeamId,
        int skip,
        int take,
        InboxCategory? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many lines the box holds, out of everything in it or of one kind of it.
    ///
    /// The count is the box's own and not the count of the page on screen: a filter that said
    /// "3 lines" beside a page of three while four hundred more of the same kind sit on the
    /// pages after it would be a filter that reports the page it is reading rather than the
    /// box it filters.
    /// </summary>
    Task<int> CountAsync(
        Guid recipientTeamId,
        InboxCategory? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many of each kind the box holds, in one read of the whole box.
    ///
    /// It is a grouping rather than a question per category, and it is asked of the database
    /// rather than counted over the twenty lines on the screen: the filter's own numbers are
    /// the numbers that decide which of the filters is worth pressing, and a count taken from
    /// one page is a count of that page.
    /// </summary>
    Task<IReadOnlyList<InboxCategoryTally>> TallyCategoriesAsync(
        Guid recipientTeamId,
        CancellationToken cancellationToken = default);

    /// <summary>How many messages the manager has not opened. The number on the column.</summary>
    Task<int> UnreadCountAsync(Guid recipientTeamId, CancellationToken cancellationToken = default);

    Task<InboxMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a message of this reference has already been delivered to this club.
    ///
    /// It is the same guard the ledger keeps, for the same reason: a match settled twice, a
    /// prize paid twice, a restart over a weekend — the world retries its work, and a club
    /// told the same news twice reads it as two events.
    /// </summary>
    Task<bool> ExistsWithReferenceAsync(
        Guid recipientTeamId,
        string reference,
        CancellationToken cancellationToken = default);

    Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default);
    void Update(InboxMessage message);
}
