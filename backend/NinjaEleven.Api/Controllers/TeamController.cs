using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Teams;
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
    private readonly StandingsService _standingsService;
    private readonly ClubProfileService _clubProfileService;

    public TeamController(
        TeamService teamService,
        FinanceService financeService,
        ManagerService managerService,
        StandingsService standingsService,
        ClubProfileService clubProfileService)
    {
        _teamService = teamService;
        _financeService = financeService;
        _managerService = managerService;
        _standingsService = standingsService;
        _clubProfileService = clubProfileService;
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
    /// The club's page, whole: who it is, who runs it, what it costs to keep, what it has won
    /// and what has happened to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One call, because the page is one thing. A screen that asked for the balance here, the
    /// roster there, the shelf from a third place and each of its four moments from a fourth
    /// would be making eight requests to draw one card, and would have to invent an answer for
    /// each of them while they were in flight — which is exactly how a page ends up showing a
    /// manager a club's size next to a club's balance from two different years.
    /// </para>
    /// <para>
    /// The season is optional and it moves exactly one number, the size of the roster: the
    /// balance is deliberately not narrowed to a season, the shelf is the club's whole career,
    /// and the history is read from the beginning of time. A career is longer than a season,
    /// and a page that only remembered this season would be a page about a team rather than a
    /// page about a club.
    /// </para>
    /// </remarks>
    [HttpGet("{teamId:guid}/profile")]
    public async Task<ActionResult<ClubProfileDto>> GetProfile(
        Guid teamId,
        [FromQuery] Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _clubProfileService.GetProfileAsync(teamId, seasonId, cancellationToken);
            return Ok(profile.ToDto());
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { code = "TeamNotFound", teamId });
        }
    }

    /// <summary>
    /// The club's book: a page of its movements, newest first, and the totals of whatever the
    /// page was narrowed to. No season filter means the whole career, which is the only
    /// reading in which the lines of two seasons sit in one list in the order they happened.
    /// </summary>
    [HttpGet("{teamId:guid}/finance")]
    public async Task<IActionResult> GetFinance(
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
    /// The club's balance, read from the last line written in its whole book. The market shows
    /// this above everything else — a manager bidding on a man needs to know whether his club
    /// can pay — and it is not narrowed by a season, because a balance that followed a filter
    /// would tell a manager his club had as much as it had spent.
    /// </summary>
    [HttpGet("{teamId:guid}/balance")]
    public async Task<IActionResult> GetBalance(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var balance = await _financeService.GetBalanceAsync(teamId, cancellationToken);
        return Ok(balance.ToDto());
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
    /// The club's place in a season: the division it is in and the line it holds there.
    ///
    /// It is asked of the club and not of a competition because the club is the subject, and
    /// the season is required: a club's division is not a fact about the club, it is the
    /// enrolment it holds for that season — the same club is a different club in a different
    /// division next year, and a division read off the club itself would be a division that
    /// survives a relegation.
    /// </summary>
    [HttpGet("{teamId:guid}/standing")]
    public async Task<ActionResult<ClubStandingDto>> GetStanding(
        Guid teamId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var standing = await _standingsService.GetClubStandingAsync(teamId, seasonId, cancellationToken);
        return Ok(standing.ToDto());
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
    /// Draws the crest of the club the manager has taken charge of.
    ///
    /// <para>
    /// A request with neither a letter nor a figure is refused rather than stored as a blank
    /// shield: a crest is what a club is called by, and a coloured shape is not one. The refusal
    /// is <c>CrestWithoutElement</c>, which the editor shows without losing what was drawn.
    /// </para>
    /// </summary>
    [HttpPut("{id:guid}/crest")]
    public async Task<ActionResult<TeamDto>> UpdateCrest(
        Guid id,
        [FromBody] UpdateTeamCrestRequestDto request,
        CancellationToken cancellationToken)
    {
        CrestDesign? crest;

        try
        {
            crest = request.Text is null && request.Emblem is null
                ? null
                : new CrestDesign(
                    request.Shape,
                    request.PrimaryColor,
                    request.SecondaryColor,
                    request.Text is { } text
                        ? new CrestText(text.Content, text.Color, text.VerticalPosition)
                        : null,
                    request.Emblem is { } emblem
                        ? new CrestEmblem(emblem.Kind, emblem.Color, emblem.VerticalPosition)
                        : null);
        }
        catch (ArgumentException error)
        {
            throw new DomainValidationException("InvalidCrest", error.Message);
        }

        var team = await _teamService.UpdateCrestAsync(id, crest, cancellationToken);
        return Ok(team.ToDto());
    }

    /// <summary>
    /// Draws the two shirts of the club the manager has taken charge of.
    /// </summary>
    [HttpPut("{id:guid}/kits")]
    public async Task<ActionResult<TeamDto>> UpdateKits(
        Guid id,
        [FromBody] UpdateTeamKitsRequestDto request,
        CancellationToken cancellationToken)
    {
        KitDesign homeKit;
        KitDesign? awayKit;

        try
        {
            homeKit = ToKit(request.HomeKit);
            awayKit = request.AwayKit is null ? null : ToKit(request.AwayKit);
        }
        catch (ArgumentException error)
        {
            throw new DomainValidationException("InvalidKit", error.Message);
        }

        var team = await _teamService.UpdateKitsAsync(id, homeKit, awayKit, cancellationToken);
        return Ok(team.ToDto());
    }

    /// <summary>
    /// Puts one of the manager's own men in a shirt.
    ///
    /// <para>
    /// The answer is the number the man now wears rather than the whole squad, because the
    /// screen that sends this is holding the squad already and wants to change one cell of
    /// it. Re-reading twenty-three men to learn one number is a round trip the caller did not
    /// ask for, and it is a round trip that can answer after somebody else has changed a
    /// second row.
    /// </para>
    /// </summary>
    [HttpPut("{id:guid}/shirt-number")]
    public async Task<ActionResult<UpdateShirtNumberResponseDto>> UpdateShirtNumber(
        Guid id,
        [FromBody] UpdateShirtNumberRequestDto request,
        CancellationToken cancellationToken)
    {
        var number = await _teamService.UpdateShirtNumberAsync(
            id, request.PlayerId, request.ShirtNumber, cancellationToken);

        return Ok(new UpdateShirtNumberResponseDto
        {
            PlayerId = request.PlayerId,
            ShirtNumber = number
        });
    }

    /// <summary>
    /// Signs one of the manager's own men again.
    ///
    /// <para>
    /// The request carries the number of seasons and nothing else about the money. The wage is
    /// the server's to work out, because the salary on a contract is fixed for the run of that
    /// contract and the only moment it moves is here: a client that sent the wage would be a
    /// client that could renew a man for whatever it liked, and a renewal the club does not
    /// agree to is not a renewal.
    /// </para>
    /// <para>
    /// It is a POST because it writes an agreement, and the answer is the whole new shape of
    /// the deal rather than a bare ok — a manager who has just signed a man for three seasons
    /// at a new wage is owed the two numbers he agreed to, and the screen that sent this would
    /// otherwise have to guess them from the squad it is about to refetch.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/contract/renew")]
    public async Task<ActionResult<RenewContractResponseDto>> RenewContract(
        Guid id,
        [FromBody] RenewContractRequestDto request,
        CancellationToken cancellationToken)
    {
        var renewal = await _teamService.RenewContractAsync(
            id, request.PlayerId, request.Seasons, request.SeasonId, cancellationToken);

        return Ok(new RenewContractResponseDto
        {
            PlayerId = renewal.PlayerId,
            TeamId = renewal.TeamId,
            SeasonId = renewal.SeasonId,
            ContractId = renewal.ContractId,
            Seasons = renewal.Seasons,
            SeasonsLeft = renewal.SeasonsLeft,
            Wage = renewal.Wage
        });
    }

    /// <summary>
    /// The wire's kit as the domain's, with the domain doing the refusing. The controller maps
    /// and nothing else: a colour that is not a colour is a fact about the request, and the
    /// domain is where a club's clothes are a rule.
    /// </summary>
    private static KitDesign ToKit(KitDto kit) =>
        new(kit.PrimaryColor, kit.SecondaryColor, kit.Pattern, kit.TrimColor);

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
