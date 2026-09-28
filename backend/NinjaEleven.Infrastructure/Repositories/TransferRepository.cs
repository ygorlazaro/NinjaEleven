using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Transfers;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly NinjaElevenDbContext _context;

    public TransferRepository(NinjaElevenDbContext context)
    {
        _context = context;
    }

    public async Task<Transfer?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transfer>> ListIncomingAsync(
        Guid clubId,
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.SellingClubId == clubId && t.ProposalSeasonId == proposalSeasonId)
            .OrderByDescending(t => t.ProposedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transfer>> ListOutgoingAsync(
        Guid clubId,
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.BuyingClubId == clubId && t.ProposalSeasonId == proposalSeasonId)
            .OrderByDescending(t => t.ProposedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transfer>> ListByPlayerAsync(
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.PlayerId == playerId)
            .OrderByDescending(t => t.ProposedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transfer>> ListPendingAsync(
        Guid proposalSeasonId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.ProposalSeasonId == proposalSeasonId && t.Status == TransferStatus.Pending)
            .OrderBy(t => t.ProposedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The accepted deals due to arrive in a season, asked for by the season's number.
    ///
    /// The number and not the season's identifier is the whole point: a deal agreed in a season
    /// that is still being played names the season it waits for before that season exists, and
    /// the window that completes the deal is asked for the same number. Filtering this on the
    /// season the proposal was made in instead — which is what the query used to do — reads a
    /// table that can never answer: every deal would be waiting for a season one further away
    /// than it is, and no player would ever arrive anywhere.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListAcceptedAsync(
        int arrivalSeasonNumber,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.ArrivalSeasonNumber == arrivalSeasonNumber
                        && t.Status == TransferStatus.Accepted)
            .OrderBy(t => t.ProposedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every deal naming a season the world has not opened yet, so a season opening can point
    /// each of them at the row they are waiting for.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListWaitingForSeasonAsync(
        int arrivalSeasonNumber,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.ArrivalSeasonNumber == arrivalSeasonNumber && t.ArrivalSeasonId == null)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Whether the player is the subject of a deal that is still live.
    ///
    /// Live means pending or accepted and nothing else, and it is not scoped to a season on
    /// purpose. A man with an offer on the table is spoken for, whatever season the offer was
    /// made in: a buyer waiting for the next window is a buyer the club is already counting on,
    /// and letting a rival make a second offer on the same player is how two clubs end up
    /// waiting for a man neither of them has. A deal stops being live when it is rejected,
    /// completed or expired, and a season close expires whatever is still unanswered.
    /// </summary>
    /// <summary>
    /// Every deal in the world that is still live: pending an answer or accepted and waiting for
    /// its window. The market reads this once to know which men are spoken for, and it is not
    /// scoped to a season — a deal agreed last season and still waiting for the Supercup has the
    /// same claim on its player as one made this morning.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListLiveAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.Status == TransferStatus.Pending || t.Status == TransferStatus.Accepted)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsActiveAsync(
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .AnyAsync(
                t => t.PlayerId == playerId
                     && (t.Status == TransferStatus.Pending || t.Status == TransferStatus.Accepted),
                cancellationToken);

    public async Task<IReadOnlyList<Transfer>> ListPendingOffersForSellerAsync(
        Guid sellingClubId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .Where(t => t.SellingClubId == sellingClubId
                        && t.PlayerId == playerId
                        && t.Status == TransferStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Transfer transfer, CancellationToken cancellationToken = default) =>
        await _context.Transfers.AddAsync(transfer, cancellationToken);

    public void Update(Transfer transfer) => _context.Transfers.Update(transfer);
}
