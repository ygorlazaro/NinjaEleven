using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

[ApiController]
[Route("competition")]
[Produces("application/json")]
public class CompetitionController : ControllerBase
{
    private readonly CompetitionService _competitionService;

    public CompetitionController(CompetitionService competitionService)
    {
        _competitionService = competitionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompetitionDto>>> List(CancellationToken cancellationToken)
    {
        var competitions = await _competitionService.GetAllAsync(cancellationToken);
        return Ok(competitions.ToDtos());
    }

    [HttpGet("by-season/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CompetitionDto>>> ListBySeason(
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var competitions = await _competitionService.GetBySeasonAsync(seasonId, cancellationToken);
        return Ok(competitions.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompetitionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var competition = await _competitionService.GetByIdAsync(id, cancellationToken);
        return Ok(competition.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionDto>> Create(
        [FromBody] CreateCompetitionRequestDto request,
        CancellationToken cancellationToken)
    {
        var competition = await _competitionService.CreateAsync(request.Name, request.Type, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = competition.Id }, competition.ToDto());
    }
}
