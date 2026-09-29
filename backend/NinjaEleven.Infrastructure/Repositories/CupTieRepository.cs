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
            // The identifier is the tie-breaker, and it is there for one reason: a bracket whose
            // sixteen ties changed places every time it was asked for is a bracket nobody can
            // point at a club in. The pairing order itself is not stored — a tie does not know
            // it was the seventh of its round — so this is stability, not seeding.
            .OrderBy(tie => tie.RoundNumber)
            .ThenBy(tie => tie.Id)
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

    /// <summary>
    /// Gets the club IDs that are still alive in the cup (haven't been eliminated yet).
    /// A club is alive if it has won its latest tie or hasn't lost a tie yet.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetAliveClubsInCupAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var ties = await _dbContext.CupTies
            .AsNoTracking()
            .Where(tie => tie.CompetitionSeasonId == competitionSeasonId)
            .ToListAsync(cancellationToken);

        if (ties.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        // Find eliminated clubs: those that lost a resolved tie
        var eliminated = new HashSet<Guid>();
        foreach (var tie in ties)
        {
            if (tie.IsResolved && tie.LoserTeamId.HasValue)
            {
                eliminated.Add(tie.LoserTeamId.Value);
            }
        }

        // All clubs that participated but aren't eliminated are alive
        var allClubs = new HashSet<Guid>();
        foreach (var tie in ties)
        {
            allClubs.Add(tie.HomeTeamId);
            allClubs.Add(tie.AwayTeamId);
        }

        return allClubs.Where(c => !eliminated.Contains(c)).ToList();
    }

    public async Task<Dictionary<Guid, IReadOnlyList<CupTie>>> ListByCompetitionSeasonsAsync(
        IEnumerable<Guid> competitionSeasonIds,
        CancellationToken cancellationToken = default)
    {
        var seasonIdList = competitionSeasonIds.ToList();
        
        var ties = await _dbContext.CupTies
            .AsNoTracking()
            .Where(tie => seasonIdList.Contains(tie.CompetitionSeasonId))
            .OrderBy(tie => tie.CompetitionSeasonId)
            .ThenBy(tie => tie.RoundNumber)
            .ThenBy(tie => tie.Id)
            .ToListAsync(cancellationToken);

        return ties
            .GroupBy(t => t.CompetitionSeasonId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CupTie>)g.ToList());
    }
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
