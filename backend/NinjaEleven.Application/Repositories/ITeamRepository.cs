using NinjaEleven.Domain.Players;
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
    /// Marks this club as the one a manager is running, and unmarks the one that was before.
    ///
    /// The flag belongs to the team, not to a service, and it belongs in the repository because
    /// two of them write it: a career beginning asks for it, and a club being taken over asks
    /// for it. One row in the world carries it, and a second copy of this rule in two services
    /// is a way for the world to end up with two.
    /// </summary>
    Task<Team> MarkAsManagerClubAsync(Guid teamId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// The clubs that have no human manager assigned yet, so a new user can claim one.
    /// "No human manager" is a manager row with no <see cref="Manager.UserId"/> —
    /// or no manager row at all. A club that appears here is NPC-controlled.
    /// </summary>
    Task<IReadOnlyList<Team>> ListClubsWithoutManagerAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clubs without human manager in a specific season.
    /// </summary>
    Task<IReadOnlyList<Team>> ListClubsWithoutManagerInSeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active membership across all clubs for a season, so a transfer search can
    /// build its listings in one query rather than one club at a time.
    /// </summary>
    Task<IReadOnlyList<TeamMembership>> ListAllContractsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task AddMembershipAsync(TeamMembership membership, CancellationToken cancellationToken = default);

    void UpdateMembership(TeamMembership membership);

    /// <summary>
    /// Gets a player by ID.
    /// </summary>
    Task<Player?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the division tier for teams in a season from competition participants.
    /// </summary>
    Task<Dictionary<Guid, int>> GetTeamDivisionsAsync(
        Guid seasonId,
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets squads for multiple teams in a season in a single query.
    /// </summary>
    Task<Dictionary<Guid, IReadOnlyList<TeamMembership>>> GetSquadsAsync(
        IEnumerable<Guid> teamIds,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple players by their IDs in a single query.
    /// </summary>
    Task<Dictionary<Guid, Player>> GetPlayersAsync(
        IEnumerable<Guid> playerIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all squads for a season in a single query.
    /// </summary>
    Task<Dictionary<Guid, IReadOnlyList<TeamMembership>>> GetAllSquadsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);
}
