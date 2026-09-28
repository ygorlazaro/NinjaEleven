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

    /// <summary>
    /// How old he is, in whole years, and the only thing about time the world keeps about him.
    ///
    /// It is a number rather than a birth date because a birth date is a second fact about the
    /// same thing: it can be turned into an age, and the two can only ever disagree between one
    /// birthday and the next — a man the game calls 24 whose birthday is in November is the
    /// same man in every place a manager reads him, and a world where the club list, the market
    /// and the retirement rule each did their own arithmetic from a date would be a world with
    /// three ages for one player. So there is one number, it is the one every screen reads, and
    /// it moves once a year, at the opening of a season, by exactly one (see
    /// <see cref="AgeUp"/>).
    /// </summary>
    public int Age { get; private set; }

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
    /// none. It belongs to the identity rather than to a season, for the same reason the age
    /// does: the same man is recognised in every edition, and a face that changed on transfer
    /// would be a different man. It is optional rather than an empty string because the column is
    /// <c>jsonb</c> and <c>''</c> is not a JSON document — "no face" is a real state here, and
    /// the profile screen has to survive it.
    /// </summary>
    public string? Face { get; private set; }

    /// <summary>The youngest anybody in a squad is: a season's intake is drawn from here.</summary>
    public const int YoungestAge = 16;

    /// <summary>
    /// The oldest age a man is carried to. Past this he is a number the world stopped counting
    /// at, and the game does not need a man older than every club's reserve list to know he is
    /// finished.
    /// </summary>
    public const int OldestAge = 60;

    private Player() { }

    public static Player Create(
        string name,
        int age,
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
            Age = ClampAge(age),
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
    /// A year older, and this is the only thing in the game that ages anybody.
    ///
    /// It is called once per player when a season opens, before anything else about the new
    /// season is decided, because everything that reads an age reads the age of the world as it
    /// stands: the price a club asks, the value the market reads, and the rule that decides who
    /// is announcing his retirement. A rule applied to a year-old number is a rule applied to
    /// last season's football, and the man who turns thirty-seven this year has to be the man
    /// who announces it this year.
    /// </summary>
    public void AgeUp() => Age = Math.Min(OldestAge, Age + 1);

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

    private static int ClampAge(int age) => Math.Max(YoungestAge, Math.Min(OldestAge, age));
}