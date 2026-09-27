using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// Transport only: it converts requests and results into DTOs and delegates every
/// decision to the services. Routes are singular, as the architecture requires.
/// </summary>
[ApiController]
[Route("team")]
[Produces("application/json")]
public class TeamController : ControllerBase
{
    private readonly TeamService _teamService;
    private readonly FinanceService _financeService;

    public TeamController(TeamService teamService, FinanceService financeService)
    {
        _teamService = teamService;
        _financeService = financeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> List(CancellationToken cancellationToken)
    {
        var teams = await _teamService.GetAllAsync(cancellationToken);
        return Ok(teams.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var team = await _teamService.GetByIdAsync(id, cancellationToken);
        return Ok(team.ToDto());
    }

    [HttpGet("{teamId:guid}/squad/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SquadPlayerDto>>> GetSquad(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var squad = await _teamService.GetSquadAsync(teamId, seasonId, cancellationToken);
        return Ok(squad.Select(player => player.ToDto()).ToList());
    }

    /// <summary>
    /// The club's last finished matches, newest first, for the form guide on its card.
    /// </summary>
    [HttpGet("{teamId:guid}/matches")]
    public async Task<ActionResult<IReadOnlyList<TeamMatchRecordDto>>> GetRecentMatches(
        Guid teamId,
        [FromQuery] int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        var matches = await _teamService.GetRecentMatchesAsync(teamId, limit, cancellationToken);
        return Ok(matches.Select(match => match.ToDto()).ToList());
    }

    /// <summary>
    /// The club's book: a page of its movements, newest first, and the totals of whatever the
    /// page was narrowed to. No season filter means the whole career, which is the only
    /// reading in which the lines of two seasons sit in one list in the order they happened.
    /// </summary>
    [HttpGet("{teamId:guid}/finance")]
    public async Task<ActionResult<FinanceLedgerDto>> GetFinance(
        Guid teamId,
        [FromQuery] Guid? seasonId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = FinanceRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _financeService.GetLedgerAsync(teamId, seasonId, page, pageSize, cancellationToken);

        return Ok(ledger.ToDto());
    }

    /// <summary>
    /// The club's scorers of a season, optionally for one kind of competition.
    /// </summary>
    /// <remarks>
    /// The competition arrives as a name and is bound to the enum, so `?competition=Cup` is the
    /// wire form of a value the world stores as an enum. A client that asked for a cup and got
    /// a league table would be given a number that answers a different question, and there
    /// would be nothing in the answer to say so.
    /// </remarks>
    [HttpGet("{teamId:guid}/scorer")]
    public async Task<ActionResult<IReadOnlyList<ClubScorerDto>>> GetScorers(
        Guid teamId,
        [FromQuery] Guid seasonId,
        [FromQuery] CompetitionType? competition = null,
        [FromQuery] int? topN = null,
        CancellationToken cancellationToken = default)
    {
        var scorers = await _teamService.GetScorersAsync(
            teamId, seasonId, competition, topN ?? ScorerRules.DefaultScorers, cancellationToken);

        return Ok(scorers.ToDtos());
    }
}
