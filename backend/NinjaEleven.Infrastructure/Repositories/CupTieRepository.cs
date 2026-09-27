using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class CupTieRepository : ICupTieRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public CupTieRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CupTie>> ListByCompetitionSeasonAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CupTies
            .AsNoTracking()
            .Where(tie => tie.CompetitionSeasonId == competitionSeasonId)
            .OrderBy(tie => tie.RoundNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CupTie>> ListByRoundAsync(
        Guid competitionSeasonId,
        int roundNumber,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CupTies
            .AsNoTracking()
            .Where(tie => tie.CompetitionSeasonId == competitionSeasonId && tie.RoundNumber == roundNumber)
            .ToListAsync(cancellationToken);

    public async Task<CupTie?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.CupTies
            .FirstOrDefaultAsync(tie => tie.Id == id, cancellationToken);

    public async Task<CupTie?> GetByLegAsync(Guid fixtureId, CancellationToken cancellationToken = default) =>
        await _dbContext.CupTies
            .FirstOrDefaultAsync(
                tie => tie.FirstLegFixtureId == fixtureId || tie.SecondLegFixtureId == fixtureId,
                cancellationToken);

    public async Task AddRangeAsync(IEnumerable<CupTie> ties, CancellationToken cancellationToken = default) =>
        await _dbContext.CupTies.AddRangeAsync(ties, cancellationToken);

    public void Update(CupTie tie) => _dbContext.CupTies.Update(tie);
}

public class TrophyRepository : ITrophyRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public TrophyRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TrophyAward>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TrophyAwards
            .AsNoTracking()
            .Where(trophy => trophy.TeamId == teamId)
            .OrderByDescending(trophy => trophy.WonAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrophyAward>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TrophyAwards
            .AsNoTracking()
            .Where(trophy => trophy.SeasonId == seasonId)
            .OrderBy(trophy => trophy.Position)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(
        IEnumerable<TrophyAward> trophies,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TrophyAwards.AddRangeAsync(trophies, cancellationToken);
}
