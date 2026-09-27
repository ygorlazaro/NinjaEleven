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
    public int OldestBirthYear { get; set; } = 1988;
    public int YoungestBirthYear { get; set; } = 2005;

    /// <summary>
    /// When set, the whole seeding run is reproducible: same seed, same world.
    /// </summary>
    public int? RandomSeed { get; set; }
}
