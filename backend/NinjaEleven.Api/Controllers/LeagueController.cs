using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// Competition level endpoints. The route is singular as well; the classification and
/// the top scorers are always computed by the backend.
/// </summary>
[ApiController]
[Route("league")]
[Produces("application/json")]
public class LeagueController : ControllerBase
{
    private const int DefaultTopScorers = 15;

    private readonly LeagueService _leagueService;
    private readonly StandingsService _standingsService;

    public LeagueController(LeagueService leagueService, StandingsService standingsService)
    {
        _leagueService = leagueService;
        _standingsService = standingsService;
    }

    [HttpPost("setup")]
    public async Task<ActionResult<LeagueSetupResultDto>> Setup(
        [FromBody] SetupLeagueRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.CompetitionId == Guid.Empty)
        {
            throw new DomainValidationException("CompetitionRequired", "A competition must be informed.");
        }

        if (request.SeasonId == Guid.Empty)
        {
            throw new DomainValidationException("SeasonRequired", "A season must be informed.");
        }

        var setup = await _leagueService.SetupAsync(
            request.CompetitionId,
            request.SeasonId,
            request.TeamIds,
            cancellationToken);

        var rounds = await _leagueService.GetRoundsAsync(setup.CompetitionSeasonId, cancellationToken);

        return Ok(new LeagueSetupResultDto
        {
            CompetitionSeasonId = setup.CompetitionSeasonId,
            CompetitionId = setup.CompetitionId,
            SeasonId = setup.SeasonId,
            Rounds = rounds.ToDtos(),
            Fixtures = setup.Fixtures.Select(fixture => fixture.ToDto()).ToList()
        });
    }

    /// <summary>
    /// The table of one competition edition, with the live one beside it.
    ///
    /// Both are computed by the backend, because a table sorted in the browser is a table
    /// that can disagree with the promotion rules — and a manager told his club is sixth by
    /// the promotion pass and seventh by the screen has been given two answers to one
    /// question.
    /// </summary>
    [HttpGet("standing/{competitionSeasonId:guid}")]
    public async Task<ActionResult<CompetitionStandingsDto>> GetStandings(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var standings = await _standingsService.GetAsync(competitionSeasonId, cancellationToken);
        return Ok(standings.ToDto());
    }

    [HttpGet("standing/{competitionSeasonId:guid}/team/{teamId:guid}")]
    public async Task<ActionResult<StandingDto>> GetStanding(
        Guid competitionSeasonId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var standing = await _standingsService.GetRowAsync(competitionSeasonId, teamId, cancellationToken);
        return Ok(standing.ToDto());
    }

    /// <summary>
    /// What each division's table is paid out of, and what a position in it is worth.
    ///
    /// The backend owns these numbers because the share is a geometric weight and the last club
    /// is paid whatever the other eleven leave over: a screen that divided the purse by twelve
    /// would publish a championship that does not pay out its own money, and the last cheque is
    /// the one a manager adds the other eleven up to find.
    /// </summary>
    [HttpGet("prizes")]
    public ActionResult<IReadOnlyList<DivisionPurseDto>> GetPrizes() =>
        Ok(_leagueService.GetChampionshipPurses().ToDtos());

    /// <summary>
    /// A season's scoring chart, of one kind of competition and of one division of it.
    ///
    /// The division is a filter and not a detail, because the championship is four editions of
    /// one kind: asked for "the league" without one, the answer is a chart of the country, and a
    /// division's artilharia read off it is topped by a striker from a division whose purse the
    /// prize list under that same page does not use. The prize panel beside it is already
    /// per division, so a chart that was not would be the one panel on the screen paying a
    /// first-division cheque for a second division's football.
    /// </summary>
    [HttpGet("scorer/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ScorerDto>>> GetScorers(
        Guid seasonId,
        [FromQuery] int? topN,
        [FromQuery] CompetitionType? competition,
        [FromQuery] Guid? divisionId,
        CancellationToken cancellationToken)
    {
        var scorers = await _leagueService.GetScorersAsync(
            seasonId, topN ?? DefaultTopScorers, competition, divisionId, cancellationToken);

        return Ok(scorers.ToDtos());
    }
}
