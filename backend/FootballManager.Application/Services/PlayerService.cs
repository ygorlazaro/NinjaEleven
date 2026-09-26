using FootballManager.Domain.Enums;
using FootballManager.Application.Models;
using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Players;
using FootballManager.Domain.Seasons;
using FootballManager.Domain.Teams;

namespace FootballManager.Application.Services;

public class PlayerService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ISeasonRepository _seasonRepository;

    public PlayerService(
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository,
        ISeasonRepository seasonRepository)
    {
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _playerRepository.ListAsync(cancellationToken);

    public async Task<Player> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _playerRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Player), id);

    public async Task<PlayerSeasonState> GetSeasonStateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _playerRepository.GetAsync(playerId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Player), playerId);
        }

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        return await _playerRepository.GetSeasonStateAsync(playerId, seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);
    }

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

        var seasonStates = await _playerRepository.ListSeasonStatesAsync(seasonId, teamId, cancellationToken);
        var squad = new List<SquadPlayer>(seasonStates.Count);

        foreach (var seasonState in seasonStates)
        {
            var player = await _playerRepository.GetAsync(seasonState.PlayerId, cancellationToken);
            if (player is not null)
            {
                squad.Add(new SquadPlayer { Player = player, SeasonState = seasonState });
            }
        }

        // A squad is read top to bottom, so it comes back the way a manager reads it:
        // goalkeepers, defenders, midfielders, attackers, and by name inside each group.
        return squad
            .Apply(player => player.Player.Position, player => player.Player.Name)
            .ToList();
    }
}
