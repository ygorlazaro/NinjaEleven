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
    private readonly SeasonCloseService _closeService;

    public SeasonController(
        SeasonService seasonService,
        SeasonCalendarService calendarService,
        SeasonCloseService closeService)
    {
        _seasonService = seasonService;
        _calendarService = calendarService;
        _closeService = closeService;
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

    /// <summary>
    /// Whether a season has nothing left to play, so a client can offer the button that closes
    /// it only once there is something to close.
    /// </summary>
    [HttpGet("{id:guid}/finished")]
    public async Task<ActionResult<bool>> IsFinished(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _closeService.IsFinishedAsync(id, cancellationToken));

    /// <summary>
    /// Closes a season: pays the championship prizes, writes the trophies and opens the
    /// season after it.
    ///
    /// It is idempotent, so a client that calls it twice — a double tap, a retry, a manager
    /// who pressed it again because the screen had not answered — pays every club once and
    /// opens one season, not two.
    /// </summary>
    /// <param name="openNextSeason">
    /// Whether the season after this one is opened. The switch exists because paying a season
    /// is a fact about the season that is over and opening the next one is a fact about the
    /// world: an administrator who wants the books settled before the calendar exists can ask
    /// for the first without the second.
    /// </param>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<SeasonCloseDto>> Close(
        Guid id,
        [FromQuery] bool openNextSeason = true,
        CancellationToken cancellationToken = default)
    {
        if (await _seasonService.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", id);
        }

        var result = await _closeService.CloseAsync(id, openNextSeason, cancellationToken);

        return Ok(new SeasonCloseDto
        {
            SeasonId = result.SeasonId,
            NextSeasonId = result.NextSeasonId,
            PrizesPaid = result.PrizesPaid,
            Champions = result.Champions
        });
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
