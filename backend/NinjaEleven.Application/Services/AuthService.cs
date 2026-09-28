using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Users;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Handles account creation, login, and password changes. This is the only service that
/// writes user rows, so the rules around them stay in one place.
/// </summary>
public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IManagerRepository _managers;
    private readonly ITeamRepository _teams;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IManagerRepository managers,
        ITeamRepository teams,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _users = users;
        _managers = managers;
        _teams = teams;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user and, if a team is selected, creates the manager that starts the career.
    /// A user without a team is an account that has not yet picked a club; they will be redirected
    /// to the club selector on next login.
    /// </summary>
    public async Task<AuthResult> RegisterAsync(
        string email,
        string password,
        string? coachName,
        Guid? teamId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("InvalidEmail", "An email address is required.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new DomainValidationException(
                "WeakPassword", "The password must be at least six characters long.");

        var existing = await _users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
            throw new DomainValidationException(
                "EmailAlreadyInUse", $"The email '{email}' is already registered.");

        var passwordHash = _passwordHasher.Hash(password);
        var user = User.Create(email, passwordHash);

        if (teamId.HasValue && teamId != Guid.Empty)
        {
            var team = await _teams.GetAsync(teamId.Value, cancellationToken)
                ?? throw new EntityNotFoundException("Team", teamId.Value);

            if (coachName is null or { Length: 0 })
                throw new DomainValidationException(
                    "CoachNameRequired", "A coach name is required when selecting a club.");

            var manager = Manager.Create(teamId.Value, coachName);
            manager.SetUserId(user.Id);
            user.SetManager(manager);

            await _teams.MarkAsManagerClubAsync(teamId.Value, cancellationToken);
            await _managers.AddAsync(manager, cancellationToken);
        }

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User registered: {Email} with coach {CoachName}.",
            email,
            coachName ?? "(no club yet)");

        return new AuthResult(user.Id, user.Email, user.Manager?.TeamId, coachName);
    }

    /// <summary>
    /// Signs a user in by email and password, returning the claims needed for a JWT.
    /// </summary>
    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
            throw new DomainValidationException(
                "InvalidCredentials", "The email or password is incorrect.");

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new DomainValidationException(
                "InvalidCredentials", "The email or password is incorrect.");

        var coachName = user.Manager?.Name;
        var teamId = user.Manager?.TeamId;

        _logger.LogInformation("User logged in: {Email}.", email);

        return new AuthResult(user.Id, user.Email, teamId, coachName);
    }

    /// <summary>
    /// Changes the password of the authenticated user.
    /// </summary>
    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
            throw new DomainValidationException(
                "InvalidCredentials", "The current password is incorrect.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new DomainValidationException(
                "WeakPassword", "The new password must be at least six characters long.");

        user.SetPasswordHash(_passwordHasher.Hash(newPassword));
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {Email}.", user.Email);
    }

    /// <summary>
    /// Returns the list of clubs that have no human manager, so a new user can claim one.
    /// </summary>
    public async Task<IReadOnlyList<Team>> GetAvailableClubsAsync(
        CancellationToken cancellationToken = default) =>
        await _teams.ListClubsWithoutManagerAsync(cancellationToken);

    /// <summary>
    /// The manager linked to a user, if any. Used to return the manager detail after linking.
    /// </summary>
    public async Task<Manager?> GetManagerByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _managers.ListByUserIdAsync(userId, cancellationToken);

    /// <summary>
    /// Links an existing user to a newly created manager, completing their career setup.
    /// A manager can only be created once per user; the link is permanent.
    /// </summary>
    public async Task LinkManagerAsync(
        Guid userId,
        Guid teamId,
        string coachName,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        if (user.Manager is not null)
            throw new DomainValidationException(
                "ManagerAlreadyExists", "You already have a club. A user can only control one team.");

        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", teamId);

        if (await _managers.GetByTeamAsync(teamId, cancellationToken) is not null)
            throw new DomainValidationException(
                "TeamAlreadyControlled", "That club already has a manager.");

        var manager = Manager.Create(teamId, coachName);
        manager.SetUserId(user.Id);
        user.SetManager(manager);

        await _teams.MarkAsManagerClubAsync(teamId, cancellationToken);
        await _managers.AddAsync(manager, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {Email} took charge of {TeamName} as {CoachName}.",
            user.Email,
            team.Name,
            coachName);
    }
}

/// <summary>The result of a login or registration: enough to mint a JWT.</summary>
public record AuthResult(
    Guid UserId,
    string Email,
    Guid? TeamId,
    string? CoachName);
