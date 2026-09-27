using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Use cases around clubs. It coordinates the repositories and decides what is valid;
/// it never writes SQL and it never returns entities straight to the transport layer.
/// </summary>
public class TeamService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMatchRepository _matchRepository;

    public TeamService(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository,
        IMatchRepository matchRepository)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
        _matchRepository = matchRepository;
    }

    /// <summary>
    /// The last matches a club has finished, newest first, for the form guide on its card.
    /// A club that does not exist has no form, and saying so beats drawing an empty list
    /// for an id that was never one.
    /// </summary>
    public async Task<IReadOnlyList<TeamMatchRecord>> GetRecentMatchesAsync(
        Guid teamId,
        int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        return await _matchRepository.GetTeamHistoryAsync(teamId, TeamHistoryRules.Clamp(limit), cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _teamRepository.ListAsync(cancellationToken);

    public async Task<Team> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _teamRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), id);

    public async Task<IReadOnlyList<SquadPlayer>> GetSquadAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        var memberships = await _teamRepository.GetSquadAsync(teamId, seasonId, cancellationToken);
        var squad = new List<SquadPlayer>(memberships.Count);

        foreach (var membership in memberships)
        {
            var player = await _playerRepository.GetAsync(membership.PlayerId, cancellationToken);
            if (player is null)
            {
                continue;
            }

            var seasonState = await _playerRepository.GetSeasonStateAsync(player.Id, seasonId, cancellationToken);
            if (seasonState is null)
            {
                continue;
            }

            squad.Add(new SquadPlayer { Player = player, SeasonState = seasonState });
        }

        return squad;
    }
}
