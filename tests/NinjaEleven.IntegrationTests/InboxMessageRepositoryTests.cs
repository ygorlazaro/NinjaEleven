using Microsoft.EntityFrameworkCore;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// The box is paged, ordered and deduplicated by EF rather than by assertions in C#, so these
/// are run through a real <see cref="NinjaElevenDbContext"/>: a box that is only ever read by
/// a service in a unit test is a box whose ordering, paging and uniqueness have never been
/// asked of the database that will actually be asked of them.
/// </summary>
public class InboxMessageRepositoryTests : IDisposable
{
    private readonly DbContextOptions<NinjaElevenDbContext> _options;
    private readonly NinjaElevenDbContext _db;
    private readonly InboxMessageRepository _repository;
    private readonly Team _club;

    public InboxMessageRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"inbox-{Guid.NewGuid()}")
            .Options;

        _db = new NinjaElevenDbContext(_options);
        _repository = new InboxMessageRepository(_db);

        _club = Team.Create("Ninja Eleven", "NIN", "#101820", "#38d39f", 70);
        _db.Teams.Add(_club);
    }

    public void Dispose() => _db.Dispose();

    private InboxMessage AMessage(
        string reference,
        DateTimeOffset createdAt,
        InboxCategory category = InboxCategory.Finance) =>
        InboxMessage.Create(
            _club.Id,
            category,
            $"Assunto {reference}",
            "Tesouraria",
            "Conteúdo.",
            reference,
            createdAt: createdAt);

    [Fact]
    public async Task ListAsync_ReturnsTheNewestMessageFirst()
    {
        var start = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _db.InboxMessages.AddRange(
            AMessage("finance:1", start),
            AMessage("finance:2", start.AddHours(1)),
            AMessage("finance:3", start.AddHours(2)));
        await _db.SaveChangesAsync();

        var page = await _repository.ListAsync(_club.Id, 0, 10);

        Assert.Equal(["finance:3", "finance:2", "finance:1"], page.Select(m => m.Reference).ToArray());
    }

    [Fact]
    public async Task ListAsync_PagesTheBox()
    {
        var start = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _db.InboxMessages.AddRange(
            AMessage("finance:1", start),
            AMessage("finance:2", start.AddHours(1)),
            AMessage("finance:3", start.AddHours(2)));
        await _db.SaveChangesAsync();

        var second = await _repository.ListAsync(_club.Id, 1, 1);

        Assert.Equal("finance:2", Assert.Single(second).Reference);
    }

    /// <summary>
    /// A filter narrows which lines are in the page and never reorders them.
    ///
    /// Both halves are the point: a manager who asks for his titles is still reading his mail
    /// by when it arrived, and a filter that grouped them by subject would have put a title
    /// from March above the one from yesterday.
    /// </summary>
    [Fact]
    public async Task ListAsync_ReadsOneCategoryAndKeepsTheOrderOfTheBox()
    {
        var start = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _db.InboxMessages.AddRange(
            AMessage("title:1", start, InboxCategory.Title),
            AMessage("finance:1", start.AddHours(1)),
            AMessage("title:2", start.AddHours(2), InboxCategory.Title));
        await _db.SaveChangesAsync();

        var page = await _repository.ListAsync(_club.Id, 0, 10, InboxCategory.Title);

        Assert.Equal(["title:2", "title:1"], page.Select(m => m.Reference).ToArray());
    }

    /// <summary>
    /// The count is the count of the filter and not the count of the box.
    ///
    /// These are asked with the same narrowing for the same reason: a page of titles beside a
    /// count of everything is a box whose own numbers disagree with its contents, and the
    /// manager is the one reading both.
    /// </summary>
    [Fact]
    public async Task CountAsync_CountsTheCategoryAndNotTheBox()
    {
        var start = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _db.InboxMessages.AddRange(
            AMessage("finance:1", start),
            AMessage("finance:2", start.AddHours(1)),
            AMessage("title:1", start.AddHours(2), InboxCategory.Title));
        await _db.SaveChangesAsync();

        Assert.Equal(3, await _repository.CountAsync(_club.Id));
        Assert.Equal(1, await _repository.CountAsync(_club.Id, InboxCategory.Title));
    }

    /// <summary>
    /// The column of filters is labelled out of this and nothing else.
    ///
    /// It is the whole box and not the page, in the category enum's own order rather than by
    /// how many of each there are — a filter column that reorders itself after every matchday
    /// is one a manager has to find again every time the whistle goes.
    /// </summary>
    [Fact]
    public async Task TallyCategoriesAsync_CountsTheWholeBoxByKind()
    {
        var start = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _db.InboxMessages.AddRange(
            AMessage("title:1", start, InboxCategory.Title),
            AMessage("finance:1", start.AddHours(1)),
            AMessage("finance:2", start.AddHours(2)),
            AMessage("report:1", start.AddHours(3), InboxCategory.MatchReport));
        await _db.SaveChangesAsync();

        var tally = await _repository.TallyCategoriesAsync(_club.Id);

        Assert.Equal(
            [
                (InboxCategory.Finance, 2),
                (InboxCategory.MatchReport, 1),
                (InboxCategory.Title, 1)
            ],
            tally.Select(row => (row.Category, row.Count)).ToArray());
    }

    /// <summary>
    /// The guard the whole feature rests on: a match settled twice, a prize paid twice, or a
    /// process that was down over a weekend must not send a club the same news twice.
    /// </summary>
    [Fact]
    public async Task ExistsWithReferenceAsync_RecognisesAMessageAlreadyDelivered()
    {
        _db.InboxMessages.Add(AMessage("finance:abc", DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();

        Assert.True(await _repository.ExistsWithReferenceAsync(_club.Id, "finance:abc"));
        Assert.False(await _repository.ExistsWithReferenceAsync(_club.Id, "finance:other"));
    }

    [Fact]
    public async Task UnreadCountAsync_CountsOnlyTheLinesTheManagerHasNotOpened()
    {
        var read = AMessage("finance:1", DateTimeOffset.UtcNow);
        read.MarkRead();

        _db.InboxMessages.AddRange(read, AMessage("finance:2", DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _repository.UnreadCountAsync(_club.Id));
        Assert.Equal(2, await _repository.CountAsync(_club.Id));
    }
}
