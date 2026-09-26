using FootballManager.Application.Repositories;
using FootballManager.Domain.Matches;
using FootballManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FootballManager.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly FootballManagerDbContext _dbContext;

    public MatchRepository(FootballManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Match>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .OrderBy(match => match.FixtureId)
            .ToListAsync(cancellationToken);

    public async Task<Match?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(match => match.Id == id, cancellationToken);

    public async Task<Match?> GetByFixtureAsync(Guid fixtureId, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(match => match.FixtureId == fixtureId, cancellationToken);

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default) =>
        await _dbContext.Matches.AddAsync(match, cancellationToken);

    public void Update(Match match) => _dbContext.Matches.Update(match);

    public async Task<IReadOnlyList<MatchEvent>> ListEventsAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchEvents
            .AsNoTracking()
            .Where(matchEvent => matchEvent.MatchId == matchId)
            .OrderBy(matchEvent => matchEvent.Sequence)
            .ToListAsync(cancellationToken);

    public async Task AddEventsAsync(
        IEnumerable<MatchEvent> events,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchEvents.AddRangeAsync(events, cancellationToken);

    public async Task<MatchStatistics?> GetStatisticsAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(statistics => statistics.MatchId == matchId, cancellationToken);

    public async Task AddStatisticsAsync(
        MatchStatistics statistics,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchStatistics.AddAsync(statistics, cancellationToken);
}
