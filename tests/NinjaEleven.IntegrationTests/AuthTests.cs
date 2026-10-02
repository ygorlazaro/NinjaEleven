using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Users;
using NinjaEleven.Infrastructure;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using NinjaEleven.Infrastructure.Security;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// Registration, login and password change: the three things the auth flow must do before
/// a manager ever sees a pitch. The tests use the real repositories and the real password
/// hasher against an in-memory database, so the wiring — not the mocks — is what is tested.
/// </summary>
public class AuthTests
{
    /// <summary>
    /// A clock the test moves. The dormancy rule is measured in days, so a test that has to
    /// wait thirty of them is a test that never runs; this one is asked what day it is.
    /// </summary>
    private sealed class HandClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private AuthService CreateAuthService(NinjaElevenDbContext db, IClock? clock = null)
    {
        var users = new UserRepository(db);
        var managers = new ManagerRepository(db);
        var teams = new TeamRepository(db);
        var competitions = new CompetitionRepository(db);
        var seasons = new SeasonRepository(db);
        var cupTies = new CupTieRepository(db);
        var standings = new StandingsService(
            new RoundRepository(db),
            new FixtureRepository(db),
            new MatchRepository(db),
            competitions,
            teams,
            new SquadStrengthReader(teams));
        var passwordHasher = new BcryptPasswordHasher();
        var unitOfWork = new EfUnitOfWork(db);

        return new AuthService(
            users,
            managers,
            teams,
            competitions,
            seasons,
            cupTies,
            standings,
            passwordHasher,
            unitOfWork,
            clock ?? new HandClock(DateTimeOffset.UtcNow),
            NullLogger<AuthService>.Instance);
    }

