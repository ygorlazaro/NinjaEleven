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
        InboxCategory? category = null,
        CancellationToken cancellationToken = default) =>
        await Of(_dbContext.InboxMessages.Where(message => message.RecipientTeamId == recipientTeamId), category)
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
        InboxCategory? category = null,
        CancellationToken cancellationToken = default) =>
        await Of(_dbContext.InboxMessages.Where(message => message.RecipientTeamId == recipientTeamId), category)
            .AsNoTracking()
            .CountAsync(cancellationToken);

    /// <summary>
    /// The box counted by kind, in one grouping.
    ///
    /// The kinds come back in the order the column is written in — the enum's own order — and
    /// not by how many of each there are: a filter whose rows reorder themselves every time a
    /// message arrives is a filter a manager has to find again after every matchday, and the
    /// order he learns once is the order the box's own categories are declared in.
    /// </summary>
    public async Task<IReadOnlyList<InboxCategoryTally>> TallyCategoriesAsync(
        Guid recipientTeamId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.InboxMessages
            .AsNoTracking()
            .Where(message => message.RecipientTeamId == recipientTeamId)
            .GroupBy(message => message.Category)
            .Select(group => new { Category = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(row => row.Category)
            .Select(row => new InboxCategoryTally(row.Category, row.Count))
            .ToList();
    }

    /// <summary>
    /// The lines of one club's box, narrowed to a kind or not.
    ///
    /// It is a method rather than a lambda written twice so that a filter can never be
    /// understood by the page and not by the count beside it: the two are the same query with
    /// the same narrowing, and a page of "Partida" beside a count of the whole box is a box
    /// whose numbers disagree with its contents.
    /// </summary>
    private static IQueryable<InboxMessage> Of(IQueryable<InboxMessage> box, InboxCategory? category) =>
        category is null ? box : box.Where(message => message.Category == category.Value);

    /// <summary>
    /// How much of the whole box the manager has not opened.
    ///
    /// It takes no category on purpose, and it is the same number the column's badge asks for:
    /// "3 mensagens novas" is a fact about his mail, not about the slice of it he happens to be
    /// reading. A badge that shrank to the filter's own unread would be a badge counting two
    /// different things depending on which screen he was looking at.
    /// </summary>
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
