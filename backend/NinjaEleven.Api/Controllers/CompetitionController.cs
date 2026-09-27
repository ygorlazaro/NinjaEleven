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

    /// <summary>
    /// The clubs entered in one edition of a competition.
    ///
    /// This is the list the club picker is built from, and it is asked of the backend because
    /// a club's division is a fact about its enrolment in an edition rather than a column on
    /// the club: a client handed a list of clubs and a list of divisions has to join them up
    /// itself, and joining them up itself is how a club ends up in the 3ª Divisão because the
    /// two lists were sorted differently.
    /// </summary>
    [HttpGet("{competitionSeasonId:guid}/club")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> ListClubs(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var clubs = await _competitionService.GetClubsAsync(competitionSeasonId, cancellationToken);
        return Ok(clubs.ToDtos());
    }

    [HttpGet("by-season/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CompetitionEditionDto>>> ListBySeason(
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
