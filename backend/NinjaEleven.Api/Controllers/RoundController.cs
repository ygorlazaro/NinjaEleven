using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Api.Realtime;
using NinjaEleven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

[ApiController]
[Route("round")]
[Produces("application/json")]
public class RoundController : ControllerBase
{
    private readonly RoundService _roundService;
    private readonly MatchSimulator _simulator;

    public RoundController(RoundService roundService, MatchSimulator simulator)
    {
        _roundService = roundService;
        _simulator = simulator;
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

    /// <summary>
    /// Plays every fixture of the round that is still scheduled, without a live
    /// session. The round is over when this returns, which is what makes the next one
    /// the round the manager is playing in.
    /// </summary>
    [HttpPost("{id:guid}/simulate")]
    public async Task<ActionResult<RoundSimulationDto>> Simulate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _simulator.SimulateRoundAsync(id, cancellationToken);
        return Ok(result);
    }
}
