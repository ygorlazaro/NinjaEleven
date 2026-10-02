using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence contract for transfer proposals. It only stores and reads them:
/// what a fee is worth, who may accept and when the player moves — those are decisions
/// of the domain and they belong to <see cref="Services.TransferService"/>.
/// </summary>
public interface ITransferRepository
{
    Task<Transfer?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every proposal addressed to one club in the season given, newest first. An inbox is a
    /// season's business: a deal made last season was last season's answer.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListIncomingAsync(
        Guid clubId,
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every proposal one club has made in the season given, newest first.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListOutgoingAsync(
        Guid clubId,
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every proposal involving one player, across every season, newest first.
    /// A player's history is the list of clubs he has worn a shirt for, and this is
    /// the list that says so.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListByPlayerAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every proposal still waiting on a selling club in the season given, oldest first.
    /// A club's inbox is this list and nothing else.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListPendingAsync(
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every accepted proposal due to arrive in the season of the number given, oldest first.
    /// These are the deals waiting for a window, and this is the list a round-end sweep works
    /// through. It is asked for by season <em>number</em> because a deal names the season it
    /// waits for before that season exists, and the sweep knows only the number.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListAcceptedAsync(
        int arrivalSeasonNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every proposal naming a season the world has not opened yet, so that opening the season
    /// can point each of them at the row they are waiting for.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListWaitingForSeasonAsync(
        int arrivalSeasonNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every deal in the world that is still live — pending or accepted. The market reads this to
    /// know which men are already spoken for, rather than asking about each one, and it is not
    /// scoped to a season: a deal agreed last season and still waiting for the Supercup has the
    /// same claim on its player as one made this morning.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListLiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every accepted deal in the world, across every season. The market reads this to know
    /// which men are spoken for — a player with an accepted deal on him may not be offered to,
    /// because a promise is a promise — while a proposal that is still waiting for the selling
    /// club to answer is a bid, and a bid is a thing two clubs can make for the same man.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListAcceptedAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether an accepted proposal involves this player, so a second offer is refused rather
    /// than piling up. Only an accepted deal blocks a new one: a proposal that is still
    /// waiting for the selling club to answer is a bid, and a bid is a thing two clubs can
    /// make for the same man. The moment it is accepted it stops being a bid and becomes a
    /// promise, and a man with a promise on him is spoken for.
    ///
    /// Not scoped to a season: a man with an accepted deal is spoken for, and a deal stops
    /// being accepted only when it is completed, called off or the player leaves anyway.
    /// </summary>
    Task<bool> ExistsAcceptedAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every pending proposal whose selling club has no manager and whose deadline has passed,
    /// oldest first. A club without a manager is not a club that forgets — it is a club that
    /// has to be told when to decide — and this is the sweep that tells it: a bid left on its
    /// desk past the round it was given expires, the player is free again, and the market
    /// keeps moving.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListExpiredAsync(
        int currentRoundNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every transfer that finished in the last three rounds, across every club of the
    /// division given, newest first. A market that shows what happened recently is a market
    /// a manager can read without opening a second screen, and the three rounds are counted
    /// from the round being played rather than from a date, because that is the only thing
    /// a round is.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListRecentCompletedAsync(
        Guid competitionSeasonId,
        int currentRoundNumber,
        int windowRounds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every transfer involving one club, across the seasons given, newest first, with the
    /// season each one was made in. A club's transfer history is the list of men who came
    /// and men who went, and the season is what separates this season's business from last
    /// season's when the two sit in one table.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListByClubAsync(
        Guid teamId,
        IEnumerable<int> seasonNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every completed transfer for clubs in a division, read once for the rankings. The
    /// rankings are a reading of the same set as the recent-transfers panel, and asking it
    /// once for the whole division keeps a page of four tables from asking sixteen clubs'
    /// history one at a time.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListCompletedByDivisionAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The offers a club has on the table for a player it is selling, tracked so they can be
    /// withdrawn. Tracked rather than untracked because a withdrawal is a change: a club that
    /// lets a man go has nothing left to sell, and every offer naming him as a player it holds
    /// is a promise the club can no longer keep.
    /// </summary>
    Task<IReadOnlyList<Transfer>> ListPendingOffersForSellerAsync(
        Guid sellingClubId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Transfer transfer, CancellationToken cancellationToken = default);
    void Update(Transfer transfer);

    /// <summary>
    /// Takes a deal out of the hands of anybody else who is completing this same window,
    /// and says whether this caller is the one that got it.
    /// </summary>
    /// <para>
    /// The claim is a conditional update rather than a read followed by a write, because a
    /// read followed by a write is a race with a name: two callers — the matchday sweep and
    /// a hand on <c>POST /transfer/complete</c> — both read the same accepted row, both
    /// believe they own it, and both sign the player. The membership is written before the
    /// status is, so the second one lands on top of the first and the club ends up holding
    /// the same man twice with one deal row saying he arrived once.
    /// </para>
    /// <para>
    /// One row, updated by its own key and its own status, is the whole claim. The second
    /// caller's update blocks on the first's row lock and then re-reads the row, finds it
    /// no longer accepted, and matches nothing — so it is told no rather than being trusted
    /// to have checked. It runs inside the same unit of work as the membership it guards, so
    /// the claim and the signing commit together or not at all: a deal claimed and then lost
    /// is a deal whose player never arrived and whose status says he did.
    /// </para>
    /// <param name="transferId">The deal to take.</param>
    /// <returns>True when this caller took the deal and is the one to complete it.</returns>
    Task<bool> TryClaimForCompletionAsync(
        Guid transferId,
        CancellationToken cancellationToken = default);
}
