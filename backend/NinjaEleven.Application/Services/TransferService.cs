using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Application.Services;

/// <summary>
/// What a release settled: the man it settled for, the club it settled with, the price, and
/// how many offers the club had on the table that went with him.
/// </summary>
public record ReleaseOutcome(string PlayerName, string ClubName, decimal Cost, int WithdrawnOffers);

/// <summary>
/// The transfer market: proposing, answering, completing, releasing and searching.
///
/// A transfer is a promise about the future rather than a fact about today, so every deal
/// carries the season it was made in and the season the player is due to arrive in. They are
/// not always consecutive, because the mid-season window is an arrival in the season a deal was
/// agreed in — and keeping them apart is what lets the player stay on his old club's books, and
/// on its pitch, while a buyer waits for the window to open.
///
/// Three things this service is careful about, and each of them was a way for the market to
/// quietly do nothing:
///
/// <list type="bullet">
///   <item>
///     A deal names the season it waits for by <em>number</em>. A world playing its first
///     season has no second one to point at, and a service that insisted on the row before the
///     season existed refused every deal a manager could possibly make.
///   </item>
///   <item>
///     The completion sweep is asked for the deals due in the season that just had its window,
///     by that season's number. Asking it for the deals proposed in it is a question with an
///     empty answer, and no player would ever arrive anywhere.
///   </item>
///   <item>
///     A player with no club is a player a club can sign. He is not a player a club can buy:
///     there is nobody to buy him from, so the deal has no seller, costs a signing fee rather
///     than a price, and nobody has to be asked.
///   </item>
/// </list>
/// </summary>
public class TransferService
{
    /// <summary>
    /// How many offers one club may put on the table in a single round. A club does its business
    /// when it wants to and a round is a reasonable amount of time to want to do it in, but a
    /// club that could table thirty offers a round would empty the league into itself.
    /// </summary>
    private const int MaxProposalsPerClubPerRound = 2;

    /// <summary>
    /// How many free agents one club may sign in a round while it is short of the minimum.
    /// A club below twenty-one is a club that cannot field a side, and the market's answer to
    /// that is a handful of men rather than a rebuild in one afternoon.
    /// </summary>
    private const int MaxSigningsPerClubPerRound = 3;

