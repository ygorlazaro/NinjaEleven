using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Users;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A club whose manager has stopped turning up, and what the world does about it.
///
/// <para>
/// These are the real repositories against a real context, because the rule this is testing
/// is mostly about persistence: a dismissal is only a dismissal if the manager's row comes
/// back without a user and the club comes back without a person. A service tested against
/// mocks would prove the arithmetic and nothing about the part that matters.
/// </para>
///
/// <para>
/// The clock is a hand the test turns, because thirty days is the axis the whole rule turns
/// on and a test that could not move it could only ever prove one day of it.
/// </para>
/// </summary>
public class ManagerDormancyTests : IDisposable
{
    private static readonly DateTimeOffset TheFirstDay =
        new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly NinjaElevenDbContext _db;
    private readonly UserRepository _users;
    private readonly ManagerRepository _managers;
    private readonly TeamRepository _teams;
    private readonly HandClock _clock = new(TheFirstDay);

    public ManagerDormancyTests()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"Dormancy-{Guid.NewGuid()}")
            .Options;

        _db = new NinjaElevenDbContext(options);
        _users = new UserRepository(_db);
        _managers = new ManagerRepository(_db);
        _teams = new TeamRepository(_db);

        for (var i = 1; i <= 6; i++)
        {
            _db.Teams.Add(Team.Create($"Clube {i}", $"C{i}", "#0a5", "#fff"));
        }

        _db.SaveChanges();
    }

    private sealed class HandClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void AdvanceTo(DateTimeOffset when) => UtcNow = when;
    }

    private sealed class TestUnitOfWork(NinjaElevenDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);
    }

    private ManagerService Managers() => new(
        _teams,
        _managers,
        new TestUnitOfWork(_db),
        _clock,
        NullLogger<ManagerService>.Instance);

    /// <summary>
    /// Registers an account the way the world does: a club, a manager with the account behind
    /// it, and the club marked as somebody's. Each call takes a club nobody holds, because a
    /// club has one manager and two managers over the same chair is a world this could not
    /// tell apart from a real one.
    /// </summary>
    private async Task<(User Account, Manager Manager, Team Club)> GivenAManagerAsync(
        string email = "manager@example.com")
    {
        var club = _db.Teams
            .Where(t => !_db.Managers.Any(m => m.TeamId == t.Id))
            .OrderBy(t => t.Name)
            .First();
        var account = User.Create(email, "$2b$hash");
        var manager = Manager.Create(club.Id, "Técnico");
        manager.SetUserId(account.Id);
        account.SetManager(manager);

        // The account was created on the world's first day, not on the machine's: a manager
        // who registered here signed in here, and reading his last sign-in off the wall clock
        // would put his last visit in the future of the day the test is about to move to.
        account.RecordLogin(_clock.UtcNow);

        _db.Users.Add(account);
        _db.Managers.Add(manager);
        await _teams.MarkAsManagerClubAsync(club.Id);
        await _db.SaveChangesAsync();

        return (account, manager, club);
    }

    [Fact]
    public async Task AManagerWhoSignedInTodayKeepsHisClub()
    {
        var (_, _, club) = await GivenAManagerAsync();

        var dismissed = await Managers().DismissTheManagersWhoHaveGoneQuietAsync();

        Assert.Empty(dismissed);

        // The manager row is exactly as it was: still behind a person, and the club still
        // marked. A sweep that wrote to a row it was not dismissing would be a rule that
        // cannot be run twice without consequence.
        Assert.NotNull((await _db.Managers.SingleAsync()).UserId);
        Assert.True((await _db.Teams.SingleAsync(t => t.Id == club.Id)).IsManagerClub);
    }

    [Fact]
    public async Task AManagerWhoHasNotSignedInForThirtyDaysStillKeepsHisClub()
    {
        var (_, _, club) = await GivenAManagerAsync();
        _clock.AdvanceTo(TheFirstDay.AddDays(29).AddHours(23));

        var dismissed = await Managers().DismissTheManagersWhoHaveGoneQuietAsync();

        Assert.Empty(dismissed);
        Assert.NotNull((await _db.Managers.SingleAsync()).UserId);
        Assert.True((await _db.Teams.SingleAsync(t => t.Id == club.Id)).IsManagerClub);
    }

    [Fact]
    public async Task AManagerWhoHasNotSignedInForThirtyOneDaysLosesHisClubToTheWorld()
    {
        var (account, _, club) = await GivenAManagerAsync();
        _clock.AdvanceTo(TheFirstDay.AddDays(31));

        var dismissed = await Managers().DismissTheManagersWhoHaveGoneQuietAsync();

        Assert.Equal([club.Name], dismissed);

        // The manager row is the NPC chair now: same row, same name, no person behind it. The
        // club keeps a manager — that is what stops the world from finding a club with nobody
        // in charge of it — and it is no longer a human's.
        var manager = await _db.Managers.SingleAsync();
        Assert.Null(manager.UserId);
        Assert.Equal(club.Id, manager.TeamId);

        // The account is marked, not deleted: a person who signs in again is a manager who
        // needs a club, not a manager who has to register again.
        var stored = await _db.Users.SingleAsync(u => u.Id == account.Id);
        Assert.False(stored.IsActive);
        Assert.Equal(TheFirstDay.AddDays(31), stored.DismissedAt);

        // And it remembers which club, because the manager link no longer can: the dismissal
        // is the act of clearing that link, so this is the only place left that says where the
        // manager was, and a reactivation that cannot see it would hand back the same club.
        Assert.Equal(club.Id, stored.DismissedTeamId);
    }

    [Fact]
    public async Task AClubNobodyIsRunningIsNoLongerMarkedAsSomebodiesClub()
    {
        var (_, _, club) = await GivenAManagerAsync();
        _clock.AdvanceTo(TheFirstDay.AddDays(31));

        await Managers().DismissTheManagersWhoHaveGoneQuietAsync();

        // The mark is what the transfer market reads to decide whose offers wait in an inbox.
        // Left on a club whose manager was let go, it would keep handing that club a manager's
        // inbox for an account that is no longer running it.
        Assert.False((await _db.Teams.SingleAsync(t => t.Id == club.Id)).IsManagerClub);
    }

    [Fact]
    public async Task SigningInKeepsTheSweepFromFiring()
    {
        // Both accounts start on the same day, because that is the only way the two of them
        // can be a question with two answers: the first came back, the second never did.
        await GivenAManagerAsync();
        await GivenAManagerAsync("second@example.com");

        var first = await _db.Users.SingleAsync(u => u.Email == "manager@example.com");
        first.RecordLogin(TheFirstDay.AddDays(25));
        await _db.SaveChangesAsync();

        // One sweep, two accounts, two different answers — which is the point of asking the
        // world rather than asking the clock: "is it the thirtieth of the month" is not a
        // question about a month.
        _clock.AdvanceTo(TheFirstDay.AddDays(31));
        var dismissed = await Managers().DismissTheManagersWhoHaveGoneQuietAsync();

        Assert.Single(dismissed);
        Assert.True((await _db.Users.SingleAsync(u => u.Email == "manager@example.com")).IsActive);
        Assert.False((await _db.Users.SingleAsync(u => u.Email == "second@example.com")).IsActive);
    }

    [Fact]
    public async Task RunningTheSweepTwiceChangesNothingTheSecondTime()
    {
        await GivenAManagerAsync();
        _clock.AdvanceTo(TheFirstDay.AddDays(31));

        var service = Managers();
        Assert.Single(await service.DismissTheManagersWhoHaveGoneQuietAsync());
        Assert.Empty(await service.DismissTheManagersWhoHaveGoneQuietAsync());

        // A dismissal is not a countdown that restarts. An account that has already been
        // dismissed is not overdue again on the next sweep, or the log would report the same
        // club every day for ever.
        var stored = await _db.Users.SingleAsync();
        Assert.Equal(TheFirstDay.AddDays(31), stored.DismissedAt);
    }

    [Fact]
    public async Task AnAccountWithNoManagerIsNotSomethingToSweep()
    {
        // A world can hold an account that never got as far as a club. There is nothing to
        // hand back, and the sweep asking about it would be a question with no answer.
        _db.Users.Add(User.Create("clubless@example.com", "$2b$hash"));
        await _db.SaveChangesAsync();

        _clock.AdvanceTo(TheFirstDay.AddDays(400));

        Assert.Empty(await Managers().DismissTheManagersWhoHaveGoneQuietAsync());
        Assert.True((await _db.Users.SingleAsync(u => u.Email == "clubless@example.com")).IsActive);
    }

    public void Dispose() => _db.Dispose();
}
