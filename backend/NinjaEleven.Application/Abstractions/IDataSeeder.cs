using NinjaEleven.Domain.Players;
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
    /// Deals the season's academy intake: young players assigned to clubs, aged 16 to
    /// <see cref="AcademyRules.MaxAcademyAge"/>, up to <see cref="AcademyRules.MaxAcademyPlayersPerClub"/>
    /// per club. Academy players are not on a first-team contract and cannot be bought
    /// by other clubs until promoted.
    /// </summary>
    /// <param name="seasonId">The season the academy players are being given a state for.</param>
    Task GenerateAcademyPlayersAsync(Guid seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a given number of young free agents on the market, aged as
    /// <see cref="YouthIntakeRules"/> says, with no club and no contract. It is a backfill
    /// for worlds that were seeded before the academy system existed.
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
