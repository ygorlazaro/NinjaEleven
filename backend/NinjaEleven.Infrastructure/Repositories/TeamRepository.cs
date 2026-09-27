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
}
