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
            .Include(t => t.Player)
            .Include(t => t.BuyingClub)
            .Include(t => t.SellingClub)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every accepted deal in the world, across every season. The market reads this to know
    /// which men are spoken for — a player with an accepted deal on him may not be offered to,
    /// because a promise is a promise — while a proposal that is still waiting for the selling
    /// club to answer is a bid, and a bid is a thing two clubs can make for the same man.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListAcceptedAllAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.Status == TransferStatus.Accepted)
            .Include(t => t.Player)
            .Include(t => t.BuyingClub)
            .Include(t => t.SellingClub)
            .ToListAsync(cancellationToken);


    public async Task<bool> ExistsAcceptedAsync(
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .AnyAsync(
                t => t.PlayerId == playerId && t.Status == TransferStatus.Accepted,
                cancellationToken);

    /// <summary>
    /// Every pending proposal whose selling club has no manager and whose deadline has passed,
    /// oldest first. A club without a manager is not a club that forgets — it is a club that
    /// has to be told when to decide — and this is the sweep that tells it: a bid left on its
    /// desk past the round it was given expires, the player is free again, and the market
    /// keeps moving.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListExpiredAsync(
        int currentRoundNumber,
        CancellationToken cancellationToken = default) =>
        await _context.Transfers
            .AsNoTracking()
            .Where(t => t.Status == TransferStatus.Pending
                        && t.AnswerByRound.HasValue
                        && currentRoundNumber > t.AnswerByRound.Value)
            .OrderBy(t => t.ProposedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every transfer that finished in the last three rounds, across every club of the
    /// division given, newest first. A market that shows what happened recently is a market
    /// a manager can read without opening a second screen, and the three rounds are counted
    /// from the round being played rather than from a date, because that is the only thing
    /// a round is.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListRecentCompletedAsync(
        Guid competitionSeasonId,
        int currentRoundNumber,
        int windowRounds,
        CancellationToken cancellationToken = default)
    {
        var clubsInDivision = await _context.CompetitionParticipants
            .AsNoTracking()
            .Where(p => p.CompetitionSeasonId == competitionSeasonId)
            .Select(p => p.TeamId)
            .ToHashSetAsync(cancellationToken);

        if (clubsInDivision.Count == 0)
        {
            return Array.Empty<Transfer>();
        }

        var fromRound = Math.Max(1, currentRoundNumber - windowRounds + 1);

        return await _context.Transfers
            .AsNoTracking()
            .Where(t => t.Status == TransferStatus.Completed
                        && t.ArrivalRoundNumber >= fromRound
                        && t.ArrivalRoundNumber <= currentRoundNumber
                        && (clubsInDivision.Contains(t.BuyingClubId)
                            || (t.SellingClubId.HasValue && clubsInDivision.Contains(t.SellingClubId.Value))))
            .Include(t => t.Player)
            .Include(t => t.BuyingClub)
            .Include(t => t.SellingClub)
            .OrderByDescending(t => t.CompletedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every live transfer involving one club, across the seasons given, newest first, with
    /// the season each one was made in. A club's transfer history is the list of men who came
    /// and men who went, and the season is what separates this season's business from last
    /// season's when the two sit in one table.
    ///
    /// A bid that was refused or ran out of time is not on it. Rejected and expired are the
    /// two ways a proposal ends without a player ever moving, and a club page that listed
    /// them would show a season of business in which the same player left three times and
    /// arrived never — the two dead letters are noise on a page about who came and who went.
    /// </summary>
    public async Task<IReadOnlyList<Transfer>> ListByClubAsync(
        Guid teamId,
        IEnumerable<int> seasonNumbers,
        CancellationToken cancellationToken = default)
    {
        var seasonList = seasonNumbers.ToHashSet();

        var liveStatuses = new[]
        {
            TransferStatus.Pending,
            TransferStatus.Accepted,
            TransferStatus.Completed
        };

        return await _context.Transfers
            .AsNoTracking()
            .Where(t => (t.BuyingClubId == teamId
                         || (t.SellingClubId.HasValue && t.SellingClubId.Value == teamId))
                     && seasonList.Contains(t.ArrivalSeasonNumber)
                     && liveStatuses.Contains(t.Status))
            .Include(t => t.Player)
            .Include(t => t.BuyingClub)
            .Include(t => t.SellingClub)
            .OrderByDescending(t => t.CompletedAt ?? t.ProposedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

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

    /// <summary>
    /// Moves one deal from accepted to completed, and says whether this caller is the one that
    /// moved it.
    /// </summary>
    /// <remarks>
    /// The predicate is the claim. A caller whose update matches no row is a caller that lost
    /// the race to a window being completed twice — by the matchday sweep and by a hand on the
    /// route at the same moment — and it is told so here rather than discovering it afterwards
    /// in a squad that holds one man three times. No <c>FOR UPDATE</c> is needed, and none is
    /// wanted: the row lock PostgreSQL takes for the update is held until the unit of work
    /// commits, which is exactly as long as the membership this guards needs it.
    /// </remarks>
    public async Task<bool> TryClaimForCompletionAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var claimed = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE transfers
                SET status = {TransferStatus.Completed.ToString()},
                    completed_at = {DateOnly.FromDateTime(DateTime.Now)}
              WHERE id = {transferId}
                AND status = {TransferStatus.Accepted.ToString()}
             """,
            cancellationToken);

        return claimed == 1;
    }
}
