using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class ClubEventRepository : IClubEventRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public ClubEventRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ClubEvent>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ClubEvents
            .AsNoTracking()
            .Where(clubEvent => clubEvent.TeamId == teamId)
            // Newest first because a career is read from now backwards: the moment a manager
            // is living through is the first line he reads and the founding is the last.
            .OrderByDescending(clubEvent => clubEvent.OccurredAt)
            .ThenByDescending(clubEvent => clubEvent.Id)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ClubEvent clubEvent, CancellationToken cancellationToken = default) =>
        await _dbContext.ClubEvents.AddAsync(clubEvent, cancellationToken);
}