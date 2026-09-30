using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class FixtureRepository : IFixtureRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public FixtureRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Fixture>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .OrderBy(fixture => fixture.RoundId)
            .ToListAsync(cancellationToken);

    public async Task<Fixture?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .FirstOrDefaultAsync(fixture => fixture.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Fixture>> ListByRoundAsync(
        Guid roundId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .Where(fixture => fixture.RoundId == roundId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Fixture>> ListByRoundIdsAsync(
        IEnumerable<Guid> roundIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures
            .AsNoTracking()
            .Where(fixture => roundIds.Contains(fixture.RoundId))
            .OrderBy(fixture => fixture.RoundId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The walk of fixture, window and matchday done as one query, because the question it
    /// answers is asked per click and a calendar read to find it is a season of football the
    /// database did not need to hand over.
    ///
    /// <para>
    /// The joins are written out rather than walked through navigations because there are none
    /// to walk: a fixture's window and a window's matchday are configured as foreign keys with
    /// no collection on the far side, so <c>fixture.Round</c> is not a property that exists and
    /// the join is the only way to cross from one row to the next.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Fixture>> ListByTeamAndDateAsync(
        Guid teamId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        await (
                from fixture in _dbContext.Fixtures.AsNoTracking()
                join round in _dbContext.Rounds.AsNoTracking() on fixture.RoundId equals round.Id
                join matchDay in _dbContext.MatchDays.AsNoTracking()
                    on round.MatchDayId.Value equals matchDay.Id
                where (fixture.HomeTeamId == teamId || fixture.AwayTeamId == teamId)
                      && matchDay.Date == date
                select fixture)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Fixture fixture, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures.AddAsync(fixture, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Fixture> fixtures, CancellationToken cancellationToken = default) =>
        await _dbContext.Fixtures.AddRangeAsync(fixtures, cancellationToken);

    /// <summary>
    /// Marks a fixture's row as changed.
    ///
    /// <para>
    /// The same reason as <c>MatchRepository.Update</c>, and the same shape of fix: reads here
    /// are <c>AsNoTracking</c>, so a command holding a fixture is holding a copy the context
    /// does not own, and anything else on the command's path that attaches one leaves the
    /// context with a second instance of the same row. EF will not attach the second, so the
    /// values are written onto whichever instance is already tracked.
    /// </para>
    /// </summary>
    public void Update(Fixture fixture)
    {
        var tracked = _dbContext.Fixtures.Local.FirstOrDefault(candidate => candidate.Id == fixture.Id);

        if (tracked is null)
        {
            _dbContext.Fixtures.Update(fixture);
            return;
        }

        if (!ReferenceEquals(tracked, fixture))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(fixture);
        }
    }

    public void RemoveRange(IEnumerable<Fixture> fixtures) => _dbContext.Fixtures.RemoveRange(fixtures);
}
