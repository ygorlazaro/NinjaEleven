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

    public async Task<IReadOnlyList<Round>> ListByCompetitionSeasonIdsAsync(
        IEnumerable<Guid> competitionSeasonIds,
        CancellationToken cancellationToken = default)
    {
        var ids = competitionSeasonIds.ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<Round>();
        }

        return await _dbContext.Rounds
            .AsNoTracking()
            .Where(round => ids.Contains(round.CompetitionSeasonId))
            .OrderBy(round => round.CompetitionSeasonId)
            .ThenBy(round => round.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Round>> ListByMatchDayAsync(
        Guid matchDayId,
        CancellationToken cancellationToken = default) =>        await _dbContext.Rounds
            .AsNoTracking()
            .Where(round => round.MatchDayId == matchDayId)
            .OrderBy(round => round.Window)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Round round, CancellationToken cancellationToken = default) =>
        await _dbContext.Rounds.AddAsync(round, cancellationToken);

    public void Update(Round round) => _dbContext.Rounds.Update(round);

    public void RemoveRange(IEnumerable<Round> rounds) => _dbContext.Rounds.RemoveRange(rounds);
}
