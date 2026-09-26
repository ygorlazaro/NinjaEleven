using FootballManager.Application.Models;
using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Teams;

namespace FootballManager.Application.Services;

/// <summary>
/// Use cases around clubs. It coordinates the repositories and decides what is valid;
/// it never writes SQL and it never returns entities straight to the transport layer.
/// </summary>
public class TeamService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISeasonRepository _seasonRepository;

    public TeamService(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
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
