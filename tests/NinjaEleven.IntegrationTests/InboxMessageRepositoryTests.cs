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

    private InboxMessage AMessage(string reference, DateTimeOffset createdAt) =>
        InboxMessage.Create(
            _club.Id,
            InboxCategory.Finance,
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
