using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Managers;

namespace NinjaEleven.Domain.Users;

/// <summary>
/// An account that can log in and take charge of a club. A user is the digital identity
/// of the person who plays the game: the email is how they sign in, the password hash proves
/// it is them, and the link to the manager row is the club they have taken over.
///
/// A user has zero or one manager — the career begins when the user picks a club and a name
/// for their coach. Until that moment they have no manager row; after it they do, and the
/// link lives on the manager side as <see cref="Manager.UserId"/>.
/// </summary>
public class User
{
    public Guid Id { get; private set; }

    /// <summary>The email the user signs in with; unique across all accounts.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>A BCrypt hash of the user's password.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>The manager this user has created, or null while they have not yet picked a club.</summary>
    public Manager? Manager { get; private set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException(
                "InvalidEmail", "An email address is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainValidationException(
                "InvalidPassword", "A password is required.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    /// <summary>Links this user to the manager they created when starting a career.</summary>
    public void SetManager(Manager manager)
    {
        Manager = manager;
    }
}
