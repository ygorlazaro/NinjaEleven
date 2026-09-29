using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class InboxMessageRepository : IInboxMessageRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public InboxMessageRepository(NinjaElevenDbContext dbContext) => _dbContext = dbContext;

    /// <summary>
    /// A club's box, a page at a time.
    ///
    /// Paging rather than reading it whole is the point: a club plays a season of football and
    /// pays a wage bill every matchday, so the box is thousands of lines long by the end of
    /// one, and a screen that could only be shown its first hundred would be a screen that
    /// stops working in the second month.
    /// </summary>
    public async Task<IReadOnlyList<InboxMessage>> ListAsync(
        Guid recipientTeamId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.InboxMessages
            .AsNoTracking()
            .Where(message => message.RecipientTeamId == recipientTeamId)
            // Newest first, and the id is the tie-breaker: two messages written in the same
            // tick have the same timestamp, and a page that can order them either way is a
            // page whose top line changes from refresh to refresh.
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(
        Guid recipientTeamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.InboxMessages
            .AsNoTracking()
            .CountAsync(message => message.RecipientTeamId == recipientTeamId, cancellationToken);

    public async Task<int> UnreadCountAsync(
        Guid recipientTeamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.InboxMessages
            .AsNoTracking()
            .CountAsync(message => message.RecipientTeamId == recipientTeamId && !message.IsRead, cancellationToken);

    public async Task<InboxMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.InboxMessages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);

    public async Task<bool> ExistsWithReferenceAsync(
        Guid recipientTeamId,
        string reference,
        CancellationToken cancellationToken = default) =>
        await _dbContext.InboxMessages
            .AsNoTracking()
            .AnyAsync(
                message => message.RecipientTeamId == recipientTeamId && message.Reference == reference,
                cancellationToken);

    public async Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default) =>
        await _dbContext.InboxMessages.AddAsync(message, cancellationToken);

    public void Update(InboxMessage message) => _dbContext.InboxMessages.Update(message);
}
