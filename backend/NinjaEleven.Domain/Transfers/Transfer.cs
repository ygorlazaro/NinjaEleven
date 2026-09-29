using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Domain.Transfers;

/// <summary>
/// A proposal to move a player from one club to another — or, when he has no club, to give him
/// one — and the money that goes with it.
///
/// A transfer is a promise about the future rather than a fact about today, so it carries two
/// seasons: the one it was made in and the one the player is due to arrive in. They are not
/// always consecutive, because the mid-season window is an arrival in the season a deal was
/// agreed in, and keeping them apart is what lets a proposal made in July sit beside one made
/// in December without the second pretending the first never happened.
///
/// The arrival season is stored as a <em>number</em> and only linked to a season once that
/// season exists. A world that is still playing its first season has no second one to point at,
/// and a proposal that insisted on the row before it would refuse every deal a manager could
/// possibly make. The number is what a deal is about; the row is bookkeeping.
///
/// A proposal with no selling club is a signing. A man with no club has nobody to ask, so the
/// fee is zero, and the money that moves is only out of the buyer: there is no second club's
/// book to credit, because there is no second club.
/// </summary>
public class Transfer
{
    public Guid Id { get; private set; }
    public Guid PlayerId { get; private set; }

    /// <summary>
    /// The club selling the player, or null when he is a free agent and there is nobody to
    /// sell him to. A signing is a transfer whose seller is nobody.
    /// </summary>
    public Guid? SellingClubId { get; private set; }

    public Guid BuyingClubId { get; private set; }

    /// <summary>The season the proposal was made in.</summary>
    public Guid ProposalSeasonId { get; private set; }

    /// <summary>
    /// The season the player is due to arrive in, counted from one. It is known the moment the
    /// proposal is made — the rules say which window is still ahead of it — and it does not
    /// depend on a season row existing yet.
    /// </summary>
    public int ArrivalSeasonNumber { get; private set; }

    /// <summary>
    /// The season row the player arrives in, once that season exists. Null while the arrival is
    /// still in the future and the world has not opened that season yet.
    /// </summary>
    public Guid? ArrivalSeasonId { get; private set; }

    /// <summary>
    /// The price the buying club agreed or was asked to pay, in limos. Zero for a signing: a
    /// free agent is not bought, he is picked up.
    /// </summary>
    public decimal Fee { get; private set; }

    /// <summary>Where the proposal is in its life cycle.</summary>
    public TransferStatus Status { get; private set; }

    /// <summary>When the proposal was made.</summary>
    public DateOnly ProposedAt { get; private set; }

    /// <summary>When the selling club answered, when it did.</summary>
    public DateOnly? ResolvedAt { get; private set; }

    /// <summary>When the player arrived at his new club, when the deal completed.</summary>
    public DateOnly? CompletedAt { get; private set; }

    /// <summary>
    /// The round of the arrival season in which the player is due to arrive. It is a number and
    /// not the identifier of a round row because it is read before that row is known: it is the
    /// name of the moment the deal is waiting for, and the calendar confirms it later. Null
    /// until the window that carries it comes round.
    /// </summary>
    public int? ArrivalRoundNumber { get; private set; }

    /// <summary>
    /// The championship round the world was playing when the proposal was made. It is the only
    /// way to ask how long a proposal has been waiting without reading a date, and it is the
    /// thing a deadline is measured against.
    /// </summary>
    public int? ProposalRoundNumber { get; private set; }

    /// <summary>
    /// The round by which the selling club must answer this proposal, or null when the club has
    /// a person behind it and answers on its own schedule. A club without a manager is not a
    /// club that forgets: it is a club that has to be told when to decide, and a proposal left
    /// on its desk forever is a market that stops moving.
    /// </summary>
    public int? AnswerByRound { get; private set; }

    /// <summary>The player this proposal is about, loaded when the deal is read with its names.</summary>
    public Player? Player { get; set; }

    /// <summary>The club buying the player, loaded when the deal is read with its names.</summary>
    public Team? BuyingClub { get; set; }

    /// <summary>The club selling the player, loaded when the deal is read with its names.</summary>
    public Team? SellingClub { get; set; }

    private Transfer() { }

    public static Transfer Propose(
        Guid playerId,
        Guid? sellingClubId,
        Guid buyingClubId,
        Guid proposalSeasonId,
        int arrivalSeasonNumber,
        Guid? arrivalSeasonId,
        decimal fee,
        DateOnly proposedAt,
        int? arrivalRoundNumber = null,
        int? proposalRoundNumber = null,
        int? answerByRound = null)
    {
        if (fee < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(fee), fee, "A transfer fee is not a negative number.");
        }

        // A signing carries a fee and no seller, and the two are not in conflict: the fee is what
        // the club pays to take a man nobody is selling (see PlayerValuation.FreeAgentSigningFee),
        // and there is nobody to pay it to. It is the money that moves — out of the buyer, into
        // nobody's account — and not a price a seller refused, so what the check above refuses is
        // a negative amount and nothing else.
        if (sellingClubId == buyingClubId)
        {
            throw new ArgumentException(
                "Um clube não pode propor a compra de seu próprio jogador.", nameof(buyingClubId));
        }

