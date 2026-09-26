namespace FootballManager.Application.Abstractions;

/// <summary>
/// Populates a fresh database with the reference data (name pools) and a starting
/// world (season, competition, teams, squads). Exposed as an abstraction so the API
/// only depends on the Application contract, not on EF Core.
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
