using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Infrastructure.Persistence;

namespace NinjaEleven.Infrastructure.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public SeasonRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .OrderBy(season => season.Number)
            .ToListAsync(cancellationToken);

    public async Task<Season?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Id == id, cancellationToken);

    public async Task<Season?> GetByNumberAsync(int number, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Number == number, cancellationToken);

    public async Task<Season?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(season => season.Status == SeasonStatus.InProgress, cancellationToken);

    /// <summary>
    /// How many rounds of a season's championship are still to come.
    /// </summary>
    /// <remarks>
    /// The championship is three editions of one competition, so a season's windows are only
    /// findable by walking all three — which is why this query joins the rounds to the
    /// editions of the league rather than reading a season's own row. The two questions it
    /// answers are asked in one go: which windows exist, and how many of them are closed.
    /// </remarks>
    public async Task<int> GetChampionshipRoundsLeftAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        // The editions of this season that are the championship, and the windows of each. One
        // query for the set rather than one per division: a page that quotes a settlement for
        // a squad of twenty-three men must not read the calendar twenty-three times.
        var windows = await (
            from round in _dbContext.Rounds.AsNoTracking()
            join competitionSeason in _dbContext.CompetitionSeasons.AsNoTracking()
                on round.CompetitionSeasonId equals competitionSeason.Id
            join competition in _dbContext.Competitions.AsNoTracking()
                on competitionSeason.CompetitionId equals competition.Id
            where competitionSeason.SeasonId == seasonId
                  && competition.Type == CompetitionType.League
            select new { round.Number, round.CompletedAt })
            .ToListAsync(cancellationToken);

        var played = windows
            .Where(window => window.CompletedAt is not null)
            .Select(window => window.Number)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(0, CompetitionRules.LeagueMatchDays - played);
    }

    public async Task AddAsync(Season season, CancellationToken cancellationToken = default) =>
        await _dbContext.Seasons.AddAsync(season, cancellationToken);

    /// <summary>
    /// Marks a season's row as changed.
    ///
    /// <para>
    /// The same reason as <c>MatchRepository.Update</c>: reads here are <c>AsNoTracking</c>, so
    /// the context may already be holding this season from an earlier read on the same request.
    /// A season close reads the season it is closing, opens the next one, and then writes to
    /// the one it closed — and a second instance of that key is an error EF refuses from inside
    /// the middle of a close, which is the one place a half-finished close is worst.
    /// </para>
    /// </summary>
    public void Update(Season season)
    {
        var tracked = _dbContext.Seasons.Local
            .FirstOrDefault(candidate => candidate.Id == season.Id);

        if (tracked is null)
        {
            _dbContext.Seasons.Update(season);
            return;
        }

        if (!ReferenceEquals(tracked, season))
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(season);
        }
    }
}