    private readonly ITransferRepository _transfers;
    private readonly ITeamRepository _teams;
    private readonly IPlayerRepository _players;
    private readonly ISeasonRepository _seasons;
    private readonly ICompetitionRepository _competitions;
    private readonly IRoundRepository _rounds;
    private readonly IMatchRepository _matches;
    private readonly IFinanceRepository _finance;
    private readonly InboxService _inbox;
    private readonly IManagedClubReader _managedClubs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        ITransferRepository transfers,
        ITeamRepository teams,
        IPlayerRepository players,
        ISeasonRepository seasons,
        ICompetitionRepository competitions,
        IRoundRepository rounds,
        IMatchRepository matches,
        IFinanceRepository finance,
        InboxService inbox,
        IManagedClubReader managedClubs,
        IUnitOfWork unitOfWork,
        ILogger<TransferService> logger)
    {
        _transfers = transfers;
        _teams = teams;
        _players = players;
        _seasons = seasons;
        _competitions = competitions;
        _rounds = rounds;
        _matches = matches;
        _finance = finance;
        _inbox = inbox;
        _managedClubs = managedClubs;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// The current season, which is the season every transfer is read against.
    /// </summary>
    private async Task<Season> GetCurrentSeasonAsync(CancellationToken ct) =>
        await _seasons.GetCurrentAsync(ct)
        ?? throw new DomainValidationException("NoSeason", "O mundo não tem uma temporada em andamento.");

    #region Proposing and answering

    /// <summary>
    /// Proposes that a player join a club, and says when he will get there.
    ///
    /// A player with a club is proposed for sale and the selling club is asked to answer: a
    /// market where a club disappears the moment another one makes an offer is a market where a
    /// bargain is gone before a rival can blink. A player with no club is signed, and there is
    /// nobody to answer — the deal is agreed on the spot and waits for the same window as any
    /// other, because a man who joins a club mid-round still has not played a match in it.
    /// </summary>
    /// <param name="playerId">The player being proposed.</param>
    /// <param name="buyingClubId">The club making the proposal.</param>
    /// <param name="fee">The fee offered, or null to offer the asking price.</param>
    public async Task<TransferProposal> ProposeTransferAsync(
        Guid playerId,
        Guid buyingClubId,
        decimal? fee = null,
        CancellationToken cancellationToken = default)
    {
        var season = await GetCurrentSeasonAsync(cancellationToken);

        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var state = await _players.GetSeasonStateForUpdateAsync(playerId, season.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        var buyingClub = await _teams.GetAsync(buyingClubId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", buyingClubId);

        if (await SquadSizeAsync(buyingClubId, cancellationToken) is var size && !SquadSizeRules.CanAddOne(size))
        {
            throw new DomainValidationException(
                "SquadFull",
                $"O elenco do {buyingClub.Name} já tem {size} jogadores, e o limite é {SquadSizeRules.MaxSquadSize}.");
        }

        if (await _transfers.ExistsAcceptedAsync(playerId, cancellationToken))
        {
            throw new DomainValidationException(
                "TransferAccepted", "O jogador já tem uma proposta aceita.");
        }

        var window = await ReadTheWindowAsync(season, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var arrivalSeason = await _seasons.GetByNumberAsync(window.ArrivalSeasonNumber, cancellationToken);

        // A man with no club is signed rather than bought. The two cases are asked with the
        // club id itself rather than with a flag, because the flag is a second copy of a fact
        // the compiler cannot check and the reader cannot verify: after this branch, the
        // player has a club, and everything below reads it.
        if (state.TeamId is not { } currentClubId)
        {
            // Nobody to ask and a price all the same: a man with no club is not being sold, he is
            // being picked up, and a deal that waited for an answer would be a deal waiting for a
            // club that does not exist. The fee is the rule's — a share of what he is worth — and
            // not the number the caller happened to type, because a price is a negotiation and
            // there is nobody on the other side of it to negotiate with.
            var signingFee = PlayerValuation.FreeAgentSigningFee(PlayerValuation.MarketValue(player, state));

            var deal = Transfer.Propose(
                playerId,
                sellingClubId: null,
                buyingClubId,
                season.Id,
                window.ArrivalSeasonNumber,
                arrivalSeason?.Id,
                signingFee,
                today,
                window.ArrivalRoundNumber);

            // Nobody to ask: a man with no club is not being sold, he is being picked up, and a
            // deal that waited for an answer would be a deal waiting for a club that does not
            // exist. It is still a promise about the future, so it still waits for the window.
            deal.Accept(today);

            await _transfers.AddAsync(deal, cancellationToken);
            await MoveTheMoneyAsync(deal, season, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Signing agreed: {PlayerName} to {BuyingClub} for {Fee} limos, arriving in season " +
                "{Arrival} round {Round}.",
                player.Name, buyingClub.Name, signingFee,
                window.ArrivalSeasonNumber, window.ArrivalRoundNumber);

            return BuildProposal(deal, player, sellingClub: null, buyingClub, season.Number);
        }

        if (currentClubId == buyingClubId)
        {
            throw new DomainValidationException(
                "CannotBuyFromYourself", "Um clube não pode propor a compra de seu próprio jogador.");
        }

        var membership = await ResolveContractAsync(playerId, currentClubId, cancellationToken)
            ?? throw new DomainValidationException(
                "PlayerWithoutContract", "O jogador não tem contrato ativo.");

        var sellingClub = await _teams.GetAsync(currentClubId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", currentClubId);

        var marketValue = PlayerValuation.MarketValue(player, state);
        var askingPrice = fee ?? PlayerValuation.AskingPrice(
            marketValue, membership.IsInHisLastSeason(season.Number));

        var transfer = Transfer.Propose(
            playerId,
            sellingClub.Id,
            buyingClub.Id,
            season.Id,
            window.ArrivalSeasonNumber,
            arrivalSeason?.Id,
            askingPrice,
            today,
window.ArrivalRoundNumber);

        // A club without a manager is not a club that forgets: it is a club that has to be told
        // when to decide. The deadline is between one and five rounds ahead of the round the
        // proposal was made in, so a bid left on an NPC club desk does not sit there until the
        // season closes — the sweep answers it, the player is free again, and the market keeps
        // moving. A club with a manager answers on its own schedule and carries no deadline.
        //
        // The seed is the player and the season, so a replayed proposal is given the same
        // deadline: the deadline is a fact about this deal, and a deal that replays the same has
        // to answer by the same round.
        if (!sellingClub.IsManagerClub)
        {
            var proposalRound = await CurrentChampionshipRoundAsync(season, cancellationToken);
            var deadline = proposalRound + new Random(SeedFor(playerId, season.Id)).Next(1, 6);
            transfer.SetDecisionDeadline(proposalRound, deadline);
        }

        await _transfers.AddAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A bid on one of the manager's own men is news the moment it is made, and the market
        // screen is where he would look for it if he happened to be there. Only the club that
        // owns the player is told, because the other thirty five have nobody to tell: a proposal
        // the engine makes on its own is not something a computer-controlled club reads.
        if (sellingClub.IsManagerClub)
        {
            await _inbox.PostTransferOfferAsync(
                new TransferOfferFacts
                {
                    RecipientTeamId = sellingClub.Id,
                    ClubName = sellingClub.Name,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    BiddingClubId = buyingClub.Id,
                    BiddingClubName = buyingClub.Name,
                    Fee = askingPrice,
                    // The price the club would have asked for is not the interesting number
                    // when the two are the same figure — what the book says the man is worth
                    // is, and it is the one the manager compares an offer against.
                    AskingPrice = marketValue,
                    AnswerByRound = transfer.AnswerByRound,
                    Reference = $"offer:{transfer.Id}"
                },
                cancellationToken);
        }

        _logger.LogInformation(
            "Transfer proposed: {PlayerName} from {SellingClub} to {BuyingClub} for {Fee} limos, " +
            "arriving in season {Arrival} round {Round}.",
            player.Name, sellingClub.Name, buyingClub.Name, askingPrice,
            window.ArrivalSeasonNumber, window.ArrivalRoundNumber);

        return BuildProposal(transfer, player, sellingClub, buyingClub, season.Number);
    }

    /// <summary>
    /// Answers a proposal addressed to one club.
    ///
    /// The club named has to be the selling club, and that is checked rather than assumed: a
    /// buying club accepting its own proposal is a market where a manager sets his own price and
    /// signs his own cheque, and a selling club refusing is the veto a club has over who wears
    /// its shirt. A signing has no selling club and is never pending, so it never arrives here.
    /// </summary>
    public async Task<TransferProposal> AnswerTransferAsync(
        Guid transferId,
        Guid clubId,
        bool accept,
        CancellationToken cancellationToken = default)
    {
        var season = await GetCurrentSeasonAsync(cancellationToken);

        var transfer = await _transfers.GetAsync(transferId, cancellationToken)
            ?? throw new EntityNotFoundException("Transfer", transferId);

        if (transfer.Status != TransferStatus.Pending)
        {
            throw new DomainValidationException(
                "TransferNotPending", "A proposta não está mais pendente.");
        }

        if (transfer.SellingClubId is null)
        {
            throw new DomainValidationException(
                "TransferNotForSale", "Uma contratação de jogador livre não espera resposta de ninguém.");
        }

        if (transfer.SellingClubId != clubId)
        {
            throw new DomainValidationException(
                "NotTheSellingClub", "Só o clube que tem o jogador na lista pode responder à proposta.");
        }

        var sellingClub = await _teams.GetAsync(transfer.SellingClubId.Value, cancellationToken)
            ?? throw new EntityNotFoundException("Team", transfer.SellingClubId.Value);

        var buyingClub = await _teams.GetAsync(transfer.BuyingClubId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", transfer.BuyingClubId);

        var player = await _players.GetAsync(transfer.PlayerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", transfer.PlayerId);

        var resolvedAt = DateOnly.FromDateTime(DateTime.Now);

        if (accept)
        {
            // The bound is asked here rather than at the window, and the window would be too
            // late. The manager's club is the one club the market never checks for it, because
            // his offers are his own decisions — so a sale accepted without this leaves his
            // club one man short of a side he can put on the pitch, and it is the answer, not the
            // proposal, that is the moment the club agreed to it.
            var squad = await SquadSizeAsync(transfer.SellingClubId.Value, cancellationToken);
            if (!SquadSizeRules.CanRemoveOne(squad))
            {
                throw new DomainValidationException(
                    "SquadTooSmallToSell",
                    $"O elenco tem {squad} jogadores e o mínimo é {SquadSizeRules.MinSquadSize}: " +
                    $"vender {player.Name} deixaria o clube sem um time para jogar.");
            }

            transfer.Accept(resolvedAt);

            // The money changes hands here, where the club agreed to the deal, and not at the
            // window: a sale accepted today has been paid for today, and a seller that has agreed
            // to a price is a seller that has been paid it even if the player has not walked out
            // of the door yet.
            await MoveTheMoneyAsync(transfer, season, cancellationToken);
        }
        else
        {
            transfer.Reject(resolvedAt);
        }

        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Transfer {TransferId} for {PlayerName}: {Decision} by {SellingClub}.",
            transferId, player.Name, accept ? "aceita" : "recusada", sellingClub.Name);

        return BuildProposal(transfer, player, sellingClub, buyingClub, season.Number);
    }

    /// <summary>
    /// Gives up on every proposal a club never answered in the season being closed.
    ///
    /// A proposal nobody answered is not a deal in progress, it is a rumour. Left alive it would
    /// go on saying a player is spoken for — which is exactly what the market reads to refuse a
    /// second offer — for ever, and a player nobody could bid for again would be a player the
    /// world had quietly deleted from its own market.
    /// </summary>
    public async Task<int> ExpireUnansweredAsync(
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _transfers.ListPendingAsync(proposalSeasonId, cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var transfer in pending)
        {
            transfer.Expire(today);
            _transfers.Update(transfer);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await ReportTheDecisionsAsync(pending, _ => InboxDecision.Expired, cancellationToken);

        _logger.LogInformation(
            "{Count} unanswered proposals expired at the end of the season.", pending.Count);

        return pending.Count;
    }

    #endregion

    #region Releasing

    /// <summary>
    /// Releases a player from his contract, ending the membership and paying the settlement the
    /// rules say the club owes.
    ///
    /// A release is a club's decision, not the player's — the rules calculate what the club still
    /// owes and halve it, and that is what leaves the club's book. The player becomes a free
    /// agent: his season state keeps his goals, but his team is cleared. And a club may only let
    /// go while it can still field a side: the settlement is a price for the men on a list, not a
    /// way of ending up with none.
    ///
    /// The two names come back with the cost because a release is reported to a manager, and a
    /// settlement read without the name of the man it settled for is a number on a screen that
    /// belongs to nobody.
    /// </summary>
    public async Task<ReleaseOutcome> ReleasePlayerAsync(
        Guid playerId,
        Guid clubId,
        CancellationToken cancellationToken = default)
    {
        var season = await GetCurrentSeasonAsync(cancellationToken);
        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var squad = await SquadSizeAsync(clubId, cancellationToken);
        if (!SquadSizeRules.CanRemoveOne(squad))
        {
            throw new DomainValidationException(
                "SquadTooSmallToRelease",
                $"O clube tem {squad} jogadores e o mínimo é {SquadSizeRules.MinSquadSize}: " +
                "ele não pode rescindir com o elenco abaixo do mínimo.");
        }

        var contract = await ResolveContractAsync(playerId, clubId, cancellationToken)
            ?? throw new DomainValidationException(
                "PlayerNotContracted", "O jogador não tem contrato ativo neste clube.");

        var state = await _players.GetSeasonStateForUpdateAsync(playerId, season.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        var salary = PlayerValuation.SeasonWage(player, state);
        var roundsLeft = await _seasons.GetChampionshipRoundsLeftAsync(season.Id, cancellationToken);

        // The same rule the squad list and a player's card quote from, so the figure a manager
        // read before pressing the button is the figure that is charged.
        var cost = ReleaseRules.QuoteReleaseCost(
            salary,
            CompetitionRules.LeagueMatchDays,
            roundsLeft,
            contract.SeasonsLeft(season.Number));

        var today = DateOnly.FromDateTime(DateTime.Now);
        contract.End(today);
        _teams.UpdateMembership(contract);

        // Every offer the club had on the table for this man is withdrawn with him.
        //
        // An offer left standing is not a courtesy, it is a lie the world believes: it says a
        // club holds a player nobody will deliver, it keeps him "spoken for" so no rival may
        // bid for a free agent, and it sits in a buyer's list as a purchase that cannot happen.
        // The completion sweep would reject it at the window, which stops the money — but a
        // deal that cannot be kept should end when it stops being possible, not when the
        // calendar next comes round to notice.
        var withdrawn = await _transfers.ListPendingOffersForSellerAsync(
            clubId, playerId, cancellationToken);

        foreach (var offer in withdrawn)
        {
            offer.Expire(today);
            _transfers.Update(offer);
        }

        var clubName = (await _teams.GetAsync(clubId, cancellationToken))?.Name ?? clubId.ToString();
        var lastLine = await _finance.GetLastAsync(clubId, cancellationToken);
        var sequence = (lastLine?.Sequence ?? 0) + 1;
        var balanceBefore = lastLine?.BalanceAfter ?? 0m;

        var settlement = FinanceMovement.Create(
            clubId,
            season.Id,
            sequence,
            matchDayNumber: null,
            FinanceMovementKind.TransferOut,
            $"Rescisão de contrato — {player.Name}",
            -cost,
            balanceBefore,
            matchId: null,
            $"release:{playerId}");

        await _finance.AddAsync(settlement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The player is now a free agent, and a free agent is a man on the market: his state
        // carries his goals but names no club, and the market reads that state to know he is
        // for sale. A release that only ended the contract would leave a man the club no longer
        // pays for and a season state still saying he plays for them — a player the manager
        // paid to get rid of and cannot buy back.
        state.SetTeam(null);
        _players.UpdateSeasonState(state);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Released {PlayerName} from {ClubName}: {Cost} limos for {Rounds} rounds of wages left, " +
            "and {Withdrawn} offers withdrawn.",
            player.Name, clubName, cost, roundsLeft, withdrawn.Count);

        return new ReleaseOutcome(player.Name, clubName, cost, withdrawn.Count);
    }

    #endregion

    #region Completing

    /// <summary>
    /// Completes every accepted deal due to arrive in the season given, pinning the arrival round
    /// and moving the players.
    ///
    /// A transfer is a promise about the future and the window is what makes it today: when a
    /// window comes round, the two memberships are swapped and the player walks through the door.
    /// The fee is not the window's to move — it was spent when the deal was agreed, so a manager
    /// read what the purchase cost on the day he made it — and a deal that cannot be kept after
    /// the money has changed hands is called off and the money given back, because a transfer
    /// that did not happen is not a transfer and a club's book records what happened.
    ///
    /// It is asked for by the arrival season, which is the season the deals name — the one the
    /// calendar has just reached — and it is idempotent: a deal already completed is not in the
    /// list at all, and a refund is written with the deal's own reference, so a season asked
    /// twice does not move a player or a coin twice.
    /// </summary>
    /// <param name="arrivalSeasonId">The season whose window has come round.</param>
    /// <param name="arrivalRound">
    /// The round being entered, which is what the completed deal records as the round the player
    /// walked in on.
    /// </param>
    public async Task<int> CompletePendingTransfersAsync(
        Guid arrivalSeasonId,
        int arrivalRound,
        CancellationToken cancellationToken = default)
    {
        var arrivalSeason = await _seasons.GetAsync(arrivalSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", arrivalSeasonId);

        var accepted = await _transfers.ListAcceptedAsync(arrivalSeason.Number, cancellationToken);
        if (accepted.Count == 0)
        {
            return 0;
        }

        var completed = 0;

        // The deals that could not be kept are collected and written once at the end rather
        // than one inside the loop: the window settles the whole window, and a caller that
        // awaits a message per deal is a caller that asks the world three names per deal.
        var calledOff = new List<Transfer>();

        // The shirts dealt in this window, per club, so two men arriving at the same club in
        // the same window are put in two different shirts. Without it the second read of the
        // dressing room would not see the first signing — the membership is written but the
        // save happens once at the end of the loop — and both would be handed the same number,
        // which is a clash the world would discover as a failed window rather than as a shirt.
        var dealtShirts = new Dictionary<Guid, HashSet<int>>();

        // The men signed earlier in this same window. The guard below asks the club's book
        // whether it already holds the player, and that book is the database — which is
        // exactly right for a player signed in an earlier window and exactly wrong for one
        // signed four deals ago in this one, because the save happens once at the end of the
        // loop. So a second deal for the same man passed the guard, wrote a second contract,
        // and left a club holding one player twice: a squad of twenty-three that is
        // twenty-four men with one of them in two shirts, and a squad screen that dies on a
        // duplicate key instead of showing it. The same reason the shirts are tracked here.
        //
        // Keyed by the man and not by the club, because that is the shape of the rule: a
        // player is signed once in a window, and two deals naming him to two different clubs
        // are not a man moving twice, they are one move written down twice. Keyed by club,
        // the two-club case slips through and the man ends up on two books at once — 51 of
        // them were, in the season this was found.
        var signedThisWindow = new HashSet<Guid>();

        // Who is in charge of a club is a fact about the world and not about this window, so it
        // is read once here and read at all: a window that moves forty men asks the question of
        // nobody forty times, because the answer is a property of the world rather than of the
        // deal. A world of nobody is the answer that ends the reporting below before it starts,
        // so a world with no manager behind it never pays for a read it would not deliver.
        var managed = (await _managedClubs.ListManagedClubsAsync(cancellationToken)).ToHashSet();

        // The men this window moved out of a club, with the man named once so the message can
        // say who rather than look him up per line. They are held rather than messaged in the
        // loop because the message is a decision about a division, and a division is a fact
        // about the whole window — one read of it, after the loop, instead of a question per
        // deal about something that is the same answer for every one of them.
        var departures = new List<CompletedDeparture>();

        foreach (var transfer in accepted)
        {
            if (transfer.ArrivalRoundNumber is null)
            {
                transfer.SetArrivalRound(arrivalRound);
            }

            var buyer = await _teams.GetAsync(transfer.BuyingClubId, cancellationToken);
            var buyerSize = await SquadSizeAsync(transfer.BuyingClubId, cancellationToken);

            if (!SquadSizeRules.CanAddOne(buyerSize))
            {
                _logger.LogWarning(
                    "Transfer {TransferId}: {BuyingClub} is full at {Size} players. The deal waits for the next window.",
                    transfer.Id, buyer?.Name, buyerSize);
                _transfers.Update(transfer);
                continue;
            }

            // The invariant rather than the race: a club does not hold the same man twice, and
            // no caller gets to decide that by being first. The claim further down settles
            // which of two simultaneous callers writes the membership; this settles what a
            // membership *is* — a man the club has signed, not a row it happens to be about to
            // add. A deal whose buyer already holds him is a deal that died between being
            // agreed and being completed, and it is called off rather than kept, because the
            // alternative is a squad of twenty-three that is twenty-four men with one of them
            // twice — and a shirt number dealt to the second copy that nobody can see.
            var alreadyHere = signedThisWindow.Contains(transfer.PlayerId)
                || (await _teams.GetLiveContractsAsync(transfer.BuyingClubId, cancellationToken))
                    .Any(contract => contract.PlayerId == transfer.PlayerId);

            if (alreadyHere)
            {
                await CallOffTheDealAsync(transfer, arrivalSeason, calledOff, cancellationToken);
                _logger.LogWarning(
                    "Transfer {TransferId}: player {PlayerId} is already signed in this window. " +
                    "The deal is off.",
                    transfer.Id, transfer.PlayerId);
                continue;
            }

            // The seller is written off only when there is one: a signing has nobody to sell him
            // to, and a deal whose seller has already let the player go is a deal that died
            // between being agreed and being completed, not one to complete anyway.
            if (transfer.SellingClubId is { } sellerId)
            {
                var sellingClub = await _teams.GetAsync(sellerId, cancellationToken);
                var sellingClubName = sellingClub?.Name ?? sellerId.ToString();
                var sellerContracts = await _teams.GetLiveContractsAsync(sellerId, cancellationToken);
                var oldMembership = sellerContracts.FirstOrDefault(m => m.PlayerId == transfer.PlayerId);

                if (oldMembership is null)
                {
                    await CallOffTheDealAsync(transfer, arrivalSeason, calledOff, cancellationToken);
                    _logger.LogWarning(
                        "Transfer {TransferId}: the selling club no longer holds player {PlayerId}. The deal is off.",
                        transfer.Id, transfer.PlayerId);
                    continue;
                }

                // The seller's own book is asked again here, and this is the only place it can
                // be. Every other check on this deal was made when the deal was made, against a
                // club that has since signed somebody or let somebody go: a club of twenty-two
                // that agreed to sell one man and has since released another would pass both
                // checks and arrive at the window one player short of a side it can put on the
                // pitch. The window is the moment the man actually leaves, so it is the moment
                // the bound is real, and a deal that would break it is off rather than late.
                var sellerSize = await SquadSizeAsync(sellerId, cancellationToken);
                if (!SquadSizeRules.CanRemoveOne(sellerSize))
                {
                    await CallOffTheDealAsync(transfer, arrivalSeason, calledOff, cancellationToken);
                    _logger.LogWarning(
                        "Transfer {TransferId}: {SellingClub} is down to {Size} players and cannot " +
                        "sell another. The deal is off.",
                        transfer.Id, sellingClubName, sellerSize);
                    continue;
                }

                oldMembership.End(DateOnly.FromDateTime(DateTime.Now));
                _teams.UpdateMembership(oldMembership);
            }

            // The deal is taken here and not at the top of the loop, because a claim is a
            // promise that this caller is about to sign the player: taken earlier it would
            // mark a deal completed whose buyer was full and which is only waiting for the
            // next window, and a deal marked completed is a deal nobody signs again.
            //
            // Everything above is a question this deal might still fail, and a failed deal
            // must be left exactly as it was found. Past this line it cannot fail: the row is
            // this caller's, the membership below belongs to exactly one signing, and the
            // claim and the membership commit together or not at all.
            if (!await _transfers.TryClaimForCompletionAsync(transfer.Id, cancellationToken))
            {
                _logger.LogInformation(
                    "Transfer {TransferId} was completed by another caller; this one did not sign him again.",
                    transfer.Id);
                continue;
            }

            // The man is moved to the buying club before the contract is written, because the
            // contract carries a wage and a wage is worked out from the season he is arriving
            // into. The two writes go in the same commit either way; the order is here so that
            // the number on the contract is the number the rule produces for him today rather
            // than a second reading of the same man.
            var state = await MoveTheStateAsync(transfer, arrivalSeason, cancellationToken);
            var player = await _players.GetAsync(transfer.PlayerId, cancellationToken);

            signedThisWindow.Add(transfer.PlayerId);

            await _teams.AddMembershipAsync(
                TeamMembership.Create(
                    transfer.PlayerId,
                    transfer.BuyingClubId,
                    DateOnly.FromDateTime(DateTime.Now),
                    contractSeasons: FinanceRules.DefaultContractSeasons,
                    startSeasonNumber: arrivalSeason.Number,
                    shirtNumber: await DealTheShirtAsync(
                        transfer.BuyingClubId, transfer.PlayerId, dealtShirts, cancellationToken),
                    wage: player is null ? 0m : PlayerValuation.SeasonWage(player, state)),
                cancellationToken);

            // The status was written by the claim, atomically and only once. This update is
            // here for the arrival round the deal records, which the claim does not set.
            transfer.Complete(DateOnly.FromDateTime(DateTime.Now));
            _transfers.Update(transfer);
            completed++;

            departures.Add(new CompletedDeparture(
                transfer,
                transfer.SellingClubId,
                transfer.BuyingClubId,
                player));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await ReportTheDeparturesAsync(departures, managed, arrivalSeason, cancellationToken);
        await ReportTheDecisionsAsync(calledOff, _ => InboxDecision.CalledOff, cancellationToken);

        _logger.LogInformation(
            "Completed {Completed} transfers for arrival season {SeasonNumber} at round {Round}.",
            completed, arrivalSeason.Number, arrivalRound);

        return completed;
    }

    /// <summary>
    /// The shirt a signing is given: the lowest number nobody on this club's books is wearing,
    /// asked of the position he plays so that the number one stays a goalkeeper's.
    ///
    /// <para>
    /// A signing is not offered a number to pick from. The market brings a player, not a shirt,
    /// and a man who arrives in a club whose eleven already wears 2 through 22 takes 23 —
    /// which is why the rule is "the lowest one that is free" and not "the next one in
    /// sequence", and why a club that has deliberately moved somebody out of the way does not
    /// hand that same number to the next man through the door.
    /// </para>
    ///
    /// <para>
    /// The numbers dealt earlier in this same window are remembered rather than re-read,
    /// because the membership carrying them is written but not yet saved and the second read
    /// of the dressing room would not contain it.
    /// </para>
    /// </summary>
    private async Task<int> DealTheShirtAsync(
        Guid buyingClubId,
        Guid playerId,
        Dictionary<Guid, HashSet<int>> dealtInThisWindow,
        CancellationToken cancellationToken)
    {
        if (!dealtInThisWindow.TryGetValue(buyingClubId, out var worn))
        {
            var contracts = await _teams.GetLiveContractsAsync(buyingClubId, cancellationToken);
            worn = contracts
                .Where(contract => contract.ShirtNumber.HasValue)
                .Select(contract => contract.ShirtNumber!.Value)
                .ToHashSet();

            dealtInThisWindow[buyingClubId] = worn;
        }

        var player = await _players.GetAsync(playerId, cancellationToken);
        var number = ShirtNumberRules.For(worn, player?.Position == Domain.Enums.Position.GK);

        worn.Add(number);
        return number;
    }

    /// <summary>
    /// The fee, out of one club's book and into the other's, in two lines with one reference
    /// each so a deal settled twice is settled once.
    ///
    /// This runs when the deal is **agreed**, not when the player arrives, and it is the whole
    /// reason a manager reads his balance on the day he makes an offer rather than eleven rounds
    /// later: a purchase is a decision, and a decision that only shows up in the books when the
    /// man finally walks through the door is a decision the manager took without seeing what it
    /// cost. The window still moves the player, the contract and the season state — the money is
    /// not the window's to move, it was spent when the deal was struck.
    ///
    /// A signing is a cost with nobody on the other side: the buyer is debited the signing fee
    /// and there is no seller to credit, because a man with no club was not sold to anybody.
    ///
    /// The season the movement is written into is the season the deal was agreed in, which is
    /// not always the season the player arrives: a deal struck in one season and honoured in the
    /// next cost the club the money in the season it decided to spend it.
    /// </summary>
    private async Task MoveTheMoneyAsync(
        Transfer transfer,
        Season season,
        CancellationToken cancellationToken)
    {
        if (transfer.Fee <= 0m)
        {
            return;
        }

        var buyingReference = $"transfer:{transfer.Id}:purchase";

        var playerName = (await _players.GetAsync(transfer.PlayerId, cancellationToken))?.Name
                         ?? transfer.PlayerId.ToString();

        if (!await _finance.ExistsWithReferenceAsync(
                transfer.BuyingClubId, season.Id, FinanceMovementKind.TransferOut, buyingReference, cancellationToken))
        {
            var buyingLast = await _finance.GetLastAsync(transfer.BuyingClubId, cancellationToken);
            var purchase = FinanceMovement.Create(
                transfer.BuyingClubId,
                season.Id,
                sequence: (buyingLast?.Sequence ?? 0) + 1,
                (await CurrentMatchDayAsync(season.Id, cancellationToken))?.Number,
                FinanceMovementKind.TransferOut,
                transfer.SellingClubId is null ? $"Contratação de {playerName}" : $"Compra de {playerName}",
                -transfer.Fee,
                balanceBefore: buyingLast?.BalanceAfter ?? 0m,
                matchId: null,
                buyingReference);

            await _finance.AddAsync(purchase, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (transfer.SellingClubId is not { } sellerId)
        {
            return;
        }

        var sellingReference = $"transfer:{transfer.Id}:sale";

        if (!await _finance.ExistsWithReferenceAsync(
                sellerId, season.Id, FinanceMovementKind.TransferIn, sellingReference, cancellationToken))
        {
            var sellingLast = await _finance.GetLastAsync(sellerId, cancellationToken);
            var sale = FinanceMovement.Create(
                sellerId,
                season.Id,
                sequence: (sellingLast?.Sequence ?? 0) + 1,
                (await CurrentMatchDayAsync(season.Id, cancellationToken))?.Number,
                FinanceMovementKind.TransferIn,
                $"Venda de {playerName}",
                transfer.Fee,
                balanceBefore: sellingLast?.BalanceAfter ?? 0m,
                matchId: null,
                sellingReference);

            await _finance.AddAsync(sale, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gives the fee back to the club that paid it, when a deal that was already paid for falls
    /// through at the window.
    ///
    /// The money is taken when the deal is agreed, so a deal that dies between the agreement and
    /// the player's arrival — the buyer has filled up, the seller has fallen to the minimum, the
    /// player was let go in between — is a club that has paid for a player it is not getting. The
    /// buyer is made whole and the seller takes the money back, in the season the deal died in,
    /// because a transfer that did not happen is not a transfer and a club's book is a record of
    /// what happened rather than of what was once promised.
    /// </summary>
    private async Task RefundTheMoneyAsync(
        Transfer transfer,
        Season season,
        CancellationToken cancellationToken)
    {
        if (transfer.Fee <= 0m)
        {
            return;
        }

        var playerName = (await _players.GetAsync(transfer.PlayerId, cancellationToken))?.Name
                         ?? transfer.PlayerId.ToString();
        var buyingReference = $"transfer:{transfer.Id}:refund:purchase";

        if (!await _finance.ExistsWithReferenceAsync(
                transfer.BuyingClubId, season.Id, FinanceMovementKind.TransferIn, buyingReference, cancellationToken))
        {
            var buyingLast = await _finance.GetLastAsync(transfer.BuyingClubId, cancellationToken);
            var refund = FinanceMovement.Create(
                    transfer.BuyingClubId,
                    season.Id,
                    sequence: (buyingLast?.Sequence ?? 0) + 1,
                    (await CurrentMatchDayAsync(season.Id, cancellationToken))?.Number,
                    FinanceMovementKind.TransferIn,
                    $"Estorno da compra de {playerName}",
                    transfer.Fee,
                    balanceBefore: buyingLast?.BalanceAfter ?? 0m,
                    matchId: null,
                buyingReference);

            await _finance.AddAsync(refund, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (transfer.SellingClubId is not { } sellerId)
        {
            return;
        }

        var sellingReference = $"transfer:{transfer.Id}:refund:sale";

        if (!await _finance.ExistsWithReferenceAsync(
                sellerId, season.Id, FinanceMovementKind.TransferOut, sellingReference, cancellationToken))
        {
            var sellingLast = await _finance.GetLastAsync(sellerId, cancellationToken);
            var clawback = FinanceMovement.Create(
                    sellerId,
                    season.Id,
                    sequence: (sellingLast?.Sequence ?? 0) + 1,
                    (await CurrentMatchDayAsync(season.Id, cancellationToken))?.Number,
                    FinanceMovementKind.TransferOut,
                    $"Estorno da venda de {playerName}",
                    -transfer.Fee,
                    balanceBefore: sellingLast?.BalanceAfter ?? 0m,
                    matchId: null,
                sellingReference);

            await _finance.AddAsync(clawback, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Ends a deal that cannot be kept, and gives the money back to whoever paid for it.
    /// </summary>
    private async Task CallOffTheDealAsync(
        Transfer transfer,
        Season season,
        ICollection<Transfer> calledOff,
        CancellationToken cancellationToken)
    {
        transfer.CallOff(DateOnly.FromDateTime(DateTime.Now));
        _transfers.Update(transfer);
        await RefundTheMoneyAsync(transfer, season, cancellationToken);
        calledOff.Add(transfer);
    }

    /// <summary>
    /// Puts the player on his new club's books for the season he is arriving in.
    ///
    /// The state is found rather than assumed. A mid-season arrival finds the state the player
    /// has been playing the season with and changes its club; an arrival in a newly opened season
    /// finds the state the season opening gave him. A world that somehow has neither is healed
    /// here instead of stalling: a contract with no season state behind it is a player who
    /// belongs to a club and cannot be picked, and the market refusing to complete the deal would
    /// only leave the world in that state for longer.
    /// </summary>
    private async Task<PlayerSeasonState> MoveTheStateAsync(
        Transfer transfer,
        Season arrivalSeason,
        CancellationToken cancellationToken)
    {
        var state = await _players.GetSeasonStateForUpdateAsync(
            transfer.PlayerId, arrivalSeason.Id, cancellationToken);

        if (state is null)
        {
            state = PlayerSeasonState.Create(
                transfer.PlayerId, arrivalSeason.Id, transfer.BuyingClubId, FullSeasonEnergy);
            await _players.AddSeasonStateAsync(state, cancellationToken);
        }

        state.SetTeam(transfer.BuyingClubId);
        _players.UpdateSeasonState(state);

        return state;
    }

    /// <summary>
    /// A player who starts a season starts it able to play a match. Energy is spent over a
    /// season and is measured against the calendar, and a season opens with everyone fit.
    /// </summary>
    private const int FullSeasonEnergy = 100;

    #endregion

    #region The market run by the clubs nobody is watching

    /// <summary>
    /// The NPC transfer market: the clubs the manager is not playing run their own business
    /// alongside him, and they do it when the championship's round closes rather than on a timer,
    /// so a matchday left running over a weekend picks up exactly the business it would have done.
    ///
    /// A club short of the minimum signs free agents first, because a club that cannot field a
    /// side has no use for a striker. A club with room then goes looking, and what it looks for is
    /// a position it is thin in rather than the best man in the country — which is why the target
    /// is scored against the buyer's own squad.
    ///
    /// The selling club decides, by the rules: `(50 - Age) * Stars * SeasonsRemaining` against a
    /// roll from one to six hundred, and the deal is made when the roll beats the formula. A
    /// young man on a long contract is expensive to tempt away; an old one on his last season is
    /// cheap. The one exception is the club a manager is running: its players are not settled by a
    /// roll, they go to an inbox to be accepted or refused by a person.
    /// </summary>
    public async Task<NpcTransferRoundResult> CalculateNpcTransfersAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var teams = (await _teams.ListAsync(cancellationToken)).ToList();
        var allPlayers = await _players.ListAsync(cancellationToken);
        var allStates = await _players.ListAllSeasonStatesAsync(seasonId, cancellationToken);
        var contracts = await _teams.ListAllContractsAsync(seasonId, cancellationToken);

        var playersById = allPlayers.ToDictionary(p => p.Id, p => p);
        var statesByPlayer = allStates
            .GroupBy(state => state.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        var teamsById = teams.ToDictionary(t => t.Id, t => t);

        // A player who has been under contract twice in one season appears twice — he was, and
        // the later membership is the club he finished the season at. The market reads one club
        // per player, and the one it reads is the last one he signed.
        var contractsByPlayer = new Dictionary<Guid, TeamMembership>();
        foreach (var contract in contracts.OrderBy(m => m.StartDate))
        {
            contractsByPlayer[contract.PlayerId] = contract;
        }

        // A club's book as it stands *at this moment*, not as it stood when the round began.
        //
        // The sizes are read once and then kept in step with the deals this run agrees: a
        // seller that has just let two men go is two men shorter, and a buyer that has just
        // signed one has one more. Reading the sizes once and never moving them is how a club
        // of twenty-three is sold three players in a single round on the strength of a
        // snapshot taken before the first of them left — the checks all pass, and the club
        // ends the round below the minimum with nobody able to field a side.
        var squadByTeam = contracts
            .GroupBy(m => m.TeamId)
            .ToDictionary(group => group.Key, group => group.Count());

        // The men who are already spoken for. A club cannot table a second offer for a player
        // who has a live deal on him, and the list of live deals is read once rather than asked
        // about player by player.
        var liveDeals = (await _transfers.ListLiveAsync(cancellationToken))
            .Select(deal => deal.PlayerId)
            .ToHashSet();

        var result = new NpcTransferRoundResult();
        var round = await CurrentChampionshipRoundAsync(season, cancellationToken);
        var window = await ReadTheWindowAsync(season, cancellationToken);
        var random = new Random(SeedFor(season, round));

        foreach (var buyer in teams.Where(team => !team.IsManagerClub))
        {
            var squad = squadByTeam.GetValueOrDefault(buyer.Id, 0);
            var clubPlayers = contractsByPlayer
                .Where(pair => pair.Value.TeamId == buyer.Id)
                .Select(pair => pair.Key)
                .ToHashSet();

            // Below the minimum the market's job is a team that can play, not a signing spree.
            if (squad < SquadSizeRules.MinSquadSize)
            {
                await SignFreeAgentsAsync(
                    buyer, season, window, squad, playersById, statesByPlayer, contractsByPlayer,
                    liveDeals, result, random, cancellationToken);

                continue;
            }

            if (!SquadSizeRules.CanAddOne(squad))
            {
                continue;
            }

            var targets = FindNpcTargets(
                buyer, clubPlayers, contracts, contractsByPlayer, playersById, squad,
                season.Number, random);

            var made = 0;
            var arrivalSeason = await _seasons.GetByNumberAsync(window.ArrivalSeasonNumber, cancellationToken);

            foreach (var target in targets)
            {
                if (made >= MaxProposalsPerClubPerRound)
                {
                    break;
                }

                // The room is asked again for every offer rather than once for the round: a club
                // that started the round at thirty-four and has since agreed one signing is at
                // thirty-five now, and a second offer would put it over a bound the completion
                // sweep would then refuse, leaving a deal accepted that can never happen.
                if (!SquadSizeRules.CanAddOne(squadByTeam.GetValueOrDefault(buyer.Id, 0)))
                {
                    break;
                }

                if (liveDeals.Contains(target.Player.Id))
                {
                    continue;
                }

                if (!contractsByPlayer.TryGetValue(target.Player.Id, out var contract)
                    || !statesByPlayer.TryGetValue(target.Player.Id, out var state))
                {
                    continue;
                }

                var seller = contract.TeamId;
                var sellerSquad = squadByTeam.GetValueOrDefault(seller, 0);

                if (!SquadSizeRules.CanRemoveOne(sellerSquad))
                {
                    continue;
                }

                var fee = PlayerValuation.AskingPrice(
                    PlayerValuation.MarketValue(target.Player, state),
                    contract.IsInHisLastSeason(season.Number));

                var proposal = Transfer.Propose(
                    target.Player.Id, seller, buyer.Id, season.Id,
                    window.ArrivalSeasonNumber, arrivalSeason?.Id,
                    fee, DateOnly.FromDateTime(DateTime.Now), window.ArrivalRoundNumber);

                if (teamsById.TryGetValue(seller, out var sellingClub) && sellingClub.IsManagerClub)
                {
                    // Somebody has to answer for this one, and it is not a roll of the dice. The
                    // offer sits in the manager's inbox until he accepts or refuses it. Nothing
                    // has left the seller's book yet — a deal nobody has answered is not a
                    // departure — so the sizes are left alone.
                    await _transfers.AddAsync(proposal, cancellationToken);

                    // And it is said out loud, rather than left on a screen the manager has to
                    // remember to open. An offer that is only ever visible where he chose to look
                    // is an offer that expires unanswered with nobody having decided anything: the
                    // pending row is the same either way, and the difference is whether the
                    // manager ever heard there was something to decide.
                    //
                    // The book value travels with it for the same reason the other path sends it:
                    // a manager comparing an offer to a price is comparing two numbers, and one
                    // of them is what the market thinks the man is worth rather than what this
                    // particular club happens to have offered.
                    await _inbox.PostTransferOfferAsync(
                        new TransferOfferFacts
                        {
                            RecipientTeamId = sellingClub.Id,
                            ClubName = sellingClub.Name,
                            PlayerId = target.Player.Id,
                            PlayerName = target.Player.Name,
                            BiddingClubId = buyer.Id,
                            BiddingClubName = buyer.Name,
                            Fee = fee,
                            AskingPrice = PlayerValuation.MarketValue(target.Player, state),
                            // No deadline, and the message says so: nothing here is going to
                            // expire this offer behind the manager's back, because a club that
                            // has not answered has not refused.
                            AnswerByRound = proposal.AnswerByRound,
                            Reference = $"offer:{proposal.Id}"
                        },
                        cancellationToken);

                    liveDeals.Add(target.Player.Id);
                    result.ProposalsMade++;
                    made++;
                    continue;
                }

                if (!await CanAffordAsync(buyer.Id, fee, cancellationToken))
                {
                    continue;
                }

                var roll = random.Next(1, 601);
                var threshold = AcceptanceThreshold(
                    target.Player.Age,
                    PlayerRating.CalculateStars(target.Player),
                    Math.Max(1, contract.SeasonsLeft(season.Number)));

                if (roll <= threshold)
                {
                    result.Rejected++;
                    continue;
                }

                proposal.Accept(DateOnly.FromDateTime(DateTime.Now));
                await _transfers.AddAsync(proposal, cancellationToken);
                await MoveTheMoneyAsync(proposal, season, cancellationToken);
                liveDeals.Add(target.Player.Id);
                MoveBetweenClubs(squadByTeam, seller, buyer.Id);
                result.ProposalsMade++;
                result.Accepted++;
                made++;
            }
        }

        // And the offers that were already on the table are answered, after the buying rather
        // than before it: a run that answered them first would be answering against squad sizes
        // this run has already changed, and a club that has just signed a man would be asked to
        // sell another on the strength of a book it no longer has.
        await AnswerTheOffersOnTheTableAsync(
            season, playersById, statesByPlayer, contractsByPlayer, teamsById,
            squadByTeam, random, result, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "NPC market at round {Round} of season {SeasonNumber}: {Proposals} proposed, " +
            "{Accepted} accepted, {Rejected} refused by the formula, {Signed} free agents signed, " +
            "{Answered} offers on the table answered.",
            round, season.Number, result.ProposalsMade, result.Accepted, result.Rejected,
            result.Signed, result.Answered);

        return result;
    }

    /// <summary>
    /// Answers the offers already on the table for a player one of these clubs holds — the ones
    /// a manager made.
    ///
    /// This is the other half of the rule above, and the half a market is dead without: the run
    /// makes a club's offers as well as settling them, because an offer that only one side of
    /// the world can answer is an offer that never gets answered. A manager who bids for a
    /// player and is never written back to is a manager whose bid is a rumour — and worse, a
    /// player nobody else may bid for until the season ends, because a live deal is what the
    /// market reads to refuse a second offer.
    ///
    /// The same formula answers them, because it is the same question: how expensive is this man
    /// to tempt away. The one exception is the club the manager is running — his players are not
    /// settled by a roll, they go to an inbox to be accepted or refused by a person, and a run
    /// that answered his own club's offers for him would be playing both sides of his table.
    /// </summary>
    private async Task<int> AnswerTheOffersOnTheTableAsync(
        Season season,
        IReadOnlyDictionary<Guid, Player> playersById,
        IReadOnlyDictionary<Guid, PlayerSeasonState> statesByPlayer,
        IReadOnlyDictionary<Guid, TeamMembership> contractsByPlayer,
        IReadOnlyDictionary<Guid, Team> teamsById,
        Dictionary<Guid, int> squadByTeam,
        Random random,
        NpcTransferRoundResult result,
        CancellationToken cancellationToken)
    {
        var pending = await _transfers.ListPendingAsync(season.Id, cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var answered = 0;

        foreach (var offer in pending)
        {
            // A signing has nobody to answer it — there is no selling club, and a deal waiting
            // on a club that does not exist is a deal nobody is ever going to answer.
            if (offer.SellingClubId is not { } sellerId)
            {
                continue;
            }

            // The manager's own club is answered by the manager, in his inbox.
            if (teamsById.TryGetValue(sellerId, out var sellingClub) && sellingClub.IsManagerClub)
            {
                continue;
            }

            // The seller has to still hold him: a deal agreed for a man who has since been
            // released or moved on is not a deal to settle, it is a row left behind. Each of
            // these says which one it was, because an offer quietly left pending is the state
            // this whole method exists to end.
            if (!playersById.TryGetValue(offer.PlayerId, out var player)
                || !contractsByPlayer.TryGetValue(offer.PlayerId, out var membership)
                || membership.TeamId != sellerId
                || !statesByPlayer.TryGetValue(offer.PlayerId, out var state))
            {
                _logger.LogWarning(
                    "Offer {TransferId} is not answered: {SellingClub} no longer holds the player, " +
                    "or he has no season state in season {SeasonNumber}.",
                    offer.Id, teamsById.GetValueOrDefault(sellerId)?.Name ?? sellerId.ToString(),
                    season.Number);
                continue;
            }

            // A club that cannot field a side does not sell, whatever it has been offered for it —
            // and the offer is refused rather than left standing. A deal the seller is not allowed
            // to keep is not a deal in progress: leaving it pending would go on saying the player
            // is spoken for, which is exactly what the market reads to refuse every other club's
            // offer, until the season closed and expired it a rumour nobody ever answered.
            if (!SquadSizeRules.CanRemoveOne(squadByTeam.GetValueOrDefault(sellerId, 0)))
            {
                offer.Reject(today);
                _transfers.Update(offer);
                answered++;
                result.Answered++;
                result.Rejected++;
                _logger.LogInformation(
                    "Offer {TransferId} refused: {SellingClub} is down to {Size} players and cannot " +
                    "sell another.",
                    offer.Id, teamsById.GetValueOrDefault(sellerId)?.Name ?? sellerId.ToString(),
                    squadByTeam.GetValueOrDefault(sellerId, 0));
                await AnswerTheManagerAsync(
                    offer, InboxDecision.RefusedOnTheSquad, playersById, teamsById, cancellationToken);
                continue;
            }

            // A price below the asking price is not an offer the club has to think about.
            //
            // The roll below answers how expensive the man is to tempt away — his age, his
            // quality, the seasons he still owes the club — and none of those three things are an
            // argument for selling him for less than the price on the card. A club that rolls the
            // dice on a bid it has already been offered half of is a club whose answer to "how
            // much" is "it depends on the day", and with the fee now paid at the acceptance a
            // manager would be buying at a price nobody chose. So the price is a floor and the
            // roll is what happens above it: under it, the offer is refused on the spot and said
            // to have been refused on the price, not on the toss.
            var askingPrice = PlayerValuation.AskingPrice(
                PlayerValuation.MarketValue(player, state),
                membership.IsInHisLastSeason(season.Number));

            if (offer.Fee < askingPrice)
            {
                offer.Reject(today);
                _transfers.Update(offer);
                answered++;
                result.Answered++;
                result.Rejected++;
                _logger.LogInformation(
                    "Offer {TransferId} for {PlayerName} refused by {SellingClub}: {Fee} is under the " +
                    "asking price of {Asking}.",
                    offer.Id, player.Name, sellingClub?.Name ?? sellerId.ToString(), offer.Fee, askingPrice);
                await AnswerTheManagerAsync(
                    offer, InboxDecision.RefusedOnPrice, playersById, teamsById, cancellationToken);
                continue;
            }

            var roll = random.Next(1, 601);
            var threshold = AcceptanceThreshold(
                player.Age,
                PlayerRating.CalculateStars(player),
                Math.Max(1, membership.SeasonsLeft(season.Number)));

            if (roll <= threshold)
            {
                offer.Reject(today);
                _transfers.Update(offer);
                answered++;
                result.Answered++;
                result.Rejected++;
                _logger.LogInformation(
                    "Offer {TransferId} for {PlayerName} refused by {SellingClub} (roll {Roll} against {Threshold:F0}).",
                    offer.Id, player.Name, sellingClub?.Name ?? sellerId.ToString(), roll, threshold);
                await AnswerTheManagerAsync(
                    offer, InboxDecision.RefusedOnThePlayer, playersById, teamsById, cancellationToken);
                continue;
            }

            offer.Accept(today);
            _transfers.Update(offer);
            await MoveTheMoneyAsync(offer, season, cancellationToken);
            MoveBetweenClubs(squadByTeam, sellerId, offer.BuyingClubId);

            answered++;
            result.Answered++;
            result.Accepted++;
            _logger.LogInformation(
                "Offer {TransferId} for {PlayerName} accepted by {SellingClub} for {Fee} limos.",
                offer.Id, player.Name, sellingClub?.Name ?? sellerId.ToString(), offer.Fee);
            await AnswerTheManagerAsync(
                offer, InboxDecision.Accepted, playersById, teamsById, cancellationToken);
        }

        return answered;
    }

    /// <summary>
    /// Writes the answer to the manager's own bid, and returns without writing when the bid is
    /// not his.
    ///
    /// This is the only writer of a decision, and the reason it takes the maps the run already
    /// holds is that the run answers the whole world in one walk: by the time a proposal has
    /// been settled, asking the database for the three names it would print is a question about
    /// a row the walk is already carrying in its hand.
    ///
    /// A proposal the manager made for his own player is skipped by the walk above — his
    /// players are answered by a person, not by a roll — so every proposal that reaches here
    /// with him as the buyer is a bid of his own going unanswered no longer.
    /// </summary>
    private async Task AnswerTheManagerAsync(
        Transfer offer,
        InboxDecision outcome,
        IReadOnlyDictionary<Guid, Player> playersById,
        IReadOnlyDictionary<Guid, Team> teamsById,
        CancellationToken cancellationToken)
    {
        if (offer.SellingClubId is not { } sellerId
            || !teamsById.TryGetValue(offer.BuyingClubId, out var buyer)
            || !buyer.IsManagerClub
            || !teamsById.TryGetValue(sellerId, out var seller)
            || !playersById.TryGetValue(offer.PlayerId, out var player))
        {
            return;
        }

        await _inbox.PostTransferDecisionAsync(
            new TransferDecisionFacts
            {
                RecipientTeamId = buyer.Id,
                ClubName = buyer.Name,
                PlayerId = player.Id,
                PlayerName = player.Name,
                OtherClubId = seller.Id,
                OtherClubName = seller.Name,
                Fee = offer.Fee,
                Outcome = outcome,
                ManagerIsSeller = false,
                Reference = $"transfer:{offer.Id}"
            },
            cancellationToken);
    }

    /// <summary>
    /// Moves a man's name from one club's book to the other's, in the sizes the run is reading.
    /// The counts are the run's own arithmetic; the rows are written when the deals complete, at
    /// the window, against a size read again from the database.
    /// </summary>
    private static void MoveBetweenClubs(Dictionary<Guid, int> squadByTeam, Guid from, Guid to)
    {
        squadByTeam[from] = squadByTeam.GetValueOrDefault(from, 0) - 1;
        squadByTeam[to] = squadByTeam.GetValueOrDefault(to, 0) + 1;
    }

    /// <summary>
    /// The number a selling club's acceptance is measured against: how expensive the player is
    /// to tempt away. Younger is dearer, better is dearer, and a longer contract is dearer still,
    /// because every one of those seasons is a promise the club made to the man rather than to
    /// whoever is buying him.
    /// </summary>
    public static double AcceptanceThreshold(int age, double stars, int seasonsRemaining) =>
        Math.Max(0d, (50 - age) * stars * seasonsRemaining);

    /// <summary>
    /// A club short of men signs whoever is on the market. A free agent is not a gift: the
    /// signing fee is a share of what he is worth, so the two ends of the market are still
    /// sorted by money — a club that cannot pay for a man does not get him, however few players
    /// it has. The men are taken from the nobody-else-is-after list first: a squad of
    /// thirty-five does not need help and a club under twenty-one cannot play a match, so the
    /// two ends are sorted before anybody competes for the middle.
    /// </summary>
    private async Task<int> SignFreeAgentsAsync(
        Team buyer,
        Season season,
        WindowState window,
        int squad,
        IReadOnlyDictionary<Guid, Player> playersById,
        IReadOnlyDictionary<Guid, PlayerSeasonState> statesByPlayer,
        IReadOnlyDictionary<Guid, TeamMembership> contractsByPlayer,
        HashSet<Guid> liveDeals,
        NpcTransferRoundResult result,
        Random random,
        CancellationToken cancellationToken)
    {
        var arrivalSeason = await _seasons.GetByNumberAsync(window.ArrivalSeasonNumber, cancellationToken);

        var wanted = new List<Player>();
        var emergency = new List<Player>();

        foreach (var player in playersById.Values)
        {
            if (!statesByPlayer.TryGetValue(player.Id, out var state)
                || state.TeamId is not null
                || contractsByPlayer.ContainsKey(player.Id)
                || liveDeals.Contains(player.Id))
            {
                continue;
            }

            // A club that cannot field a side takes the first men to arrive; a club merely short
            // of comfort waits for somebody better, and picks the better of the two lists first.
            if (player.Age <= SquadSizeRules.MinSquadSize / 2 - 4)
            {
                emergency.Add(player);
            }
            else
            {
                wanted.Add(player);
            }
        }

        var free = emergency
            .Concat(wanted)
            .OrderBy(player => random.Next())
            .ToList();

        var signed = 0;

        foreach (var player in free)
        {
            if (signed >= MaxSigningsPerClubPerRound || !SquadSizeRules.CanAddOne(squad + signed))
            {
                break;
            }

            // A free agent is not free: the signing fee is a share of what the man is worth, and
            // it is charged when the signing is agreed, exactly as a purchase is. A club that
            // cannot pay it does not sign him — the fee leaves the buyer's book the moment the
            // deal is struck, so a club that could not afford the man has to be asked the
            // question here rather than discovered at the window with a player it has not paid
            // for and cannot have.
            var signingFee = PlayerValuation.FreeAgentSigningFee(
                PlayerValuation.MarketValue(player, statesByPlayer[player.Id]));

            if (!await CanAffordAsync(buyer.Id, signingFee, cancellationToken))
            {
                continue;
            }

            var deal = Transfer.Propose(
                player.Id, sellingClubId: null, buyer.Id, season.Id,
                window.ArrivalSeasonNumber, arrivalSeason?.Id,
                signingFee, DateOnly.FromDateTime(DateTime.Now), window.ArrivalRoundNumber);

            deal.Accept(DateOnly.FromDateTime(DateTime.Now));
            await _transfers.AddAsync(deal, cancellationToken);
            await MoveTheMoneyAsync(deal, season, cancellationToken);

            liveDeals.Add(player.Id);
            result.Signed++;
            signed++;
        }

        return signed;
    }

    /// <summary>
    /// Answers every proposal whose selling club has no manager and whose deadline has passed,
    /// by letting it expire. A club without a manager is not a club that forgets — it is a club
    /// that has to be told when to decide — and this is the sweep that tells it: a bid left on
    /// its desk past the round it was given expires, the player is free again, and the market
    /// keeps moving. It is safe to call twice, and a proposal that has already expired is not
    /// expired again.
    /// </summary>
    public async Task<int> ExpirePendingProposalsAsync(
        int currentRoundNumber,
        CancellationToken cancellationToken = default)
    {
        var expired = await _transfers.ListExpiredAsync(currentRoundNumber, cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var transfer in expired)
        {
            transfer.Expire(today);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await ReportTheDecisionsAsync(expired, _ => InboxDecision.Expired, cancellationToken);

        _logger.LogInformation(
            "Expired {Count} proposals whose selling club had no manager and whose deadline passed at round {Round}.",
            expired.Count, currentRoundNumber);

        return expired.Count;
    }

    /// <summary>
    /// Writes the answer to every proposal in a batch that the manager's club is a side of.
    ///
    /// The sweeps expire the whole world and this writes one club's share of it, so the two
    /// are kept apart on purpose: a proposal between two computer-controlled clubs is settled
    /// for the ledger and for nobody's reading, and a proposal the manager is a side of is
    /// settled for him too. The names are read once for the whole batch rather than per
    /// proposal, which is the difference between three queries and three times the batch.
    /// </summary>
    private async Task ReportTheDecisionsAsync(
        IReadOnlyList<Transfer> transfers,
        Func<Transfer, InboxDecision> outcomeOf,
        CancellationToken cancellationToken)
    {
        if (transfers.Count == 0)
        {
            return;
        }

        var manager = await _teams.GetManagerClubAsync(cancellationToken);
        if (manager is null)
        {
            return;
        }

        var involved = transfers
            .Where(transfer => transfer.BuyingClubId == manager.Id || transfer.SellingClubId == manager.Id)
            .ToList();

        if (involved.Count == 0)
        {
            return;
        }

        var clubs = (await _teams.ListByIdsAsync(
                involved
                    .SelectMany(transfer => new[] { transfer.BuyingClubId, transfer.SellingClubId })
                    .Where(id => id is not null)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList(),
                cancellationToken))
            .ToDictionary(club => club.Id);

        var players = await _teams.GetPlayersAsync(
            involved.Select(transfer => transfer.PlayerId).Distinct().ToList(),
            cancellationToken);

        foreach (var transfer in involved)
        {
            var isSeller = transfer.SellingClubId == manager.Id;
            var otherId = isSeller ? transfer.BuyingClubId : transfer.SellingClubId;

            if (otherId is null
                || !clubs.TryGetValue(otherId.Value, out var other)
                || !players.TryGetValue(transfer.PlayerId, out var player))
            {
                continue;
            }

            await _inbox.PostTransferDecisionAsync(
                new TransferDecisionFacts
                {
                    RecipientTeamId = manager.Id,
                    ClubName = manager.Name,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    OtherClubId = other.Id,
                    OtherClubName = other.Name,
                    Fee = transfer.Fee,
                    Outcome = outcomeOf(transfer),
                    ManagerIsSeller = isSeller,
                    Reference = $"transfer:{transfer.Id}"
                },
                cancellationToken);
        }
    }

    /// <summary>
    /// The men a club goes after, best score first, scored against what the club already has.
    ///
    /// The sample is drawn from every other club's squad and then judged by need: a club thin at
    /// centre back wants a centre back, and a club that already has four good ones is not
    /// interested in a fifth however famous he is. The score is not the price — a club does not
    /// buy the best man available, it buys the one it is missing.
    /// </summary>
    private static IReadOnlyList<NpcTarget> FindNpcTargets(
        Team buyer,
        HashSet<Guid> clubPlayers,
        IReadOnlyList<TeamMembership> contracts,
        IReadOnlyDictionary<Guid, TeamMembership> contractsByPlayer,
        IReadOnlyDictionary<Guid, Player> playersById,
        int squad,
        int seasonNumber,
        Random random)
    {
        var pool = contracts
            .Where(membership => membership.TeamId != buyer.Id
                                && !clubPlayers.Contains(membership.PlayerId))
            .OrderBy(_ => random.Next())
            .Take(40)
            .ToList();

        var counted = new Dictionary<Position, int>();
        foreach (var membership in contracts.Where(m => m.TeamId == buyer.Id))
        {
            if (playersById.TryGetValue(membership.PlayerId, out var player))
            {
                counted[player.Position] = counted.GetValueOrDefault(player.Position) + 1;
            }
        }

        var targets = new List<NpcTarget>();

        foreach (var membership in pool)
        {
            if (!playersById.TryGetValue(membership.PlayerId, out var player))
            {
                continue;
            }

            var stars = PlayerRating.CalculateStars(player);
            var age = player.Age;
            var seasonsLeft = membership.SeasonsLeft(seasonNumber);
            var need = Math.Max(0, SquadSizeRules.MinSquadSize - squad);
            var shortfall = Math.Max(0, 4 - counted.GetValueOrDefault(player.Position));

            // A man in a position the club is short of, who is good for his age, who is either
            // free to move or nearly so, and who fills a squad that is on the small side.
            var score = (stars * 8) - age
                        + (membership.IsInHisLastSeason(seasonNumber) ? 4 : 0)
                        + (shortfall * 3)
                        + (need > 0 ? 6 : 0)
                        + (seasonsLeft <= 1 ? 3 : 0);

            targets.Add(new NpcTarget(player, membership.TeamId, score));
        }

        return targets
            .OrderByDescending(target => target.Score)
            .ThenBy(target => MarketOrder.KeyOf(target.Player.Id))
            .ToList();
    }

    /// <summary>
    /// A club's business is settled the same way every time it is asked, and the same way for
    /// every club: the season and the round are the seed, so a matchday that is swept twice — a
    /// process that was down over the weekend — reaches the same offers rather than a second set.
    /// </summary>
    private static int SeedFor(Season season, int round) =>
        unchecked(season.Number * 7919 + round * 104729 + season.Id.GetHashCode());

    /// <summary>
    /// A deadline is a fact about a deal, and a deal that replays the same has to answer by the
    /// same round. The seed is the player and the season, so a proposal made twice — by a
    /// manager and by a sweep that redrew it — is given the same deadline either time.
    /// </summary>
    private static int SeedFor(Guid playerId, Guid seasonId) =>
        unchecked(playerId.GetHashCode() ^ seasonId.GetHashCode());

    #endregion

    #region Searching

    /// <summary>
    /// Searches the market, narrowed by whatever the manager asked for, and paginated.
    ///
    /// The answer is the whole list narrowed, not a different list per checkbox: a market that
    /// loads a different set of men for each filter is a market that disagrees with itself. The
    /// order is the fixed one from <see cref="MarketOrder"/> rather than the alphabet or the
    /// value, so a manager paging through men neither reads the same striker first every time
    /// nor finds page two empty of what page one was showing.
    /// </summary>
    public async Task<TransferSearchResult> SearchTransfersAsync(
        Guid seasonId,
        TransferSearchFilters filters,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        filters ??= new TransferSearchFilters();

        var season = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var allPlayers = await _players.ListAsync(cancellationToken);
        var allStates = await _players.ListAllSeasonStatesAsync(seasonId, cancellationToken);
        var contracts = await _teams.ListAllContractsAsync(seasonId, cancellationToken);
        var teams = (await _teams.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var careers = (await _players.ListCareerTotalsAsync(cancellationToken))
            .ToDictionary(c => c.PlayerId, c => c.Line);

        var statesByPlayer = allStates
            .GroupBy(state => state.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());

        // The men somebody has already agreed to buy. A player with an accepted deal on him is
        // spoken for, and the market refuses a second offer on him — so the row has to say so
        // rather than letting a manager find out by being refused.
        var spokenFor = (await _transfers.ListAcceptedAllAsync(cancellationToken))
            .Select(deal => deal.PlayerId)
            .ToHashSet();

        // The men who have a pending proposal on them. A proposal waiting for an answer is a
        // bid, and a bid is a thing two clubs can make for the same man — so the row shows a
        // mark but does not block the offer button.
        var pendingOn = (await _transfers.ListLiveAsync(cancellationToken))
            .Where(deal => deal.Status == TransferStatus.Pending)
            .Select(deal => deal.PlayerId)
            .ToHashSet();

        var contractsByPlayer = new Dictionary<Guid, TeamMembership>();
        foreach (var contract in contracts.OrderBy(m => m.StartDate))
        {
            contractsByPlayer[contract.PlayerId] = contract;
        }

        var listings = new List<TransferListing>();

        foreach (var player in allPlayers)
        {
            if (!filters.Matches(player))
            {
                continue;
            }

            var state = statesByPlayer.GetValueOrDefault(player.Id);

            if (!filters.MatchesTheState(state))
            {
                continue;
            }

            var listing = BuildListing(
                player,
                state,
                contractsByPlayer.GetValueOrDefault(player.Id),
                teams,
                season,
                careers.GetValueOrDefault(player.Id) ?? new PlayerCareerLine(),
                hasActiveProposal: spokenFor.Contains(player.Id) || pendingOn.Contains(player.Id),
                hasAcceptedProposal: spokenFor.Contains(player.Id));

            listings.Add(listing);
        }

        var ordered = listings
            .OrderBy(listing => MarketOrder.KeyOf(listing.PlayerId))
            .ToList();

        var total = ordered.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));
        var actualPage = Math.Clamp(page, 1, totalPages);

        var window = await ReadTheWindowAsync(season, cancellationToken);

        return new TransferSearchResult
        {
            Players = ordered.Skip((actualPage - 1) * pageSize).Take(pageSize).ToList(),
            Total = total,
            Page = actualPage,
            PageSize = pageSize,
            TotalPages = totalPages,
            Window = DescribeTheWindow(season.Number, window)
        };
    }

    /// <summary>
    /// The market view of one player, for the card that opens from the search results: the same
    /// row, plus his career split by club, which is the part a club reads before it bids.
    /// </summary>
    public async Task<TransferListing> GetPlayerListingAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var state = await _players.GetSeasonStateAsync(playerId, seasonId, cancellationToken);
        var teams = (await _teams.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var contracts = await _teams.ListAllContractsAsync(seasonId, cancellationToken);

        var contract = contracts
            .Where(m => m.PlayerId == playerId)
            .OrderByDescending(m => m.StartDate)
            .FirstOrDefault();

        var careers = (await _players.ListCareerTotalsAsync(cancellationToken))
            .ToDictionary(c => c.PlayerId, c => c.Line);

        var spokenFor = (await _transfers.ListLiveAsync(cancellationToken))
            .Select(deal => deal.PlayerId)
            .ToHashSet();

        var listing = BuildListing(
            player, state, contract, teams, season,
            careers.GetValueOrDefault(playerId) ?? new PlayerCareerLine(),
            spokenFor.Contains(playerId));

        var clubLines = await _players.ListClubCareerLinesAsync(playerId, cancellationToken);

        listing.Clubs = clubLines
            .Select(line => new PlayerClubCareerLine
            {
                TeamId = line.TeamId,
                TeamName = teams.GetValueOrDefault(line.TeamId)?.Name ?? string.Empty,
                Seasons = line.Seasons,
                Total = line.Total
            })
            .OrderByDescending(line => line.Total.Goals)
            .ThenByDescending(line => line.Seasons)
            .ToList();

        return listing;
    }

    /// <summary>
    /// The inbox of a club: proposals made to it and proposals it has made, for the season given.
    /// </summary>
    public async Task<TransferInbox> GetInboxAsync(
        Guid clubId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var club = await _teams.GetAsync(clubId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", clubId);

        var teams = (await _teams.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var players = (await _players.ListAsync(cancellationToken)).ToDictionary(p => p.Id, p => p);

        var incoming = await _transfers.ListIncomingAsync(clubId, seasonId, cancellationToken);
        var outgoing = await _transfers.ListOutgoingAsync(clubId, seasonId, cancellationToken);

        TransferProposal Build(Transfer transfer) => BuildProposal(
            transfer,
            players.GetValueOrDefault(transfer.PlayerId),
            transfer.SellingClubId is { } seller ? teams.GetValueOrDefault(seller) : null,
            teams.GetValueOrDefault(transfer.BuyingClubId),
            season.Number);

return new TransferInbox
        {
            ClubId = clubId,
            ClubName = club.Name,
            ProposalSeasonNumber = season.Number,
            Incoming = incoming.Select(Build).ToList(),
            Outgoing = outgoing.Select(Build).ToList()
        };
    }

    /// <summary>
    /// The transfers that finished in the last three rounds, across every club of the division
    /// the manager's own club plays in. A market that shows what happened recently is a market
    /// a manager can read without opening a second screen, and the three rounds are counted
    /// from the round being played rather than from a date, because that is the only thing a
    /// round is.
    /// </summary>
    public async Task<DivisionRecentTransfers> GetDivisionRecentTransfersAsync(
        Guid clubId,
        int windowRounds = 3,
        CancellationToken cancellationToken = default)
    {
        var season = await GetCurrentSeasonAsync(cancellationToken);

        // The division the manager's own club is in this season. A club has no division of its
        // own — it is enrolled in one for the season, and a club that changes tier is the same
        // club in another edition — so the question is asked of the enrolment, and the recent
        // business of that division is the business of every club in it.
        var division = await _competitions.GetDivisionSeasonForTeamAsync(
            clubId, season.Id, cancellationToken);

        if (division is null)
        {
            return new DivisionRecentTransfers
            {
                CompetitionSeasonId = Guid.Empty,
                CurrentRound = 0,
                WindowRounds = windowRounds,
                Transfers = Array.Empty<TransferHistoryLine>()
            };
        }

        var currentRound = await CurrentChampionshipRoundAsync(season, cancellationToken);
        var transfers = await _transfers.ListRecentCompletedAsync(
            division.Id, currentRound, windowRounds, cancellationToken);

        var teams = (await _teams.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var seasonNumbers = (await _seasons.ListAsync(cancellationToken)).ToDictionary(s => s.Id, s => s.Number);

        return new DivisionRecentTransfers
        {
            CompetitionSeasonId = division.Id,
            CurrentRound = currentRound,
            WindowRounds = windowRounds,
            Transfers = transfers.Select(t => BuildHistoryLine(t, teams, seasonNumbers)).ToList()
        };
    }

    /// <summary>
    /// Every live transfer involving one club, across the seasons given, newest first. Pending
    /// and accepted sit in the same table as completed ones, because a proposal is a fact about
    /// the club's season whether or not the selling club has answered it yet — and a table
    /// that only showed finished deals would read as a club that did nothing. Rejected and
    /// expired are the two that never move a player, and they are left out: a bid that was
    /// turned down is not a season of the club's business.
    /// </summary>
    public async Task<ClubTransferHistory> GetClubTransferHistoryAsync(
        Guid teamId,
        IEnumerable<int> seasonNumbers,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", teamId);

        var transfers = await _transfers.ListByClubAsync(teamId, seasonNumbers, cancellationToken);

        var teams = (await _teams.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var seasonNumbersById = (await _seasons.ListAsync(cancellationToken)).ToDictionary(s => s.Id, s => s.Number);

        return new ClubTransferHistory
        {
            TeamId = teamId,
            TeamName = team.Name,
            SeasonNumbers = seasonNumbers.ToHashSet().ToList(),
            Transfers = transfers.Select(t => BuildHistoryLine(t, teams, seasonNumbersById)).ToList()
        };
    }

    private static TransferHistoryLine BuildHistoryLine(
        Transfer transfer,
        Dictionary<Guid, Team> teams,
        Dictionary<Guid, int> seasonNumbers) =>
        new()
        {
            TransferId = transfer.Id,
            PlayerId = transfer.PlayerId,
            PlayerName = transfer.Player?.Name ?? transfer.PlayerId.ToString(),
            PlayerPosition = transfer.Player?.Position.ToString() ?? string.Empty,
            SellingClubId = transfer.SellingClubId,
            SellingClubName = transfer.SellingClubId is { } seller
                ? transfer.SellingClub?.Name ?? teams.GetValueOrDefault(seller)?.Name ?? string.Empty
                : null,
            BuyingClubId = transfer.BuyingClubId,
            BuyingClubName = transfer.BuyingClub?.Name
                ?? teams.GetValueOrDefault(transfer.BuyingClubId)?.Name ?? string.Empty,
            ProposalSeasonNumber = seasonNumbers.GetValueOrDefault(transfer.ProposalSeasonId),
            ArrivalSeasonNumber = transfer.ArrivalSeasonNumber,
            ArrivalRoundNumber = transfer.ArrivalRoundNumber,
            Fee = transfer.Fee,
            Status = transfer.Status.ToString(),
            ProposedAt = transfer.ProposedAt,
            ResolvedAt = transfer.ResolvedAt,
            CompletedAt = transfer.CompletedAt
        };

    /// <summary>
    /// Every transfer a player has been involved in, newest first — the list of clubs he has
    /// worn a shirt for, and what each move was worth.
    /// </summary>
    public async Task<IReadOnlyList<PlayerTransferHistoryLine>> GetPlayerHistoryAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var transfers = await _transfers.ListByPlayerAsync(playerId, cancellationToken);
        if (transfers.Count == 0)
        {
            return Array.Empty<PlayerTransferHistoryLine>();
        }

        var allSeasons = await _seasons.ListAsync(cancellationToken);
        var teams = (await _teams.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t);
        var seasonNumbers = allSeasons.ToDictionary(s => s.Id, s => s.Number);

        return transfers
            .Select(transfer => new PlayerTransferHistoryLine
            {
                PlayerId = transfer.PlayerId,
                SellingClubId = transfer.SellingClubId,
                SellingClubName = transfer.SellingClubId is { } seller
                    ? teams.GetValueOrDefault(seller)?.Name ?? string.Empty
                    : "Sem clube",
                BuyingClubId = transfer.BuyingClubId,
                BuyingClubName = teams.GetValueOrDefault(transfer.BuyingClubId)?.Name ?? string.Empty,
                Fee = transfer.Fee,
                Status = transfer.Status,
                ProposedAt = transfer.ProposedAt,
                ResolvedAt = transfer.ResolvedAt,
                CompletedAt = transfer.CompletedAt,
                ProposalSeasonNumber = seasonNumbers.GetValueOrDefault(transfer.ProposalSeasonId),
                ArrivalSeasonNumber = transfer.ArrivalSeasonNumber
            })
            .ToList();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// How many players a club has under contract right now. A membership with no end date is a
    /// deal that has not been called off, and that is the whole test of a squad's size.
    /// </summary>
    private async Task<int> SquadSizeAsync(Guid teamId, CancellationToken ct) =>
        (await _teams.GetLiveContractsAsync(teamId, ct)).Count;

    /// <summary>
    /// Whether a club's book can carry a fee. A club that cannot pay for a man does not get him:
    /// the deal is never agreed rather than putting the club in debt it never agreed to, and the
    /// player stays where he is instead of walking into a bankruptcy. It is asked by the clubs
    /// that shop — the market checks it before it tables an offer, and it checks it before it
    /// signs a free agent — and not asked again at the window, because a deal that was paid for
    /// when it was agreed has already been paid for.
    /// </summary>
    private async Task<bool> CanAffordAsync(Guid teamId, decimal fee, CancellationToken ct)
    {
        if (fee <= 0m)
        {
            return true;
        }

        var last = await _finance.GetLastAsync(teamId, ct);
        return (last?.BalanceAfter ?? 0m) >= fee;
    }

    private async Task<TeamMembership?> ResolveContractAsync(
        Guid playerId, Guid teamId, CancellationToken ct) =>
        (await _teams.GetLiveContractsAsync(teamId, ct))
            .FirstOrDefault(m => m.PlayerId == playerId);

    private async Task<MatchDay?> CurrentMatchDayAsync(Guid seasonId, CancellationToken ct)
    {
        var matchDays = await _matches.GetMatchDaysAsync(seasonId, ct);
        return matchDays
            .Where(md => md.Date <= DateOnly.FromDateTime(DateTime.Now))
            .OrderByDescending(md => md.Number)
            .FirstOrDefault();
    }

    /// <summary>
    /// The round the world is in: the one after the last that was played.
    /// </summary>
    private async Task<int> CurrentChampionshipRoundAsync(Season season, CancellationToken ct)
    {
        var played = CompetitionRules.LeagueMatchDays -
            await _seasons.GetChampionshipRoundsLeftAsync(season.Id, ct);
        return Math.Min(CompetitionRules.LeagueMatchDays, played + 1);
    }

    /// <summary>
    /// The window the world is in: the round being played, whether the mid-season window is open,
    /// and where a proposal made right now would put its man. A proposal is never refused for
    /// arriving outside a window — it waits — so the window's job is to name the arrival, not to
    /// say no.
    /// </summary>
    private async Task<WindowState> ReadTheWindowAsync(Season season, CancellationToken ct)
    {
        var currentRound = await CurrentChampionshipRoundAsync(season, ct);

        return new WindowState(
            currentRound,
            TransferWindowRules.IsOpen(currentRound),
            TransferWindowRules.ArrivalSeasonNumberFor(currentRound, season.Number),
            TransferWindowRules.ArrivalRoundFor(currentRound));
    }

    /// <summary>
    /// The window as a manager reads it: which round the world is in, whether a man bought now
    /// would move this season, and the round he would move on. The screen shows the sentence and
    /// does not work the arithmetic out, for the same reason every other number on a screen is
    /// read rather than computed.
    /// </summary>
    private static TransferWindowState DescribeTheWindow(int seasonNumber, WindowState window) =>
        new TransferWindowState
        {
            SeasonNumber = seasonNumber,
            CurrentRound = window.CurrentRound,
            IsOpen = window.IsOpen,
            ArrivalSeasonNumber = window.ArrivalSeasonNumber,
            ArrivalRoundNumber = window.ArrivalRoundNumber,
            ArrivalLabel = window.ArrivalRoundNumber == TransferWindowRules.FirstArrivalRound
                ? $"após a {TransferWindowRules.FirstArrivalRound}ª rodada desta temporada"
                : "após a Supercopa, na próxima temporada"
        };

    private static TransferProposal BuildProposal(
        Transfer transfer,
        Player? player,
        Team? sellingClub,
        Team? buyingClub,
        int proposalSeasonNumber)
    {
        return new TransferProposal
        {
            TransferId = transfer.Id,
            PlayerId = transfer.PlayerId,
            PlayerName = player?.Name ?? string.Empty,
            PlayerPosition = player?.Position.ToString() ?? string.Empty,
            PlayerAge = player?.Age ?? 0,
            SellingClubId = transfer.SellingClubId,
            SellingClubName = sellingClub?.Name ?? "Sem clube",
            BuyingClubId = transfer.BuyingClubId,
            BuyingClubName = buyingClub?.Name ?? string.Empty,
            ProposalSeasonNumber = proposalSeasonNumber,
            ArrivalSeasonNumber = transfer.ArrivalSeasonNumber,
            ArrivalRoundNumber = transfer.ArrivalRoundNumber,
            Fee = transfer.Fee,
            Status = transfer.Status.ToString(),
            ProposedAt = transfer.ProposedAt,
            ResolvedAt = transfer.ResolvedAt,
            CompletedAt = transfer.CompletedAt
        };
    }

    /// <summary>
    /// The market view of one player: who he is, what he is worth, what he did this season, and
    /// what he has done in a career that is every club's by name.
    /// </summary>
    private static TransferListing BuildListing(
        Player player,
        PlayerSeasonState? state,
        TeamMembership? contract,
        IReadOnlyDictionary<Guid, Team> teams,
        Season season,
        PlayerCareerLine career,
        bool hasActiveProposal = false,
        bool hasAcceptedProposal = false)
    {
        var teamId = state?.TeamId;
        var team = teamId.HasValue && teams.TryGetValue(teamId.Value, out var found) ? found : null;

        // A free agent is worth something and is paid something: the absence of a club is a fact
        // about his contract, not about his legs, and a market that showed a released striker at
        // no price at all would be pricing the release and not the man.
        decimal? marketValue = null;
        decimal? salary = null;
        decimal? askingPrice = null;

        if (state is not null)
        {
            marketValue = PlayerValuation.MarketValue(player, state);
            salary = PlayerValuation.SeasonWage(player, state);

            // A free agent's price is the signing fee, and it is shown like any other price: a
            // club that takes a man off the market pays a share of what he is worth, and a
            // manager who cannot see the number is a manager agreeing to a cost he was not told.
            askingPrice = teamId.HasValue
                ? PlayerValuation.AskingPrice(
                    marketValue.Value, contract is not null && contract.IsInHisLastSeason(season.Number))
                : PlayerValuation.FreeAgentSigningFee(marketValue.Value);
        }

        return new TransferListing
        {
            PlayerId = player.Id,
            Name = player.Name,
            Position = player.Position.ToString(),
            Age = player.Age,
            Speed = player.Speed,
            Accuracy = player.Accuracy,
            Dribbling = player.Dribbling,
            Heading = player.Heading,
            Strength = player.Strength,
            GoalkeeperPower = player.GoalkeeperPower,
            Reflexes = player.Reflexes,
            Stamina = player.Stamina,
            Potential = player.Potential,
            Stars = PlayerRating.CalculateStars(player),
            TeamId = teamId,
            TeamName = team?.Name,
            TeamPrimaryColor = team?.PrimaryColor,
            TeamSecondaryColor = team?.SecondaryColor,
            Energy = state?.Energy ?? 100,
            Injury = state?.Injury.ToString() ?? "None",
            InjuryMatchesRemaining = state?.InjuryMatchesRemaining ?? 0,
            MarketValue = marketValue,
            Salary = salary,
            ContractSeasons = contract?.ContractSeasons ?? 0,
            SeasonsLeft = contract is not null ? contract.SeasonsLeft(season.Number) : 0,
            IsInLastSeason = contract is not null && contract.IsInHisLastSeason(season.Number),
            IsFreeAgent = teamId is null,
            HasActiveProposal = hasActiveProposal,
            HasAcceptedProposal = hasAcceptedProposal,
            AskingPrice = askingPrice,
            Retiring = state?.Retiring ?? false,
            Season = new PlayerCareerLine
            {
                Appearances = state is null ? 0 : 1,
                Goals = state?.Goals ?? 0,
                Saves = state?.Saves ?? 0,
                YellowCards = state?.YellowCards ?? 0,
                RedCards = state?.RedCards ?? 0
            },
            Total = career
        };
    }

    /// <summary>
    /// A man a club has an eye on, and how badly it wants him.
    /// </summary>
    /// <summary>
    /// Which division of a season each of its clubs is in, counted from one at the top.
    /// </summary>
    /// <remarks>
    /// A club is in a division for a season through the edition it is enrolled in rather than
    /// by being one, which is what lets a club be relegated and still be the same club next
    /// season. So the map is a walk of the season's four divisions and their enrolments — one
    /// read for the whole window, because a window moves dozens of men and the division a man
    /// crossed is a fact about the window and not about the man.
    /// </remarks>
    private async Task<Dictionary<Guid, int>> ReadTheDivisionsOfTheSeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var divisions = new Dictionary<Guid, int>();

        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);
        foreach (var view in views.Where(view => view.Type == CompetitionType.League && view.Tier is not null))
        {
            var participants = await _competitions.ListParticipantsAsync(view.Id, cancellationToken);

            foreach (var participant in participants)
            {
                divisions[participant.TeamId] = view.Tier!.Value;
            }
        }

        return divisions;
    }

    /// <summary>
    /// Tells a manager that one of his men signed a club in his own division.
    ///
    /// <para>
    /// Only the same division, and that is the whole of what makes the message worth opening.
    /// The manager did not sell and is not owed a bid — the bid was somebody else's, and it
    /// was not addressed to him. He is owed to know that the forward he has been planning
    /// around is now scoring against him in the table above his own, and a club two divisions
    /// away is arithmetic the box need not carry.
    /// </para>
    ///
    /// <para>
    /// It is asked after the save, so a message is never written about a move that was then
    /// rolled back, and it is keyed on the deal, so a window closed twice tells the same
    /// departure once.
    /// </para>
    /// </summary>
    private async Task ReportTheDeparturesAsync(
        IReadOnlyList<CompletedDeparture> departures,
        IReadOnlySet<Guid> managers,
        Season arrivalSeason,
        CancellationToken cancellationToken)
    {
        // A world of nobody, or a window that moved nobody out of a club, is the whole of the
        // reason not to read a division: the report is owed to a person, and a world with no
        // person in it is a world where the read has no answer to give.
        if (departures.Count == 0 || managers.Count == 0)
        {
            return;
        }

        var divisionByClub = await ReadTheDivisionsOfTheSeasonAsync(arrivalSeason.Id, cancellationToken);

        var clubNames = (await _teams.ListByIdsAsync(
            departures
                .SelectMany(departure => new[] { departure.SellingClubId, (Guid?)departure.BuyingClubId })
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .Concat(managers)
                .Distinct(),
            cancellationToken))
            .ToDictionary(club => club.Id, club => club.Name);

        var told = 0;

        foreach (var departure in departures)
        {
            if (departure.SellingClubId is not { } sellerId || !managers.Contains(sellerId))
            {
                continue;
            }

            // Both clubs have to be in the season's table for "the same division" to be a
            // question with an answer, and a club with no division is a club the pyramid has
            // not said anything about this season.
            if (!divisionByClub.TryGetValue(sellerId, out var tier) ||
                !divisionByClub.TryGetValue(departure.BuyingClubId, out var buyerTier) ||
                buyerTier != tier)
            {
                continue;
            }

            if (departure.Player is not { } player)
            {
                continue;
            }

            await _inbox.PostPlayerDepartureAsync(
                new PlayerDepartureFacts
                {
                    RecipientTeamId = sellerId,
                    ClubName = clubNames.GetValueOrDefault(sellerId, sellerId.ToString()),
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    BuyingTeamId = departure.BuyingClubId,
                    BuyingClubName = clubNames.GetValueOrDefault(departure.BuyingClubId),
                    DivisionTier = tier,
                    Fee = departure.Transfer.Fee,
                    TransferId = departure.Transfer.Id
                },
                cancellationToken);

            told++;
        }

        if (told > 0)
        {
            _logger.LogInformation(
                "Told {Count} manager(s) about a signing in their own division at the start of {Season}.",
                told,
                arrivalSeason.Name);
        }
    }

    /// <summary>
    /// A man a club has an eye on, and how badly it wants him.
    /// </summary>
    private readonly record struct NpcTarget(Player Player, Guid SellingClubId, double Score);

    /// <summary>
    /// Where the world is in its transfer calendar.
    /// </summary>
    private readonly record struct WindowState(
        int CurrentRound,
        bool IsOpen,
        int ArrivalSeasonNumber,
        int ArrivalRoundNumber);

    /// <summary>
    /// One man who arrived somewhere in this window, kept until the window can say which
    /// division he crossed.
    /// </summary>
    /// <remarks>
    /// The seller is a field and not a property of the deal because a signing has nobody to
    /// sell him to: the same window writes a free agent onto a club's books and a transfer out
    /// of another, and only one of the two is a departure anybody is owed news about. The
    /// player comes along because the loop already read him — he is the man the wage on the
    /// new contract was worked out from — and a report that looked him up again would be a
    /// second read of a man the window has already in its hand.
    /// </remarks>
    private readonly record struct CompletedDeparture(
        Transfer Transfer,
        Guid? SellingClubId,
        Guid BuyingClubId,
        Player? Player);

    #endregion
}
