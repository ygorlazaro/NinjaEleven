namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Populates a fresh database with the reference data (name pools) and a starting
/// world (season, competition, teams, squads). Exposed as an abstraction so the API
/// only depends on the Application contract, not on EF Core.
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills a club with young players to replace the ones that retired at the end of a season.
    /// The count is 80–120% of the retirees, so a club that lost three men does not come back
    /// with exactly three replacements — the market does not refill a squad like a spreadsheet.
    /// </summary>
    /// <param name="seasonId">The season the young players are being given a state for.</param>
    Task GenerateYoungPlayersAsync(Guid seasonId, CancellationToken cancellationToken = default);
}
