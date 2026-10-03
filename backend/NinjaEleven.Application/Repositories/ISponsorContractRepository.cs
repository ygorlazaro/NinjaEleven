using NinjaEleven.Domain.Sponsors;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// Persistence for shirt deals between sponsors and clubs.
/// </summary>
public interface ISponsorContractRepository
{
    Task<SponsorContract?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The active deal a club currently has, if it has one.</summary>
    Task<SponsorContract?> GetActiveByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>All contracts a club has ever held, newest first.</summary>
    Task<IReadOnlyList<SponsorContract>> ListByTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every deal that is live right now, for a whole set of sponsors at once, keyed by the
    /// sponsor holding it.
    ///
    /// <para>
    /// Which companies are willing to put their name on a club, and for how much, depends on
    /// how many clubs each of them already sponsors and on which divisions they have taken.
    /// That is one question about thirty companies, and asking it one company at a time is how
    /// a screen that offers three contracts ends up reading thirty tables.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<SponsorContract>>> ListActiveBySponsorIdsAsync(
        IEnumerable<Guid> sponsorIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live deal of every club in the set that has one, keyed by the club holding it.
    ///
    /// <para>
    /// It is asked at the kick-off, which is the one moment that has to know about both shirts
    /// at once, and it is asked about a set rather than about one club because a match has two
    /// clubs in it. A kick-off that read the home deal and then the away deal is two round trips
    /// to stamp two columns of the same row.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SponsorContract>> ListActiveByTeamIdsAsync(
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(SponsorContract contract, CancellationToken cancellationToken = default);
    void Update(SponsorContract contract);
}
