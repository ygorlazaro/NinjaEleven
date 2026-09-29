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
    private readonly MatchdayService _matchdayService;

    public FixtureController(FixtureService fixtureService, MatchdayService matchdayService)
    {
        _fixtureService = fixtureService;
        _matchdayService = matchdayService;
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

    /// <summary>
    /// The next match a club has to play, in the order football is played.
    ///
    /// It is asked of the backend because the order is a rule and not a sort: a matchday is
    /// played in waves — Supercup, championship, cup — while a window's number is only an
    /// identifier, and the round of sixteen is window one of the cup on a day whose
    /// championship is window five. A client that ordered a season's windows by that number
    /// offered a manager a cup leg the server then refused, which is a screen offering a
    /// decision it knows is going to be turned down.
    ///
    /// The answer carries whether the window may be started *now*, so a cup leg behind a
    /// championship that has not been played yet is offered as the next match and says so,
    /// rather than being offered as if it were kick-off time.
    /// </summary>
    [HttpGet("next")]
    public async Task<ActionResult<NextFixtureDto?>> GetNext(
        [FromQuery] Guid teamId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var window = await _matchdayService.GetNextFixtureWindowAsync(teamId, seasonId, cancellationToken);
        if (window is null)
        {
            return Ok();
        }

        var fixture = await _fixtureService.GetByIdAsync(window.FixtureId, cancellationToken);

        return Ok(new NextFixtureDto
        {
            Fixture = fixture.ToDto(),
            RoundId = window.RoundId,
            RoundNumber = window.RoundNumber,
            MatchDayId = window.MatchDayId,
            MatchDayNumber = window.MatchDayNumber,
            WaveOpen = window.WaveOpen,
            WaitingFor = window.WaitingFor?.ToString() ?? string.Empty,
            CompetitionName = window.CompetitionName,
            CompetitionType = window.CompetitionType.ToString()
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FixtureDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var fixture = await _fixtureService.GetByIdAsync(id, cancellationToken);
        return Ok(fixture.ToDto());
    }
}
