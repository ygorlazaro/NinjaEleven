using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class RoundRepository : IRoundRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public RoundRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Round>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Rounds
            .AsNoTracking()
            .OrderBy(round => round.CompetitionSeasonId)
            .ThenBy(round => round.Number)
            .ToListAsync(cancellationToken);

    public async Task<Round?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Rounds
            .AsNoTracking()
            .FirstOrDefaultAsync(round => round.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Round>> ListByCompetitionSeasonAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Rounds
            .AsNoTracking()
            .Where(round => round.CompetitionSeasonId == competitionSeasonId)
            .OrderBy(round => round.Number)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Round round, CancellationToken cancellationToken = default) =>
        await _dbContext.Rounds.AddAsync(round, cancellationToken);
}
