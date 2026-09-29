using NinjaEleven.Domain.Inbox;

namespace NinjaEleven.Application.Repositories;

public interface IInboxMessageRepository
{
    /// <summary>
    /// A page of a club's box, newest first.
    ///
    /// The order is the order the messages arrived and nothing else is applied to it. A box
    /// sorted by category would put today's result above a transfer that was agreed an hour
    /// ago, and a manager reads the top of his own mail the way he reads the top of a paper:
    /// by when it got there.
    /// </summary>
    Task<IReadOnlyList<InboxMessage>> ListAsync(
        Guid recipientTeamId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid recipientTeamId, CancellationToken cancellationToken = default);

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
