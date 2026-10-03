using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// A club's ground.
///
/// <para>
/// The reads are set reads and they are tracked where they are written. A settlement that has
/// just worked out which grounds finished their projects this matchday then needs those grounds
/// itself, and asking for sixty-four of them one at a time to add seats to two would be a
/// question asked five thousand times to answer a hundred.
/// </para>
/// </summary>
public interface IStadiumRepository
{
    /// <summary>
    /// These grounds, read tracked so a caller may change them and save.
    ///
    /// <para>
    /// Tracked on purpose and only here. Every other reader in this codebase reads untracked,
    /// because every other reader is reading; a reader that hands back a tracked entity nobody
    /// saves is an attach that costs a second copy of a row somebody else already holds.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Stadium>> ListForUpdateAsync(
        IEnumerable<Guid> stadiumIds,
        CancellationToken cancellationToken = default);

    /// <summary>Every ground in the world, in one read.</summary>
    Task<IReadOnlyList<Stadium>> ListAsync(CancellationToken cancellationToken = default);
}