using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Controllers;

/// <summary>
/// Transport only: it converts requests and results into DTOs and delegates every
/// decision to the services. Routes are singular, as the architecture requires.
/// </summary>
[ApiController]
[Route("team")]
[Produces("application/json")]
public class TeamController : ControllerBase
{
    private readonly TeamService _teamService;

    public TeamController(TeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> List(CancellationToken cancellationToken)
    {
        var teams = await _teamService.GetAllAsync(cancellationToken);
        return Ok(teams.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var team = await _teamService.GetByIdAsync(id, cancellationToken);
        return Ok(team.ToDto());
    }

    [HttpGet("{teamId:guid}/squad/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SquadPlayerDto>>> GetSquad(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var squad = await _teamService.GetSquadAsync(teamId, seasonId, cancellationToken);
        return Ok(squad.Select(player => player.ToDto()).ToList());
    }
}
