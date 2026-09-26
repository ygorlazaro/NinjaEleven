using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

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

    /// <summary>
    /// A player whole: who he is, what he is worth this season, and every match he has a
    /// line in. The season is optional — without it the career totals are still returned,
    /// because "how has this striker done" is a question about more than one season.
    /// </summary>
    [HttpGet("{id:guid}/profile")]
    public async Task<ActionResult<PlayerProfileDto>> GetProfile(
        Guid id,
        [FromQuery] Guid? seasonId,
        CancellationToken cancellationToken) =>
        Ok((await _playerService.GetProfileAsync(id, seasonId, cancellationToken)).ToDto());

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
