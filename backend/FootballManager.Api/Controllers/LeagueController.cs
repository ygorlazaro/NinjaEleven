using FootballManager.Api.Contracts;
using FootballManager.Api.Mappings;
using FootballManager.Application.Services;
using FootballManager.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Controllers;

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

    public LeagueController(LeagueService leagueService)
    {
        _leagueService = leagueService;
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

    [HttpGet("standing/{competitionSeasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<StandingDto>>> GetStandings(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var standings = await _leagueService.GetStandingsAsync(competitionSeasonId, cancellationToken);
        return Ok(standings.ToDtos());
    }

    [HttpGet("standing/{competitionSeasonId:guid}/team/{teamId:guid}")]
    public async Task<ActionResult<StandingDto>> GetStanding(
        Guid competitionSeasonId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var standing = await _leagueService.GetStandingAsync(competitionSeasonId, teamId, cancellationToken);
        return Ok(standing.ToDto());
    }

    [HttpGet("scorer/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ScorerDto>>> GetScorers(
        Guid seasonId,
        [FromQuery] int? topN,
        CancellationToken cancellationToken)
    {
        var scorers = await _leagueService.GetScorersAsync(seasonId, topN ?? DefaultTopScorers, cancellationToken);
        return Ok(scorers.ToDtos());
    }
}
