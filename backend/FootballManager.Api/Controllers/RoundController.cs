using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Controllers;

[ApiController]
[Route("round")]
[Produces("application/json")]
public class RoundController : ControllerBase
{
    private readonly RoundService _roundService;

    public RoundController(RoundService roundService)
    {
        _roundService = roundService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoundDto>>> List(CancellationToken cancellationToken)
    {
        var rounds = await _roundService.GetAllAsync(cancellationToken);
        return Ok(rounds.ToDtos());
    }

    [HttpGet("by-competition-season/{competitionSeasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<RoundDto>>> ListByCompetitionSeason(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var rounds = await _roundService.GetByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);
        return Ok(rounds.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoundDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var round = await _roundService.GetByIdAsync(id, cancellationToken);
        return Ok(round.ToDto());
    }
}
