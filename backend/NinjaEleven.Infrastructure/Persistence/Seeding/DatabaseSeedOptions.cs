namespace NinjaEleven.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeding options. Bound from the `DatabaseSeed` configuration section.
/// </summary>
public class DatabaseSeedOptions
{
    public const string SectionName = "DatabaseSeed";

    public int PlayersPerTeam { get; set; } = 23;

    /// <summary>
    /// Goalkeepers in every squad. A club needs more than the one it starts with: an
    /// injury, a suspension or a red card has to be coverable.
    /// </summary>
    public int GoalkeepersPerTeam { get; set; } = 3;
    public int MinimumEnergy { get; set; } = 70;
    public int MaximumEnergy { get; set; } = 100;
    public int MinimumAttribute { get; set; } = 1;
    public int MaximumAttribute { get; set; } = 20;
    /// <summary>
    /// The oldest a seeded player is. The world keeps ages rather than birth dates, so a squad is
    /// drawn between two numbers instead of between two years, and the same spread is expressed
    /// the way every other screen reads it.
    /// </summary>
    public int OldestAge { get; set; } = 38;

    /// <summary>The youngest a seeded player is.</summary>
    public int YoungestAge { get; set; } = 21;

    /// <summary>
    /// When set, the whole seeding run is reproducible: same seed, same world.
    /// </summary>
    public int? RandomSeed { get; set; }
}
