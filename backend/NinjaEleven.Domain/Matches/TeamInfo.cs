
namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Lightweight team description used inside a match context.
/// Mirrors the frontend TeamInfo contract (id, name, shortName, colors, rating).
/// </summary>
public class TeamInfo
{
    public Guid Id { get; }
    public string Name { get; }
    public string ShortName { get; }
    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public int Rating { get; }

    public TeamInfo(
        Guid id,
        string name,
        string shortName,
        string primaryColor,
        string secondaryColor,
        int rating)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ShortName = shortName ?? string.Empty;
        PrimaryColor = primaryColor ?? string.Empty;
        SecondaryColor = secondaryColor ?? string.Empty;
        Rating = rating;
    }
}