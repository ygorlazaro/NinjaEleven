using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Players;

/// <summary>
/// Represents the stable identity and static attributes of a football player.
/// This entity is intentionally decoupled from season state and club membership
/// so the same player can survive transfers and multiple seasons.
/// </summary>
public class Player
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly BirthDate { get; private set; }

    public Position Position { get; private set; }
    public int Speed { get; private set; }
    public int Accuracy { get; private set; }
    public int Dribbling { get; private set; }
    public int Heading { get; private set; }
    public int Strength { get; private set; }
    public int GoalkeeperPower { get; private set; }
    public int Reflexes { get; private set; }

    /// <summary>
    /// The player's face, as the JSON of a faces.js <c>FaceConfig</c>, or null when he has
    /// none. It belongs to the identity rather than to a season, for the same reason the
    /// birth date does: the same man is recognised in every edition, and a face that changed
    /// on transfer would be a different man. It is optional rather than an empty string
    /// because the column is <c>jsonb</c> and <c>''</c> is not a JSON document — "no face" is
    /// a real state here, and the profile screen has to survive it.
    /// </summary>
    public string? Face { get; private set; }

    private Player() { }

    public static Player Create(
        string name,
        DateOnly birthDate,
        Position position,
        int speed,
        int accuracy,
        int dribbling,
        int heading,
        int strength,
        int goalkeeperPower,
        int reflexes,
        string? face = null)
    {
        return new Player
        {
            Id = Guid.NewGuid(),
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            BirthDate = birthDate,
            Position = position,
            Speed = Clamp(speed),
            Accuracy = Clamp(accuracy),
            Dribbling = Clamp(dribbling),
            Heading = Clamp(heading),
            Strength = Clamp(strength),
            GoalkeeperPower = position == Position.GK ? Clamp(goalkeeperPower) : 0,
            Reflexes = position == Position.GK ? Clamp(reflexes) : 0,
            Face = face,
        };
    }

    /// <summary>
    /// Gives a player the face he was drawn with. It exists because the pool of faces
    /// arrived after the world was seeded, and a player already in the database cannot be
    /// created again — a career that is thrown away to give a man a nose is not worth a
    /// nose.
    /// </summary>
    public void SetFace(string face)
    {
        if (string.IsNullOrWhiteSpace(face))
        {
            throw new ArgumentException("A face cannot be empty.", nameof(face));
        }

        Face = face;
    }

    private static int Clamp(int value) => Math.Max(1, Math.Min(20, value));

    public int CalculateAge(DateOnly? referenceDate = null)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Now);
        int age = today.Year - BirthDate.Year;
        if (today < BirthDate.AddYears(age)) age--;
        return Math.Max(0, age);
    }
}