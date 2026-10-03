using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Repositories;

/// <summary>
/// A club's crowd, as a series rather than a number.
/// <para>
/// The reads are set reads. A match needs both clubs' crowds and the close of a season needs all
/// sixty-four of them, and neither question is answered by asking per club: the crowd of the
/// home team and the crowd of the away team are two rows of one season, and a service that asked
/// for them one at a time would read the same season twice for every fixture of the matchday.
/// </para>
/// </summary>
public interface ITeamFanBaseRepository
{
    /// <summary>
    /// The latest crowd each of these clubs has, which is what a match is measured against.
    ///
    /// <para>
    /// "Latest" rather than "this season's" on purpose: a match on the first matchday of a
    /// season is played by clubs whose crowd was written at the end of the one before it, and a
    /// crowd of nothing is not an answer a ground can be filled with. The world has been drawn
    /// before a crowd has been seeded for a club, and the two are seeded by different code, so
    /// this asks for what is there.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> LatestSupportersForTeamsAsync(
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default);

    /// <summary>Every club's crowd in one season, which is what a season close writes and a table reads.</summary>
    Task<IReadOnlyList<TeamFanBase>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>Every club's crowd in one season, keyed by club.</summary>
    Task<IReadOnlyDictionary<Guid, TeamFanBase>> ListBySeasonIndexAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>Which of these clubs already have a row for this season.</summary>
    Task<HashSet<Guid>> ListTeamsWithARowAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task<TeamFanBase?> GetAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TeamFanBase> fanBases, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a row the caller read untracked onto the instance this context already holds.
    /// <para>
    /// Same reason as <see cref="IMatchRepository.Update"/>: the season close reads a season's
    /// crowds with one set read, and a second save that touched one of those rows would leave the
    /// context holding two instances of it — which EF refuses, from inside a close that was
    /// halfway through paying out.
    /// </para>
    /// </summary>
    void Update(TeamFanBase fanBase);
}