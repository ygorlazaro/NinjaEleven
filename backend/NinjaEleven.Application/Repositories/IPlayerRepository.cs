using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence contract for players and their season state.
/// </summary>
public interface IPlayerRepository
{
    Task<IReadOnlyList<Player>> ListAsync(CancellationToken cancellationToken = default);
    Task<Player?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Player player, CancellationToken cancellationToken = default);
    void Update(Player player);
    void Remove(Player player);

    Task<PlayerSeasonState?> GetSeasonStateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Season state of every player, optionally restricted to one club.
    /// </summary>
    Task<IReadOnlyList<PlayerSeasonState>> ListSeasonStatesAsync(
        Guid seasonId,
        Guid? teamId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same state, but tracked, for a caller that is going to change it. The read
    /// only overload detaches the entity, so writing a season total needs this one.
    /// </summary>
    Task<PlayerSeasonState?> GetSeasonStateForUpdateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    void UpdateSeasonState(PlayerSeasonState seasonState);

    /// <summary>
    /// The goals every player of one club scored in one season, summed from the match lines,
    /// optionally restricted to one kind of competition.
    ///
    /// The competition is not on a match line — a line knows the match, and a match knows its
    /// fixture, and a fixture knows the round, and only the round knows whether the tie was a
    /// division match, a cup tie or a Supercup. So the competition is reached by walking that
    /// chain, which is why this is a query and not a filter on a column: a scorers table that
    /// could only answer for the championship would be a table whose cup goals are missing
    /// rather than zero, and a manager reading it would take a cup final's goals for nothing.
    /// </summary>
    /// <param name="teamId">The club whose men are counted.</param>
    /// <param name="seasonId">The season the goals are of.</param>
    /// <param name="competitionType">
    /// League, Cup or Supercup; null for every kind at once, which is the club's whole season.
    /// </param>
    Task<IReadOnlyList<ClubScorerLine>> ListClubScorerLinesAsync(
        Guid teamId,
        Guid seasonId,
        CompetitionType? competitionType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A season's goals by player, from the match lines, across every club in it.
    ///
    /// It is the same walk as the club's own list without the club: a scorers list restricted to
    /// one kind of competition has to be a walk rather than a filter, because only the round
    /// knows whether a tie was a division match, a cup tie or a Supercup — and a cup's chart
    /// counted from the season total would be a chart of the league's goals wearing the cup's
    /// name. Null counts every kind at once, which is the whole season.
    /// </summary>
    /// <param name="seasonId">The season the goals are of.</param>
    /// <param name="competitionType">League, Cup or Supercup; null for every kind at once.</param>
    Task<IReadOnlyList<ClubScorerLine>> ListSeasonScorerLinesAsync(
        Guid seasonId,
        CompetitionType? competitionType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One edition's goals by player, from the match lines, across every club in it.
    /// </summary>
    /// <remarks>
    /// The edition and not the kind of competition, because the championship is three editions of
    /// one kind. A season's list restricted to "League" counts all three divisions at once, which
    /// is a chart of the country and not a division's artilharia: a first-division prize list
    /// counted that way would be topped by a third-division striker and then paid out of the
    /// first division's title money.
    /// </remarks>
    /// <param name="competitionSeasonId">The edition the goals are of: a division, or the cup.</param>
    Task<IReadOnlyList<ClubScorerLine>> ListEditionScorerLinesAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every player's season state for one season, each carrying the player's birth date
    /// so age-dependent rules (retirement, market value, NPC acceptance) are applied in
    /// one pass rather than per player.
    /// </summary>
    Task<IReadOnlyList<PlayerSeasonState>> ListAllSeasonStatesAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a season state for a player. Used when a transfer moves a player to a new club
    /// for the arrival season — the player keeps his identity, and a new state is given to
    /// him for the season he is moving into.
    /// </summary>
    Task AddSeasonStateAsync(PlayerSeasonState state, CancellationToken cancellationToken = default);
}