        if (arrivalSeasonNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arrivalSeasonNumber),
                arrivalSeasonNumber,
                "A transfer chega numa temporada, e a primeira temporada é a número um.");
        }

        if (arrivalRoundNumber.HasValue && arrivalRoundNumber.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arrivalRoundNumber), arrivalRoundNumber, "A round is counted from one.");
        }

        if (proposalRoundNumber.HasValue && proposalRoundNumber.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(proposalRoundNumber), proposalRoundNumber, "A round is counted from one.");
        }

        if (answerByRound.HasValue && answerByRound.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answerByRound), answerByRound, "A round is counted from one.");
        }

        return new Transfer
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            SellingClubId = sellingClubId,
            BuyingClubId = buyingClubId,
            ProposalSeasonId = proposalSeasonId,
            ArrivalSeasonNumber = arrivalSeasonNumber,
            ArrivalSeasonId = arrivalSeasonId,
            Fee = fee,
            Status = TransferStatus.Pending,
            ProposedAt = proposedAt,
            ArrivalRoundNumber = arrivalRoundNumber,
            ProposalRoundNumber = proposalRoundNumber,
            AnswerByRound = answerByRound
        };
    }

    /// <summary>
    /// Links the deal to the season it arrives in, once the world has opened that season. A
    /// deal proposed in a first season that is still waiting for the Supercup of the second is
    /// given its row here, and nothing else about it changes.
    /// </summary>
    public void LinkArrivalSeason(Guid arrivalSeasonId)
    {
        ArrivalSeasonId = arrivalSeasonId;
    }

    /// <summary>
    /// Names the round the player walks in on. It is written when the window that carries the
    /// deal comes round, because before then the round is a number the rules chose and not one
    /// the calendar has reached.
    /// </summary>
    public void SetArrivalRound(int roundNumber)
    {
        if (roundNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber), roundNumber, "A round is counted from one.");
        }

        ArrivalRoundNumber = roundNumber;
    }

    /// <summary>
    /// Names the round by which the selling club must answer, and the round the proposal was
    /// made in. A club without a manager is not a club that forgets — it is a club that has to
    /// be told when to decide — so the deadline is written when the proposal is made and it is
    /// the sweep that honours it.
    /// </summary>
    public void SetDecisionDeadline(int proposalRoundNumber, int answerByRound)
    {
        if (proposalRoundNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(proposalRoundNumber), proposalRoundNumber, "A round is counted from one.");
        }

        if (answerByRound < proposalRoundNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answerByRound),
                answerByRound,
                "A deadline is not before the round the proposal was made in.");
        }

        ProposalRoundNumber = proposalRoundNumber;
        AnswerByRound = answerByRound;
    }

    /// <summary>The selling club accepted the proposal.</summary>
    public void Accept(DateOnly resolvedAt)
    {
        if (Status != TransferStatus.Pending)
        {
            throw new InvalidOperationException(
                "A transfer that is no longer pending cannot be accepted.");
        }

        Status = TransferStatus.Accepted;
        ResolvedAt = resolvedAt;
    }

    /// <summary>The selling club refused the proposal.</summary>
    public void Reject(DateOnly resolvedAt)
    {
        if (Status != TransferStatus.Pending)
        {
            throw new InvalidOperationException(
                "A transfer that is no longer pending cannot be rejected.");
        }

        Status = TransferStatus.Rejected;
        ResolvedAt = resolvedAt;
    }

    /// <summary>
    /// The deal was agreed and can no longer happen, so it is called off.
    ///
    /// Refusing and calling off are two different things, and the domain keeps them apart
    /// because they are told apart: a club refuses an offer it is reading, and the window calls
    /// off a deal that was signed by both sides and has since stopped being possible — the
    /// seller let the player go, or cannot pay, or is not allowed to sell him. Using the one
    /// word for both would either let a signed deal be refused by whoever got to it first, or
    /// make the window throw on the very deals it exists to settle, which is a matchday that
    /// stops halfway through its own transfers.
    /// </summary>
    public void CallOff(DateOnly calledOffAt)
    {
        if (Status != TransferStatus.Accepted)
        {
            throw new InvalidOperationException(
                "Only a deal that was agreed can be called off.");
        }

        Status = TransferStatus.Rejected;
        ResolvedAt = calledOffAt;
    }

    /// <summary>
    /// The proposal was never answered and the window for answering it has passed. A club
    /// without a manager answers on a deadline rather than on its own schedule, and this is
    /// the moment that deadline is honoured: a proposal left on an NPC club's desk past the
    /// round it was given expires, so the player is free again and the market keeps moving.
    /// </summary>
    public void Expire(DateOnly expiredAt)
    {
        if (Status != TransferStatus.Pending)
        {
            throw new InvalidOperationException(
                "A transfer that is no longer pending cannot expire.");
        }

        Status = TransferStatus.Expired;
        ResolvedAt = expiredAt;
    }

    /// <summary>Whether the round given is past the round by which this club must answer.</summary>
    public bool IsPastDeadline(int currentRoundNumber) =>
        AnswerByRound.HasValue && currentRoundNumber > AnswerByRound.Value;

    /// <summary>
    /// The player has arrived at his new club and the deal is complete. The money has moved
    /// and the two clubs' books carry their own lines of it.
    /// </summary>
    public void Complete(DateOnly completedAt)
    {
        if (Status != TransferStatus.Accepted)
        {
            throw new InvalidOperationException(
                "Only an accepted transfer can complete.");
        }

        Status = TransferStatus.Completed;
        CompletedAt = completedAt;
    }
}
