using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Transfers;
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
    private readonly IDataSeeder _seeder;

    public TransferController(
        TransferService transferService,
        PlayerService playerService,
        IDataSeeder seeder)
    {
        _transferService = transferService;
        _playerService = playerService;
        _seeder = seeder;
    }

    /// <summary>
    /// The market: every player, narrowed by whatever the manager asked for. Filters travel
    /// together and only the ones that are set exclude anybody, so the answer is the whole list
    /// narrowed rather than a different list per box. The result is paginated, and the answer
    /// also says where the transfer window is, because a man the market offers is a man whose
    /// arrival depends on which round the world is in.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<TransferSearchResultDto>> Search(
        [FromQuery] TransferSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SeasonId == Guid.Empty)
        {
            throw new DomainValidationException("SeasonRequired", "A temporada é obrigatória.");
        }

        var filters = new TransferSearchFilters
        {
            Position = query.Position,
            MinAge = query.MinAge,
            MaxAge = query.MaxAge,
            MinStars = query.MinStars,
            MaxStars = query.MaxStars,
            MinSpeed = query.MinSpeed,
            MinAccuracy = query.MinAccuracy,
            MinDribbling = query.MinDribbling,
            MinHeading = query.MinHeading,
            MinStrength = query.MinStrength,
            MinGoalkeeperPower = query.MinGoalkeeperPower,
            MinReflexes = query.MinReflexes,
            Retiring = query.Retiring,
            FreeAgentsOnly = query.FreeAgentsOnly,
            WithClubOnly = query.WithClubOnly,
            TeamId = query.TeamId
        };

        var result = await _transferService.SearchTransfersAsync(
            query.SeasonId, filters, query.Page, query.PageSize, cancellationToken);

        return Ok(result.ToDto());
    }

    /// <summary>
    /// A single player as the market reads him, for the card that opens from the search: the
    /// same row plus his career split by club, which is the half a club reads before bidding.
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
        return Ok(history.Select(h => h.ToDto()).ToList());
    }

    /// <summary>
    /// The transfers that finished in the last three rounds, across every club of the division
    /// the manager's own club plays in. A market that shows what happened recently is a market
    /// a manager can read without opening a second screen.
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<DivisionRecentTransfersDto>> GetRecent(
        [FromQuery] Guid clubId,
        [FromQuery] int windowRounds = 3,
        CancellationToken cancellationToken = default)
    {
        var recent = await _transferService.GetDivisionRecentTransfersAsync(
            clubId, windowRounds, cancellationToken);
        return Ok(recent.ToDto());
    }

    /// <summary>
    /// Every transfer involving one club, across the seasons given, newest first. Pending and
    /// accepted sit in the same table as completed ones, because a proposal is a fact about
    /// the club's season whether or not the selling club has answered it yet.
    /// </summary>
    [HttpGet("club/{teamId:guid}/history")]
    public async Task<ActionResult<ClubTransferHistoryDto>> GetClubHistory(
        Guid teamId,
        [FromQuery] int[] seasonNumbers,
        CancellationToken cancellationToken)
    {
        var history = await _transferService.GetClubTransferHistoryAsync(
            teamId, seasonNumbers ?? Array.Empty<int>(), cancellationToken);
        return Ok(history.ToDto());
    }

    /// <summary>
    /// Proposes that a player join a club. The fee is optional: when omitted the asking price is
    /// offered, and a player with no club costs a signing fee rather than a price, because
    /// there is nobody to buy him from. The answer says which season and which round he arrives
    /// on.
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
    /// Answers a proposal addressed to the manager's club. The club answering is named in the
    /// request and checked against the seller: only the club holding the player can decide his
    /// price, and a buyer accepting its own offer would be writing its own cheque.
    /// </summary>
    [HttpPost("{transferId:guid}/answer")]
    public async Task<ActionResult<TransferProposalDto>> Answer(
        Guid transferId,
        [FromBody] TransferAnswerRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ClubId == Guid.Empty)
        {
            throw new DomainValidationException("ClubRequired", "O clube que responde deve ser informado.");
        }

        var proposal = await _transferService.AnswerTransferAsync(
            transferId, request.ClubId, request.Accept, cancellationToken);

        return Ok(proposal.ToDto());
    }

    /// <summary>
    /// Releases a player from his contract. The club pays the settlement the rules set, and it
    /// may not let a man go while doing so would leave it unable to field a side.
    /// </summary>
    [HttpPost("{playerId:guid}/release")]
    public async Task<ActionResult<ReleaseResultDto>> Release(
        Guid playerId,
        [FromQuery] Guid clubId,
        CancellationToken cancellationToken)
    {
        var outcome = await _transferService.ReleasePlayerAsync(playerId, clubId, cancellationToken);

        return Ok(new ReleaseResultDto
        {
            PlayerId = playerId,
            PlayerName = outcome.PlayerName,
            ClubId = clubId,
            ClubName = outcome.ClubName,
            ReleaseCost = outcome.Cost,
            WithdrawnOffers = outcome.WithdrawnOffers,
            Message = $"{outcome.PlayerName} foi dispensado por {outcome.Cost:N2} limos."
        });
    }

    /// <summary>
    /// Completes the accepted deals due to arrive in a season. The matchday sweep calls it when
    /// a window comes round; it is safe to call twice, and a season asked twice does not move a
    /// player or a coin twice.
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
            Accepted = completed
        });
    }

    /// <summary>
    /// Runs the market the clubs nobody is watching run. The matchday sweep calls it when a
    /// championship round closes.
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
            Signed = result.Signed,
            Answered = result.Answered
        });
    }

    /// <summary>
    /// The transfer rankings of a division: most players bought, most players sold, most
    /// money spent, and most profit made. Profit is net — fees received minus fees paid.
    /// </summary>
    [HttpGet("rankings")]
    public async Task<ActionResult<TransferRankingsDto>> GetRankings(
        [FromQuery] Guid clubId,
        CancellationToken cancellationToken)
    {
        var rankings = await _transferService.GetDivisionTransferRankingsAsync(clubId, cancellationToken);
        return Ok(rankings.ToDto());
    }

    /// <summary>
    /// Deals young free agents onto the market: no club, no contract, aged as the intake rule
    /// says.
    /// The backfill exists for a world written before the intake was a rule — one whose market
    /// holds three hundred players who all belong to somebody and nobody a club could sign —
    /// and it deals the same intake a season opening deals. It is drawn from the same pool the
    /// squads are drawn from, so the men it deals are goalkeepers where they are keepers and
    /// outfield players where they are outfield players.
    /// </summary>
    [HttpPost("seed-young-players")]
    public async Task<ActionResult> SeedYoungPlayers(
        [FromQuery] int count = YouthIntakeRules.FreeAgentsPerSeason,
        [FromQuery] Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        await _seeder.SeedYoungFreeAgentsAsync(count, seasonId, cancellationToken);
        return Ok();
    }
}
