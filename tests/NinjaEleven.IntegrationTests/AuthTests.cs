using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
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
    private AuthService CreateAuthService(NinjaElevenDbContext db)
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
            teams);
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
}