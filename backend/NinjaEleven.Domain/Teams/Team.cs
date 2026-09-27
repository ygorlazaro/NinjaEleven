namespace NinjaEleven.Domain.Teams;

/// <summary>
/// Represents a football club. The squad is NOT stored as a list inside Team;
/// the link between player and club is expressed through TeamMembership.
/// </summary>
public class Team
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public string PrimaryColor { get; private set; } = string.Empty;
    public string SecondaryColor { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public Guid? StadiumId { get; private set; }
    public Stadium? Stadium { get; private set; }

    private Team() { }

    public static Team Create(
        string name,
        string shortName,
        string primaryColor,
        string secondaryColor,
        int rating)
    {
        return new Team
        {
            Id = Guid.NewGuid(),
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            ShortName = string.IsNullOrEmpty(shortName) ? name : shortName,
            PrimaryColor = string.IsNullOrEmpty(primaryColor) ? "#3a6ea5" : primaryColor,
            SecondaryColor = string.IsNullOrEmpty(secondaryColor) ? "#1f3c56" : secondaryColor,
            Rating = Math.Max(1, Math.Min(100, rating)),
        };
    }

    public void SetStadium(Stadium stadium)
    {
        Stadium = stadium;
        StadiumId = stadium?.Id;
    }
}