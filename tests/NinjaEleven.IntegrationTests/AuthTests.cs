using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
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
    private readonly NinjaElevenDbContext _db;
    private readonly AuthService _auth;

    public AuthTests()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"Auth-{Guid.NewGuid()}")
            .Options;

        _db = new NinjaElevenDbContext(options);

        // A single team exists so the register/link flow has something to claim.
        var team = Team.Create("Esporte Clube Riachuelo", "Riachuelo", "#0a5", "#fff");
        _db.Teams.Add(team);
        _db.SaveChanges();

        var users = new UserRepository(_db);
        var usersRepo = users;
        var managers = new ManagerRepository(_db);
        var teams = new TeamRepository(_db);
        var passwordHasher = new BcryptPasswordHasher();
        var unitOfWork = new EfUnitOfWork(_db);

        _auth = new AuthService(
            usersRepo,
            managers,
            teams,
            passwordHasher,
            unitOfWork,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterCreatesUserAndPasswordHash()
    {
        var result = await _auth.RegisterAsync(
            "test@example.com", "SenhaSegura123", null, null, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal("test@example.com", result.Email);

        // The password hash is never empty: BCrypt produces a $2b$ prefix, never the raw password.
        var user = await _db.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual("SenhaSegura123", user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
    }

    [Fact]
    public async Task LoginReturnsResultWithUserIdentity()
    {
        await _auth.RegisterAsync("login@example.com", "Senha123456", null, null, CancellationToken.None);

        var result = await _auth.LoginAsync("login@example.com", "Senha123456", CancellationToken.None);

        Assert.Equal("login@example.com", result.Email);
        Assert.Null(result.TeamId);
    }

    [Fact]
    public async Task LoginWithWrongPasswordFails()
    {
        await _auth.RegisterAsync("wrongpass@example.com", "Senha123456", null, null, CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _auth.LoginAsync("wrongpass@example.com", "senhaErrada", CancellationToken.None));
    }

    [Fact]
    public async Task ChangePasswordUpdatesTheHash()
    {
        var result = await _auth.RegisterAsync(
            "change@example.com", "Senha123456", null, null, CancellationToken.None);

        await _auth.ChangePasswordAsync(
            result.UserId, "Senha123456", "NovaSenha789", CancellationToken.None);

        // The old password no longer works:
        await Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _auth.LoginAsync("change@example.com", "Senha123456", CancellationToken.None));

        // The new password does:
        var newResult = await _auth.LoginAsync("change@example.com", "NovaSenha789", CancellationToken.None);
        Assert.Equal("change@example.com", newResult.Email);
    }

    [Fact]
    public async Task LinkManagerCreatesManagerAndMarksClub()
    {
        var result = await _auth.RegisterAsync(
            "link@example.com", "Senha123456", null, null, CancellationToken.None);

        var team = await _db.Teams.FirstAsync();
        await _auth.LinkManagerAsync(result.UserId, team.Id, "Linkado", CancellationToken.None);

        var manager = await _db.Managers.FirstAsync();
        Assert.Equal("Linkado", manager.Name);
        Assert.Equal(result.UserId, manager.UserId);

        // The manager is linked to the user:
        var managerOfUser = await _auth.GetManagerByUserIdAsync(result.UserId, CancellationToken.None);
        Assert.NotNull(managerOfUser);
        Assert.Equal("Linkado", managerOfUser!.Name);
    }

    [Fact]
    public async Task LinkManagerTwiceFails()
    {
        var result = await _auth.RegisterAsync(
            "double@example.com", "Senha123456", null, null, CancellationToken.None);

        var team = await _db.Teams.FirstAsync();
        await _auth.LinkManagerAsync(result.UserId, team.Id, "Primeiro", CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(async () =>
            await _auth.LinkManagerAsync(result.UserId, team.Id, "Segundo", CancellationToken.None));
    }
}
