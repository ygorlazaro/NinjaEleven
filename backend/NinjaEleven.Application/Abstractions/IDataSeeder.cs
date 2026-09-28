using NinjaEleven.Domain.Transfers;

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
    /// Deals the season's intake: the young free agents a season opens with, unattached and
    /// waiting to be signed. The count is the rule's own
    /// (<see cref="YouthIntakeRules.FreeAgentsPerSeason"/>) and it does not depend on the
    /// retirements of the season before, because a season's first day has none to divide by and
    /// a market fed by that ratio opens empty.
    /// </summary>
    /// <param name="seasonId">The season the young players are being given a state for.</param>
    Task GenerateYoungPlayersAsync(Guid seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a given number of young free agents on the market, aged as
    /// <see cref="YouthIntakeRules"/> says, with no club and no contract. It is the same intake
    /// the season opening deals, asked for with a count of its own — the backfill a world that
    /// was written before the rule existed needs, so its market is not empty of anybody a club
    /// could sign.
    /// </summary>
    /// <param name="count">How many men to deal onto the market.</param>
    /// <param name="seasonId">
    /// The season they are given a state for; null for the one in progress.
    /// </param>
    Task<int> SeedYoungFreeAgentsAsync(
        int count = YouthIntakeRules.FreeAgentsPerSeason,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default);
}
