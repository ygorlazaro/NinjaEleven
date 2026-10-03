using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class TeamFanBaseRepository : ITeamFanBaseRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public TeamFanBaseRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// One row per club: the most recent season each of them has a crowd for.
    /// </summary>
    /// <remarks>
    /// The "latest per club" is a join to the seasons rather than a read of the newest season
    /// in the world, and it orders by the season's <em>number</em> rather than by its id. Two
    /// reasons, and both of them are the same one: a guid has no calendar in it, so ordering
    /// ids would pick a season at random; and the newest season in the world is not necessarily
    /// one every club has a row in — a world that has just opened a season has rows for the
    /// season before it and none for this one, so asking for the newest season would hand back
    /// nothing at all for sixty-four clubs on the first day of football.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, int>> LatestSupportersForTeamsAsync(
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        var ids = teamIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await (
            from fanBase in _dbContext.TeamFanBases.AsNoTracking()
            join season in _dbContext.Seasons.AsNoTracking() on fanBase.SeasonId equals season.Id
            where ids.Contains(fanBase.TeamId)
            orderby fanBase.TeamId, season.Number descending
            select new { fanBase.TeamId, fanBase.Supporters })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.TeamId)
            .ToDictionary(group => group.Key, group => group.First().Supporters);
    }

    public async Task<IReadOnlyList<TeamFanBase>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamFanBases
            .AsNoTracking()
            .Where(fanBase => fanBase.SeasonId == seasonId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, TeamFanBase>> ListBySeasonIndexAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var rows = await ListBySeasonAsync(seasonId, cancellationToken);

        return rows.ToDictionary(fanBase => fanBase.TeamId);
    }

    public async Task<HashSet<Guid>> ListTeamsWithARowAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var ids = await _dbContext.TeamFanBases
            .AsNoTracking()
            .Where(fanBase => fanBase.SeasonId == seasonId)
            .Select(fanBase => fanBase.TeamId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public Task<TeamFanBase?> GetAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        _dbContext.TeamFanBases
            .FirstOrDefaultAsync(fanBase => fanBase.TeamId == teamId && fanBase.SeasonId == seasonId, cancellationToken);

    public async Task AddRangeAsync(
        IEnumerable<TeamFanBase> fanBases,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamFanBases.AddRangeAsync(fanBases, cancellationToken);

    /// <summary>
    /// Copies the caller's values onto the tracked instance rather than attaching the caller's
    /// row, because the caller read it with <c>AsNoTracking</c> and the context is already
    /// holding a copy of the same row from its own earlier read.
    /// </summary>
    public void Update(TeamFanBase fanBase)
    {
        ArgumentNullException.ThrowIfNull(fanBase);

        var tracked = _dbContext.TeamFanBases.Local
            .FirstOrDefault(candidate => candidate.Id == fanBase.Id);

        if (tracked is null)
        {
            _dbContext.TeamFanBases.Update(fanBase);
            return;
        }

        tracked.CloseTheSeasonWith(fanBase.Supporters, fanBase.PeakSupporters, fanBase.UpdatedAt);
    }
}