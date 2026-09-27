using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public TeamRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Team>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);

    public async Task<Team?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .FirstOrDefaultAsync(team => team.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Team>> ListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .Where(team => ids.Contains(team.Id))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default) =>
        await _dbContext.Teams.AddAsync(team, cancellationToken);

    public void Update(Team team) => _dbContext.Teams.Update(team);

    public void Remove(Team team) => _dbContext.Teams.Remove(team);

    public async Task<IReadOnlyList<TeamMembership>> GetSquadAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        var referenceDate = season?.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .Where(membership => membership.StartDate <= referenceDate
                                 && (membership.EndDate == null || membership.EndDate >= referenceDate))
            .OrderBy(membership => membership.PlayerId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The contracts a club still holds. A membership with no end date is a deal that has not
    /// been called off, and that is the whole test: the day a player leaves — by transfer or
    /// by retirement — his membership is given an end date, and he stops being a man of this
    /// club on the same day, whatever else the game grows to say about him.
    /// </summary>
    public async Task<IReadOnlyList<TeamMembership>> GetLiveContractsAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId && membership.EndDate == null)
            .OrderBy(membership => membership.PlayerId)
            .ToListAsync(cancellationToken);
}
