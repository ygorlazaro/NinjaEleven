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
    private readonly ManagerService _managerService;

    public TeamController(TeamService teamService, FinanceService financeService, ManagerService managerService)
    {
        _teamService = teamService;
        _financeService = financeService;
        _managerService = managerService;
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

    /// <summary>
    /// Marks the club a manager is running, and unmarks the one that was before.
    ///
    /// The engine treats all thirty-six clubs alike and is right to; this mark says which club
    /// has a person answering for it, which is the one thing the game cannot decide on its own.
    /// The market is the only reader: an offer for one of this club's players goes to an inbox
    /// to be accepted or refused, where every other club's is settled by the formula.
    /// </summary>
    [HttpPost("{id:guid}/manager-club")]
    public async Task<ActionResult<TeamDto>> TakeOver(
        Guid id,
        CancellationToken cancellationToken)
    {
        var team = await _teamService.TakeOverAsManagerClubAsync(id, cancellationToken);
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
    /// Head-to-head matches between two clubs, newest first. A club's history against a
    /// specific rival is a different question from its general run.
    /// </summary>
    [HttpGet("{teamId:guid}/head-to-head/{opponentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TeamMatchRecordDto>>> GetHeadToHead(
        Guid teamId,
        Guid opponentId,
        [FromQuery] int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        var matches = await _teamService.GetHeadToHeadAsync(teamId, opponentId, limit, cancellationToken);
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

    /// <summary>
    /// Changes the display name of the club the manager has taken charge of.
    /// </summary>
    [HttpPut("{id:guid}/name")]
    public async Task<ActionResult<TeamDto>> UpdateName(
        Guid id,
        [FromBody] UpdateTeamNameRequestDto request,
        CancellationToken cancellationToken)
    {
        var team = await _teamService.UpdateNameAsync(id, request.Name, cancellationToken);
        return Ok(team.ToDto());
    }

    /// <summary>
    /// Changes the kit colours of the club the manager has taken charge of.
    /// </summary>
    [HttpPut("{id:guid}/colors")]
    public async Task<ActionResult<TeamDto>> UpdateColors(
        Guid id,
        [FromBody] UpdateTeamColorsRequestDto request,
        CancellationToken cancellationToken)
    {
        var team = await _teamService.UpdateColorsAsync(
            id, request.PrimaryColor, request.SecondaryColor, cancellationToken);
        return Ok(team.ToDto());
    }

    /// <summary>
    /// Changes the name of the manager (coach) of a club.
    /// </summary>
    [HttpPut("{teamId:guid}/manager-name")]
    public async Task<ActionResult<ManagerDto>> UpdateManagerName(
        Guid teamId,
        [FromBody] ManagerRenameRequestDto request,
        CancellationToken cancellationToken)
    {
        var manager = await _managerService.RenameAsync(teamId, request.Name, cancellationToken);
        return Ok(manager.ToDto());
    }
}
