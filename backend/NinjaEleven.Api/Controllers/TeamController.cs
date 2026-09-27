using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

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

    /// <summary>
    /// The club's last finished matches, newest first, for the form guide on its card.
    /// </summary>
    [HttpGet("{teamId:guid}/matches")]
    public async Task<ActionResult<IReadOnlyList<TeamMatchRecordDto>>> GetRecentMatches(
        Guid teamId,
        [FromQuery] int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        var matches = await _teamService.GetRecentMatchesAsync(teamId, limit, cancellationToken);
        return Ok(matches.Select(match => match.ToDto()).ToList());
    }
}
