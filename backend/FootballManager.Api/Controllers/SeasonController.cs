using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Controllers;

[ApiController]
[Route("season")]
[Produces("application/json")]
public class SeasonController : ControllerBase
{
    private readonly SeasonService _seasonService;

    public SeasonController(SeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonDto>>> List(CancellationToken cancellationToken)
    {
        var seasons = await _seasonService.GetAllAsync(cancellationToken);
        return Ok(seasons.ToDtos());
    }

    [HttpGet("current")]
    public async Task<ActionResult<SeasonDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var season = await _seasonService.GetCurrentAsync(cancellationToken);
        return Ok(season.ToDto());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SeasonDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var season = await _seasonService.GetByIdAsync(id, cancellationToken);
        return Ok(season.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<SeasonDto>> Create(
        [FromBody] CreateSeasonRequestDto request,
        CancellationToken cancellationToken)
    {
        var season = await _seasonService.CreateAsync(
            request.Name,
            request.StartDate!.Value,
            request.EndDate!.Value,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = season.Id }, season.ToDto());
    }
}