    private NinjaElevenDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"Auth-{Guid.NewGuid()}")
            .Options;

        var db = new NinjaElevenDbContext(options);

        // Create multiple teams so multiple tests can each claim one
        for (int i = 1; i <= 20; i++)
        {
            var team = Team.Create($"Clube de Teste {i}", $"Teste {i}", "#0a5", "#fff");
            db.Teams.Add(team);
        }

        // Create a current season for club auto-assignment
        var season = Season.Create(1, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        season.Start();
        db.Seasons.Add(season);

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task RegisterCreatesUserAndPasswordHash()
    {
        var db = CreateDbContext();
        var auth = CreateAuthService(db);

        var result = await auth.RegisterAsync(
            "test@example.com", "SenhaSegura123", null, null, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal("test@example.com", result.Email);

        // The password hash is never empty: BCrypt produces a $2b$ prefix, never the raw password.
        var user = await db.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual("SenhaSegura123", user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
    }

    [Fact]
    public async Task LoginReturnsResultWithUserIdentity()
    {
        var db = CreateDbContext();
        var auth = CreateAuthService(db);

        await auth.RegisterAsync("login@example.com", "Senha123456", null, null, CancellationToken.None);

        var result = await auth.LoginAsync("login@example.com", "Senha123456", CancellationToken.None);

        Assert.Equal("login@example.com", result.Email);
        Assert.NotNull(result.TeamId); // Club is auto-assigned on registration
    }

    [Fact]
    public async Task LoginWithWrongPasswordFails()
    {
        var db = CreateDbContext();
        var auth = CreateAuthService(db);

        await auth.RegisterAsync("wrongpass@example.com", "Senha123456", null, null, CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(async () =>
            await auth.LoginAsync("wrongpass@example.com", "senhaErrada", CancellationToken.None));
    }

    [Fact]
    public async Task ChangePasswordUpdatesTheHash()
    {
        var db = CreateDbContext();
        var auth = CreateAuthService(db);

        var result = await auth.RegisterAsync(
            "change@example.com", "Senha123456", null, null, CancellationToken.None);

        await auth.ChangePasswordAsync(
            result.UserId, "Senha123456", "NovaSenha789", CancellationToken.None);

        // The old password no longer works:
        await Assert.ThrowsAsync<DomainValidationException>(async () =>
            await auth.LoginAsync("change@example.com", "Senha123456", CancellationToken.None));

        // The new password does:
        var newResult = await auth.LoginAsync("change@example.com", "NovaSenha789", CancellationToken.None);
        Assert.Equal("change@example.com", newResult.Email);
    }

    [Fact]
    public async Task SigningInRecordsTheSignIn()
    {
        var db = CreateDbContext();
        var clock = new HandClock(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var auth = CreateAuthService(db, clock);

        await auth.RegisterAsync("stamp@example.com", "Senha123456", null, null, CancellationToken.None);

        clock.UtcNow = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);
        await auth.LoginAsync("stamp@example.com", "Senha123456", CancellationToken.None);

        // The dormancy rule is measured from this date, so a login that does not write it is a
        // manager the world cannot tell apart from one who has never come back.
        var account = await db.Users.SingleAsync(u => u.Email == "stamp@example.com");
        Assert.Equal(clock.UtcNow, account.LastLoginAt);
    }

    [Fact]
    public async Task ADismissedAccountThatSignsInAgainIsGivenADifferentClub()
    {
        var db = CreateDbContext();
        var clock = new HandClock(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var auth = CreateAuthService(db, clock);

        var registered = await auth.RegisterAsync(
            "return@example.com", "Senha123456", null, null, CancellationToken.None);
        var firstClubId = registered.TeamId!.Value;

        // The account is dismissed, the way the sweep does it.
        var account = await db.Users.Include(u => u.Manager).SingleAsync(u => u.Email == "return@example.com");
        account.Dismiss(clock.UtcNow.AddDays(31), firstClubId);
        var manager = await db.Managers.SingleAsync(m => m.UserId == account.Id);
        manager.ClearUserId();
        await db.SaveChangesAsync();

        var back = await auth.LoginAsync("return@example.com", "Senha123456", CancellationToken.None);

        Assert.True(back.WasDismissed);
        Assert.True(account.IsActive);
        Assert.Null(account.DismissedAt);

        // The one rule the reactivation cannot bend: not the club they were just dismissed
        // from. Handing it straight back would answer "you were fired" with the same job.
        Assert.NotNull(back.TeamId);
        Assert.NotEqual(firstClubId, back.TeamId);
    }

    [Fact]
    public async Task ADismissedAccountThatSignsInAgainRunsTheClubItWasGiven()
    {
        var db = CreateDbContext();
        var clock = new HandClock(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var auth = CreateAuthService(db, clock);

        var registered = await auth.RegisterAsync(
            "backin@example.com", "Senha123456", "O Técnico", null, CancellationToken.None);

        var account = await db.Users.Include(u => u.Manager).SingleAsync(u => u.Email == "backin@example.com");
        account.Dismiss(clock.UtcNow.AddDays(31), registered.TeamId!.Value);
        (await db.Managers.SingleAsync(m => m.UserId == account.Id)).ClearUserId();
        await db.SaveChangesAsync();

        var back = await auth.LoginAsync("backin@example.com", "Senha123456", CancellationToken.None);

        // A club has one manager, so the NPC chair at the new club steps aside for the person
        // — and the person is behind it afterwards, which is what the mark on the club and the
        // registry of managed clubs both read.
        var newManager = await db.Managers.SingleAsync(m => m.UserId == account.Id);
        Assert.Equal(back.TeamId, newManager.TeamId);
        Assert.NotEqual(registered.TeamId, newManager.TeamId);
        Assert.True((await db.Teams.SingleAsync(t => t.Id == newManager.TeamId)).IsManagerClub);
    }

    [Fact]
    public async Task AnAccountThatWasNeverDismissedIsNotToldItCameBack()
    {
        var db = CreateDbContext();
        var auth = CreateAuthService(db);

        await auth.RegisterAsync("ordinary@example.com", "Senha123456", null, null, CancellationToken.None);
        var result = await auth.LoginAsync("ordinary@example.com", "Senha123456", CancellationToken.None);

        // The flag is what tells a client to drop the club it had cached, so an ordinary sign-in
        // carrying it would make every login look like a change of club.
        Assert.False(result.WasDismissed);
    }

    [Fact]
    public async Task ADismissedAccountIsNotGivenTheOnlyClubInTheWorld()
    {
        var db = CreateDbContext();
        var clock = new HandClock(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var auth = CreateAuthService(db, clock);

        var registered = await auth.RegisterAsync(
            "onlyclub@example.com", "Senha123456", null, null, CancellationToken.None);

        // A world with one club cannot honour "a different one". The exclusion is a courtesy to
        // a world of several clubs, so a world that has run out of them is offered the club
        // back rather than leaving a returning manager with nothing at all — a dismissal that
        // cannot be honoured is not a reason to lock the person out of the game.
        var account = await db.Users.Include(u => u.Manager).SingleAsync(u => u.Email == "onlyclub@example.com");
        account.Dismiss(clock.UtcNow.AddDays(31), registered.TeamId!.Value);
        (await db.Managers.SingleAsync(m => m.UserId == account.Id)).ClearUserId();
        await db.SaveChangesAsync();

        // Every other club goes to a person, which is the only thing that takes a club out of
        // the pool: an NPC chair is not taken, because an NPC is nobody.
        foreach (var other in await db.Teams.Where(t => t.Id != registered.TeamId).ToListAsync())
        {
            var holder = User.Create($"holder-{other.Id}@example.com", "$2b$hash");
            holder.RecordLogin(clock.UtcNow);
            db.Users.Add(holder);

            var theirs = Manager.Create(other.Id, "Outro Técnico");
            theirs.SetUserId(holder.Id);
            holder.SetManager(theirs);
            db.Managers.Add(theirs);
        }

        await db.SaveChangesAsync();

        var back = await auth.LoginAsync("onlyclub@example.com", "Senha123456", CancellationToken.None);

        Assert.True(back.WasDismissed);
        Assert.Equal(registered.TeamId, back.TeamId);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task AComingBackManagerKeepsTheNameTheyChose()
    {
        var db = CreateDbContext();
        var clock = new HandClock(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var auth = CreateAuthService(db, clock);

        var registered = await auth.RegisterAsync(
            "named@example.com", "Senha123456", "Zé do Apito", null, CancellationToken.None);

        var account = await db.Users.Include(u => u.Manager).SingleAsync(u => u.Email == "named@example.com");
        account.Dismiss(clock.UtcNow.AddDays(31), registered.TeamId!.Value);
        (await db.Managers.SingleAsync(m => m.UserId == account.Id)).ClearUserId();
        await db.SaveChangesAsync();

        var back = await auth.LoginAsync("named@example.com", "Senha123456", CancellationToken.None);

        // The name lives on the manager row, and a dismissal clears that row's link to the
        // account — so the name has to be read off the club they were let go from. Read after,
        // or not read at all, a person who comes back to a new club is handed a new career
        // under a name they never picked.
        Assert.Equal("Zé do Apito", back.CoachName);
        Assert.Equal(
            "Zé do Apito",
            (await db.Managers.SingleAsync(m => m.UserId == account.Id)).Name);
    }
}
