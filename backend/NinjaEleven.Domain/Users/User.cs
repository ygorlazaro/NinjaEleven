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

    /// <summary>
    /// When the account last signed in, or null while it never has.
    ///
    /// This is the fact a dormancy rule is measured against, and it is a fact about the
    /// account rather than about the club: a person who stops opening the game has stopped
    /// being a manager whether or not anybody noticed on the day.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>
    /// Whether this account still holds a club.
    ///
    /// False means the account was dismissed — the club went back to the world and an NPC
    /// manager took the chair — and the account itself survives, because a person who comes
    /// back is owed a club rather than an apology.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>When the account was dismissed, or null while it has never been.</summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    /// <summary>
    /// The club the account was dismissed from, or null while it has never been dismissed.
    ///
    /// <para>
    /// The club cannot be recovered from the manager link, and that is not an oversight: a
    /// dismissal is exactly the act of clearing that link, so the row on the other side stops
    /// pointing at anybody and a navigation built on it returns nothing. Without this column
    /// a returning manager would be handed a club by a rule that could not see the one they
    /// were dismissed from, and the club it picked would be that same club — a dismissal with
    /// no consequence, decided by a detail of how the join works.
    /// </para>
    /// </summary>
    public Guid? DismissedTeamId { get; private set; }

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
            CreatedAt = DateTimeOffset.UtcNow,
            // An account that has just been created has just been here. Leaving LastLoginAt
            // null would say the opposite, and a dormancy rule reading null as "never came
            // back" would dismiss a manager for the crime of registering on the right day.
            LastLoginAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    /// <summary>Records that the account signed in.</summary>
    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    /// <summary>
    /// Whether the account has been gone for longer than a manager may be away.
    ///
    /// The clock runs from the last sign-in, and from the day the account was created while
    /// it has never signed in at all — an account that registered and then closed the tab
    /// has been away for exactly as long as it has existed.
    /// </summary>
    public bool HasBeenAwayFor(TimeSpan howLong, DateTimeOffset now) =>
        now - (LastLoginAt ?? CreatedAt) >= howLong;

    /// <summary>
    /// Marks the account as dismissed: the club is gone and an NPC manager has the chair.
    ///
    /// The account is not deleted. A person who signs in again is a manager who needs a club,
    /// and deleting the row would turn a return into a re-registration with a second password
    /// and a second email that happens to be the first one.
    ///
    /// The club is named because the manager link is about to stop answering, and this is the
    /// last moment at which the account still knows where it was.
    /// </summary>
    public void Dismiss(DateTimeOffset now, Guid teamId)
    {
        if (teamId == Guid.Empty)
            throw new ArgumentException("A dismissal is from a club.", nameof(teamId));

        IsActive = false;
        DismissedAt = now;
        DismissedTeamId = teamId;
    }

    /// <summary>
    /// Brings a dismissed account back to life, which is what signing in does to one.
    /// </summary>
    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        DismissedAt = null;
        DismissedTeamId = null;
        RecordLogin(now);
    }

    /// <summary>Links this user to the manager they created when starting a career.</summary>
    public void SetManager(Manager manager)
    {
        Manager = manager;
    }
}
