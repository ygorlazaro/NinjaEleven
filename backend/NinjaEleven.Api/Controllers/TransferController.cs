using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// The transfer market. All routes are singular, as the contract requires.
/// </summary>
[ApiController]
[Route("transfer")]
[Produces("application/json")]
public class TransferController : ControllerBase
{
    private readonly TransferService _transferService;
    private readonly PlayerService _playerService;

    public TransferController(TransferService transferService, PlayerService playerService)
    {
        _transferService = transferService;
        _playerService = playerService;
    }

    /// <summary>
    /// The market: every player, optionally narrowed by position, retirement flag, or
    /// free-agent status. The answer is paginated so a list of three hundred and sixty
    /// men does not arrive at once.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<TransferSearchResultDto>> Search(
        [FromQuery] Guid seasonId,
        [FromQuery] Position? position,
        [FromQuery] bool? retiring,
        [FromQuery] bool freeAgents = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _transferService.SearchTransfersAsync(
            seasonId, position, retiring, freeAgents, page, pageSize, cancellationToken);

        return Ok(result.ToDto());
    }

    /// <summary>
    /// A single player as the market reads him, for the card that opens from the search.
    /// </summary>
    [HttpGet("listing/{playerId:guid}")]
    public async Task<ActionResult<TransferListingDto>> GetListing(
        Guid playerId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var player = await _playerService.GetByIdAsync(playerId, cancellationToken);
        var listing = await _transferService.GetPlayerListingAsync(player.Id, seasonId, cancellationToken);
        return Ok(listing.ToDto());
    }

    /// <summary>
    /// The manager's club inbox: proposals made to it and proposals it has made.
    /// </summary>
    [HttpGet("inbox/{clubId:guid}")]
    public async Task<ActionResult<TransferInboxDto>> GetInbox(
        Guid clubId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var inbox = await _transferService.GetInboxAsync(clubId, seasonId, cancellationToken);
        return Ok(inbox.ToDto());
    }

    /// <summary>
    /// A player's transfer history, newest first.
    /// </summary>
    [HttpGet("history/{playerId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TransferHistoryLineDto>>> GetHistory(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var history = await _transferService.GetPlayerHistoryAsync(playerId, cancellationToken);
        return Ok(history.ToDtos());
    }

    /// <summary>
    /// Proposes a transfer of a player to another club. The fee is optional: when omitted
    /// the asking price is offered.
    /// </summary>
    [HttpPost("propose")]
    public async Task<ActionResult<TransferProposalDto>> Propose(
        [FromBody] TransferProposeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.PlayerId == Guid.Empty)
        {
            throw new DomainValidationException("PlayerRequired", "Um jogador deve ser informado.");
        }

        if (request.BuyingClubId == Guid.Empty)
        {
            throw new DomainValidationException("ClubRequired", "O clube comprador deve ser informado.");
        }

        var proposal = await _transferService.ProposeTransferAsync(
            request.PlayerId, request.BuyingClubId, request.Fee, cancellationToken);

        return Ok(proposal.ToDto());
    }

    /// <summary>
    /// Answers a proposal addressed to the manager's club.
    /// </summary>
    [HttpPost("{transferId:guid}/answer")]
    public async Task<ActionResult<TransferProposalDto>> Answer(
        Guid transferId,
        [FromBody] TransferAnswerRequestDto request,
        CancellationToken cancellationToken)
    {
        var proposal = await _transferService.AnswerTransferAsync(
            transferId, request.Accept, cancellationToken);

        return Ok(proposal.ToDto());
    }

    /// <summary>
    /// Releases a player from his contract. The club pays the settlement the rules set.
    /// </summary>
    [HttpPost("{playerId:guid}/release")]
    public async Task<ActionResult<ReleaseResultDto>> Release(
        Guid playerId,
        [FromQuery] Guid clubId,
        CancellationToken cancellationToken)
    {
        var cost = await _transferService.ReleasePlayerAsync(playerId, clubId, cancellationToken);

        return Ok(new ReleaseResultDto
        {
            PlayerId = playerId,
            ClubId = clubId,
            ReleaseCost = cost,
            Message = $"Rescisão pagou {cost:N2} limos."
        });
    }

    /// <summary>
    /// Declares or clears a player's retirement for the season. Only a man of the age the
    /// rules allow may declare it.
    /// </summary>
    [HttpPost("{playerId:guid}/retire")]
    public async Task<ActionResult> SetRetiring(
        Guid playerId,
        [FromBody] TransferRetireRequestDto request,
        CancellationToken cancellationToken)
    {
        await _transferService.SetRetiringAsync(playerId, request.Retiring, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Completes accepted transfers whose arrival season and round have been reached.
    /// Called by the matchday sweep when the window opens.
    /// </summary>
    [HttpPost("complete")]
    public async Task<ActionResult<NpcTransferResultDto>> Complete(
        [FromQuery] Guid arrivalSeasonId,
        [FromQuery] int arrivalRound,
        CancellationToken cancellationToken)
    {
        var completed = await _transferService.CompletePendingTransfersAsync(
            arrivalSeasonId, arrivalRound, cancellationToken);

        return Ok(new NpcTransferResultDto
        {
            Completed = completed,
            Accepted = completed,
            ProposalsMade = 0,
            Rejected = 0
        });
    }

    /// <summary>
    /// Runs the NPC transfer market for a round. Called by the matchday sweep.
    /// </summary>
    [HttpPost("npc")]
    public async Task<ActionResult<NpcTransferResultDto>> CalculateNpc(
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.CalculateNpcTransfersAsync(seasonId, cancellationToken);
        return Ok(new NpcTransferResultDto
        {
            ProposalsMade = result.ProposalsMade,
            Accepted = result.Accepted,
            Rejected = result.Rejected,
            Completed = result.Completed
        });
    }
}
