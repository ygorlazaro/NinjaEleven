using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence contract for clubs and their squads. It only stores and retrieves
/// data: deciding anything about the game belongs to the services.
/// </summary>
public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> ListAsync(CancellationToken cancellationToken = default);
    Task<Team?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A set of clubs by their identifiers, in the order asked for. Reading a set of clubs one
    /// at a time is a query per club to answer a question about a division.
    /// </summary>
    Task<IReadOnlyList<Team>> ListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(Team team, CancellationToken cancellationToken = default);
    void Update(Team team);
    void Remove(Team team);

    /// <summary>
    /// Squad of a club at a given season, resolved through the active memberships.
    /// </summary>
    Task<IReadOnlyList<TeamMembership>> GetSquadAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The contracts a club holds right now: every membership that has not been called off.
    ///
    /// This is not the squad of a season, which is a snapshot of one, and it is not the whole
    /// of a club's players, which is a list of everyone who has ever worn its shirt. It is the
    /// answer to one question — who is still under contract here — and it is a question worth
    /// asking on its own because it is the one that stays true when seasons are filtered: a
    /// scorer from 2019 who is still at the club is still at the club in every season since.
    /// </summary>
    Task<IReadOnlyList<TeamMembership>> GetLiveContractsAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);
}
