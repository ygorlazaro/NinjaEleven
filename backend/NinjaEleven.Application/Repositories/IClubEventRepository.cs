using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// The moments of a club's life that had to be written down at the time.
/// </summary>
/// <remarks>
/// Only the moments with a writer live in here. Everything else a club's page shows —
/// a title, a promotion, a season's top scorer — is a fact the competition tables already
/// hold, and copying those into a second table would be a second opinion about them rather
/// than a record of them.
/// </remarks>
public interface IClubEventRepository
{
    /// <summary>
    /// A club's recorded moments, newest first.
    /// </summary>
    Task<IReadOnlyList<ClubEvent>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a moment. Not saved: the moment and whatever it was done to are one fact, and
    /// the one unit of work that writes the club is the one that must write both.
    /// </summary>
    Task AddAsync(ClubEvent clubEvent, CancellationToken cancellationToken = default);
}