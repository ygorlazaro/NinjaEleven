using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

[ApiController]
[Route("fixture")]
[Produces("application/json")]
public class FixtureController : ControllerBase
{
    private readonly FixtureService _fixtureService;

    public FixtureController(FixtureService fixtureService)
    {
        _fixtureService = fixtureService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FixtureDto>>> List(CancellationToken cancellationToken)
    {
        var fixtures = await _fixtureService.GetAllAsync(cancellationToken);
        return Ok(fixtures.Select(fixture => fixture.ToDto()).ToList());
    }

    [HttpGet("by-round/{roundId:guid}")]
    public async Task<ActionResult<IReadOnlyList<FixtureDto>>> ListByRound(
        Guid roundId,
        CancellationToken cancellationToken)
    {
        var fixtures = await _fixtureService.GetByRoundAsync(roundId, cancellationToken);
        return Ok(fixtures.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FixtureDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var fixture = await _fixtureService.GetByIdAsync(id, cancellationToken);
        return Ok(fixture.ToDto());
    }
}
