using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Controllers;

[ApiController]
[Route("player")]
[Produces("application/json")]
public class PlayerController : ControllerBase
{
    private readonly PlayerService _playerService;

    public PlayerController(PlayerService playerService)
    {
        _playerService = playerService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlayerDto>>> List(CancellationToken cancellationToken)
    {
        var players = await _playerService.GetAllAsync(cancellationToken);
        return Ok(players.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlayerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var player = await _playerService.GetByIdAsync(id, cancellationToken);
        return Ok(player.ToDto());
    }

    [HttpGet("{playerId:guid}/season/{seasonId:guid}")]
    public async Task<ActionResult<PlayerSeasonStateDto>> GetSeasonState(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var state = await _playerService.GetSeasonStateAsync(playerId, seasonId, cancellationToken);
        return Ok(state.ToDto());
    }
}
