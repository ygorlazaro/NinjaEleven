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
    /// Whether a proposal involving this player is still pending or accepted, so a second offer
    /// for the same man is refused rather than piling up. Not scoped to a season: a man with an
    /// offer on the table is spoken for, and a deal stops being live only when it is rejected,
    /// completed or expired.
    /// </summary>
    Task<bool> ExistsActiveAsync(
        Guid playerId,
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
}
