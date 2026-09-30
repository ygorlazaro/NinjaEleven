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
    /// How much he has in the tank: on the same 1..100 scale as the other attributes, and
    /// deliberately a body fact rather than a season's.
    ///
    /// <para>
    /// It belongs here and not on <see cref="PlayerSeasonState"/> because it is not a form.
    /// A man who is a marathoner next season was a marathoner this one, and a market that
    /// read his stamina from last season's state would be reading a number about the window
    /// he happened to be in rather than about the man. It is also the only attribute that is
    /// about the ninety minutes rather than about the game: the other seven answer "how well
    /// does he play", and this one answers "how much football is left in him at minute
    /// eighty-five" — which is a different question and the one a squad is rotated on.
    /// </para>
    ///
    /// <para>
    /// It is both the tank and the refill, deliberately. A man with more stamina empties more
    /// slowly <i>and</i> puts back together faster, because those are two ends of one fact
    /// and an attribute that bought only one of them would be half an attribute: a player
    /// who is hard to tire and slow to mend is a worse player than either.
    /// </para>
    /// </summary>
    public int Stamina { get; private set; }

    /// <summary>
    /// The ceiling on this man: the best reading of him the world expects he will ever
    /// produce, on the same 1..100 scale as the attributes.
    ///
    /// <para>
    /// It is a ceiling on the <em>reading</em> rather than on each attribute, and the
    /// distinction is the whole of <see cref="DevelopmentRules"/>. A per-attribute ceiling
    /// has to answer "why has he stopped improving at heading", and the only honest answer
    /// is a different number for every man, which is a number the world would have to
    /// invent. One number, weighted by the position he plays, says the same thing and can be
    /// argued about: this man will be a good defender, not a good everything.
    /// </para>
    ///
    /// <para>
    /// It belongs to the identity rather than to a season, for the same reason the age and
    /// the face do. A seventeen-year-old's potential is a fact about the seventeen-year-old,
    /// and a market that read it from last season's state would be reading a forecast of the
    /// man and finding that the forecast had changed because the window had.
    /// </para>
    /// </summary>
    public int Potential { get; private set; }

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

    /// <param name="stamina">
    /// How much he has in the tank, on the 1..100 scale. It is optional and it defaults to
    /// <see cref="StaminaReference"/> because a player created without a stated stamina is a
    /// player created at the middle of the scale — the same default as a player with no face
    /// is a player whose face is simply not drawn yet. Every real creation path — the seeder,
    /// the market, a career's first world — says what it wants.
    /// </param>
    /// <param name="potential">
    /// The ceiling on his reading, on the same 1..100 scale, defaulting to the middle of it
    /// for the same reason and with the same caveat. A player created at the reference
    /// potential is a player who is at his ceiling: he will not grow, which is a real state
    /// and a perfectly good one for a man the world is drawing to fill a squad list.
    /// </param>
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
        string? face = null,
        int stamina = StaminaReference,
        int potential = PotentialReference)
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
            Stamina = Clamp(stamina),
            Potential = Clamp(potential),
            Face = face,
        };
    }

    /// <summary>
    /// The stamina of an average body, and the point the stamina curve is measured from.
    ///
    /// <para>
    /// It is the same number <see cref="NinjaEleven.Domain.Matches.AttributeScale.Reference"/>
    /// is for the other seven attributes, and it is here rather than there because a player
    /// is created without the match engine in scope: the seeder and the market both make
    /// players, and neither of them references a match type.
    /// </para>
    /// </summary>
    public const int StaminaReference = 50;

    /// <summary>
    /// The ceiling of an average player, and the point the development curve is measured
    /// from. A player created at it is a player who has arrived.
    /// </summary>
    public const int PotentialReference = 50;

    /// <summary>
    /// A year older, a year of a career, and this is the only thing in the game that ages
    /// anybody.
    ///
    /// <para>
    /// It is called once per player when a season opens, before anything else about the new
    /// season is decided, because everything that reads an age reads the age of the world as
    /// it stands: the price a club asks, the value the market reads, and the rule that decides
    /// who is announcing his retirement. A rule applied to a year-old number is a rule applied
    /// to last season's football, and the man who turns thirty-seven this year has to be the
    /// man who announces it this year.
    /// </para>
    ///
    /// <para>
    /// Development is here and not in the caller because a year that moves the number without
    /// moving the man is a year nobody would believe in. A manager opening a season and
    /// finding last year's squad rated exactly as it was, with every man a year older and
    /// none of them any different, would be reading a world where age is a label. It is one
    /// call rather than two for the same reason the retirement announcement is one call: a
    /// caller that could forget the development would produce a world where a man grows for
    /// four seasons and then stops.
    /// </para>
    /// </summary>
    public void AgeUp()
    {
        Age = Math.Min(OldestAge, Age + 1);
        DevelopmentRules.Develop(this);
    }

    /// <summary>One of the eight things about him, by name.</summary>
    public int Get(PlayerAttribute attribute) => attribute switch
    {
        PlayerAttribute.Speed => Speed,
        PlayerAttribute.Accuracy => Accuracy,
        PlayerAttribute.Dribbling => Dribbling,
        PlayerAttribute.Heading => Heading,
        PlayerAttribute.Strength => Strength,
        PlayerAttribute.GoalkeeperPower => GoalkeeperPower,
        PlayerAttribute.Reflexes => Reflexes,
        _ => Stamina
    };

    /// <summary>
    /// Puts one of the eight at a number, clamped to the scale. It is the single door every
    /// change to an attribute goes through, which is what keeps the clamp from being
    /// forgotten in one of the seven places that would otherwise each have their own.
    /// </summary>
    public void Set(PlayerAttribute attribute, double value)
    {
        var clamped = Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero));

        switch (attribute)
        {
            case PlayerAttribute.Speed:
                Speed = clamped;
                break;
            case PlayerAttribute.Accuracy:
                Accuracy = clamped;
                break;
            case PlayerAttribute.Dribbling:
                Dribbling = clamped;
                break;
            case PlayerAttribute.Heading:
                Heading = clamped;
                break;
            case PlayerAttribute.Strength:
                Strength = clamped;
                break;
            case PlayerAttribute.GoalkeeperPower:
                GoalkeeperPower = Position == Position.GK ? clamped : 0;
                break;
            case PlayerAttribute.Reflexes:
                Reflexes = Position == Position.GK ? clamped : 0;
                break;
            default:
                Stamina = clamped;
                break;
        }
    }

    /// <summary>
    /// Adds a possibly fractional amount to one of the eight.
    ///
    /// <para>
    /// Rounds half away from zero rather than to even, and that is not pedantry: the engine's
    /// <c>Math.Round</c> sends 0.5 to 0 and 1.5 to 2, so a growth budget that kept landing
    /// on exactly a half would hand out no point at all in some years and two in others, and
    /// the man who grew would depend on the parity of his age.
    /// </para>
    /// </summary>
    public void Raise(PlayerAttribute attribute, double amount)
    {
        if (amount <= 0.0)
        {
            return;
        }

        Set(attribute, Get(attribute) + amount);
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

    private static int Clamp(int value) => Math.Max(1, Math.Min(100, value));

    private static int ClampAge(int age) => Math.Max(YoungestAge, Math.Min(OldestAge, age));
}