namespace NinjaEleven.Domain.Competitions;

/// <summary>Which way a club is moving between the divisions.</summary>
public enum MovementDirection
{
    /// <summary>Staying where it is.</summary>
    None = 0,

    /// <summary>Going up a tier.</summary>
    Promoted = 1,

    /// <summary>Going down a tier.</summary>
    Relegated = -1
}

/// <summary>
/// One club's place in the pyramid for the season that is being set up.
/// </summary>
/// <param name="TeamId">The club.</param>
/// <param name="FromTier">The tier it finishes the season in.</param>
/// <param name="ToTier">The tier it starts the next one in.</param>
/// <param name="Position">Where it finished, counted from one.</param>
public readonly record struct ClubMovement(
    Guid TeamId,
    int FromTier,
    int ToTier,
    int Position)
{
    public MovementDirection Direction => ToTier switch
    {
        _ when ToTier < FromTier => MovementDirection.Promoted,
        _ when ToTier > FromTier => MovementDirection.Relegated,
        _ => MovementDirection.None
    };

    public bool IsPromoted => Direction == MovementDirection.Promoted;

    public bool IsRelegated => Direction == MovementDirection.Relegated;

    public bool Stays => Direction == MovementDirection.None;
}

/// <summary>
/// Where every club in the pyramid ends up when a season is over.
///
/// The three divisions are a closed system: four clubs go down from the top, four go up
/// from the second and four go down from it, four go up from the third and nothing goes below
/// it. A division therefore always finishes the season with twelve clubs in it, which is the
/// whole point of working the movements out from the tables rather than settling each club
/// one at a time — a club that is promoted out of the second division is a place in the
/// first, and the club that takes it is decided by the same table.
/// </summary>
public sealed class DivisionMovement
{
    private DivisionMovement(int fromTier, IReadOnlyList<ClubMovement> movements)
    {
        FromTier = fromTier;
        Movements = movements;
    }

    /// <summary>The tier these standings belong to.</summary>
    public int FromTier { get; }

    public IReadOnlyList<ClubMovement> Movements { get; }

    public IReadOnlyList<Guid> Promoted => Movements.Where(m => m.IsPromoted).Select(m => m.TeamId).ToList();

    public IReadOnlyList<Guid> Relegated => Movements.Where(m => m.IsRelegated).Select(m => m.TeamId).ToList();

    public IReadOnlyList<Guid> Staying => Movements.Where(m => m.Stays).Select(m => m.TeamId).ToList();

    /// <summary>Who the champion of this tier was, and the only trophy that comes with it.</summary>
    public Guid Champion => Movements
        .OrderBy(m => m.Position)
        .First(m => m.Position == 1)
        .TeamId;

    /// <summary>
    /// Works out where every club in one division goes.
    /// </summary>
    /// <param name="tier">The tier these standings belong to.</param>
    /// <param name="standings">The clubs in the order the table finished them.</param>
    public static DivisionMovement From(int tier, IReadOnlyList<StandingEntry> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        if (standings.Count == 0)
        {
            throw new ArgumentException("A division with nobody in it cannot be reorganised.", nameof(standings));
        }

        var positions = new Dictionary<Guid, int>();
        for (var index = 0; index < standings.Count; index++)
        {
            positions[standings[index].TeamId] = index + 1;
        }

        var relegated = standings.Count - CompetitionRules.RelegationSlots;
        var movements = new List<ClubMovement>(standings.Count);

        // The bottom of the pyramid is a wall. Three divisions is the whole of the country,
        // and a club that finishes last in the third division is not relegated to a fourth
        // tier that does not exist — it is the last club of the last division, and the rules
        // say so rather than inventing somewhere for it to go.
        var lowestTier = CompetitionRules.Tiers().Count;

        foreach (var standing in standings)
        {
            var position = positions[standing.TeamId];

            // The champion of a tier always goes up when there is a tier above it, whatever
            // its finishing position: winning the second division and staying in it would make
            // the title worthless. The first position is inside the promotion places anyway,
            // so this only has a say when the tiers are not the size the rules assume.
            var promoted = tier > 1 && position <= CompetitionRules.PromotionSlots;
            var dropped = !promoted && position > relegated && tier < lowestTier;

            movements.Add(new ClubMovement(
                standing.TeamId,
                tier,
                promoted ? tier - 1 : dropped ? tier + 1 : tier,
                position));
        }

        return new DivisionMovement(tier, movements);
    }
}
