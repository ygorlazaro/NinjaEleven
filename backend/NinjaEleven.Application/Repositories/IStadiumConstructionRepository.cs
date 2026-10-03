using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// A club's ground work, as a series rather than a row.
///
/// <para>
/// The reads are set reads. A matchday asks whether any of the eight grounds it is closing the
/// works on are still building, and a season close settles every open project in the country,
/// and neither question is answered by asking a club at a time.
/// </para>
/// </summary>
public interface IStadiumConstructionRepository
{
    /// <summary>Every project still to be finished, in every season.</summary>
    /// <summary>
    /// Every project started in a season, finished or not, keyed by nothing and grouped by the
    /// caller.
    /// </summary>
    /// <remarks>
    /// The NPC pass asks one question of all sixty-four clubs — has this club already approved
    /// something today — and asking it club by club is the N+1 this codebase keeps paying and
    /// stopping: the table is small, and one read answers it whole. Finished projects are wanted
    /// here precisely because the question is about a round that has already passed: a club
    /// whose stand finished on this very round must not be handed the next one out of the same
    /// balance.
    /// </remarks>
    Task<IReadOnlyList<StadiumConstruction>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StadiumConstruction>> ListUnfinishedAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The projects still building on these grounds, which is what a crowd is measured against.
    /// </summary>
    /// <remarks>
    /// Finished projects are left out rather than filtered by a date, because a ground that is
    /// not being worked on is the ordinary answer and saying so should not cost a read of the
    /// whole table.
    /// </remarks>
    Task<IReadOnlyList<StadiumConstruction>> ListUnfinishedForStadiumsAsync(
        IEnumerable<Guid> stadiumIds,
        CancellationToken cancellationToken = default);

    /// <summary>The open project on these grounds, or null where there is none.</summary>
    Task<StadiumConstruction?> FindOpenForStadiumAsync(
        Guid stadiumId,
        CancellationToken cancellationToken = default);

    Task<StadiumConstruction?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(StadiumConstruction construction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a row the caller read untracked onto the instance this context already holds.
    /// <para>
    /// The settlement reads the open projects untracked, because it reads them all at once, and
    /// then writes them back one at a time. A tracked attach would leave the context holding two
    /// instances of the same project and EF would refuse the second — from inside a settlement
    /// that was halfway through adding seats.
    /// </para>
    /// </summary>
    void Update(StadiumConstruction construction);
}