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

    /// <summary>
    /// Marks a window's row as changed.
    ///
    /// <para>
    /// The same reason as <c>MatchRepository.Update</c>: reads here are <c>AsNoTracking</c>, and
    /// a window walk reads the same round through the calendar, the due list and the claim
    /// before it writes to it. Whichever of those reads left the row tracked, the write is a
    /// second instance of a key the context is holding — and that is a window that cannot be
    /// closed, which is a matchday nobody will ever come back to.
    /// </para>
    /// </summary>
    public void Update(Round round)
    {
        var tracked = _dbContext.Rounds.Local
            .FirstOrDefault(candidate => candidate.Id == round.Id);

        if (tracked is null)
        {
            _dbContext.Rounds.Update(round);
            return;
        }

        if (!ReferenceEquals(tracked, round))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(round);
        }
    }

    public void RemoveRange(IEnumerable<Round> rounds) => _dbContext.Rounds.RemoveRange(rounds);
}
