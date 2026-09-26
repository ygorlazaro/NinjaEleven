using FootballManager.Domain.Enums;

namespace FootballManager.Domain.Players;

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
        int reflexes)
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
        };
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