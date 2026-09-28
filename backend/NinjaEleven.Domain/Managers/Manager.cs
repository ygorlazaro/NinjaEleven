namespace NinjaEleven.Domain.Managers;

using NinjaEleven.Domain.Users;

/// <summary>
/// The manager of a club: the human decision-maker whose career the game is.
///
/// A manager is born the day the career starts — when a club is chosen — and a club has one
/// manager and not many: there is not a second career to be had on the same shirt. The name is
/// the one fact that cannot be a row anywhere else and is therefore kept here, so changing it
/// is a change to this row and nothing else.
/// </summary>
public class Manager
{
    public Guid Id { get; private set; }

    /// <summary>The name the manager chose when the career began, editable later.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The club the manager has taken charge of, for as long as the career lasts.</summary>
    public Guid TeamId { get; private set; }

    /// <summary>
    /// The user account that owns this career, or null while the club is still NPC-controlled.
    /// A user taking over a club sets this; it is the mark that says "a person answers for this
    /// club", read alongside <see cref="Team.IsManagerClub"/>.
    /// </summary>
    public Guid? UserId { get; private set; }
    public User? User { get; private set; }

    /// <summary>When the career began, for the opening of the manager's history.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    private Manager() { }

    public static Manager Create(Guid teamId, string name)
    {
        if (teamId == Guid.Empty)
            throw new ArgumentException("A manager must be tied to a club.", nameof(teamId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A manager needs a name.", nameof(name));

        return new Manager
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            TeamId = teamId,
            StartedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Links this manager to the user who owns the career.</summary>
    public void SetUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A user id cannot be empty.", nameof(userId));

        UserId = userId;
    }

    /// <summary>Clears the user link, returning the club to NPC control.</summary>
    public void ClearUserId() => UserId = null;

    public void SetName(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A manager needs a name.", nameof(name))
            : name.Trim();
    }
}
