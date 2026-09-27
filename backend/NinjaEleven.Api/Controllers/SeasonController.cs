using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

[ApiController]
[Route("season")]
[Produces("application/json")]
public class SeasonController : ControllerBase
{
    private readonly SeasonService _seasonService;
    private readonly SeasonCalendarService _calendarService;

    public SeasonController(SeasonService seasonService, SeasonCalendarService calendarService)
    {
        _seasonService = seasonService;
        _calendarService = calendarService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonDto>>> List(CancellationToken cancellationToken)
    {
        var seasons = await _seasonService.GetAllAsync(cancellationToken);
        return Ok(seasons.ToDtos());
    }

    /// <summary>
    /// The calendar of a season: the matchdays, and the windows of football in them.
    ///
    /// A season that has not been drawn has no calendar, and asking for it draws it. The
    /// draw is idempotent, so a client that asks again after a reload gets the calendar that
    /// is already there rather than a second one.
    /// </summary>
    /// <remarks>
    /// A GET, and not a POST, because building the calendar is idempotent: a season that has
    /// been drawn has one, and asking again returns it. A POST that could be called twice and
    /// produced the same answer would be a POST that promised a change and made none.
    /// </remarks>
    [HttpGet("{id:guid}/calendar")]
    public async Task<ActionResult<SeasonCalendarDto>> GetCalendar(
        Guid id,
        [FromQuery] bool build,
        CancellationToken cancellationToken)
    {
        if (await _seasonService.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", id);
        }

        var calendar = await (build
            ? _calendarService.BuildAsync(id, cancellationToken)
            : _calendarService.GetAsync(id, cancellationToken));

        return Ok(calendar.ToDto());
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
        var season = request.Number is { } number
            ? await _seasonService.CreateWithNumberAsync(
                number,
                request.StartDate!.Value,
                request.EndDate!.Value,
                cancellationToken)
            : await _seasonService.CreateAsync(
                request.StartDate!.Value,
                request.EndDate!.Value,
                cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = season.Id }, season.ToDto());
    }
}
