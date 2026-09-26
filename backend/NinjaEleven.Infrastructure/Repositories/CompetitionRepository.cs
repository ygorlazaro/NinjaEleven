using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class CompetitionRepository : ICompetitionRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public CompetitionRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Competition>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Competitions
            .AsNoTracking()
            .OrderBy(competition => competition.Name)
            .ToListAsync(cancellationToken);

    public async Task<Competition?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(competition => competition.Id == id, cancellationToken);

    public async Task AddAsync(Competition competition, CancellationToken cancellationToken = default) =>
        await _dbContext.Competitions.AddAsync(competition, cancellationToken);

    public void Update(Competition competition) => _dbContext.Competitions.Update(competition);

    public async Task<IReadOnlyList<Competition>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await (from competition in _dbContext.Competitions.AsNoTracking()
               join competitionSeason in _dbContext.CompetitionSeasons.AsNoTracking()
                   on competition.Id equals competitionSeason.CompetitionId
               where competitionSeason.SeasonId == seasonId
               orderby competition.Name
               select competition)
            .ToListAsync(cancellationToken);

    public async Task<CompetitionSeason?> GetSeasonAsync(
        Guid competitionId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                competitionSeason => competitionSeason.CompetitionId == competitionId
                                     && competitionSeason.SeasonId == seasonId,
                cancellationToken);

    public async Task<CompetitionSeason?> GetSeasonByIdAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                competitionSeason => competitionSeason.Id == competitionSeasonId,
                cancellationToken);

    public async Task AddSeasonAsync(
        CompetitionSeason competitionSeason,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionSeasons.AddAsync(competitionSeason, cancellationToken);

    public async Task<IReadOnlyList<CompetitionParticipant>> ListParticipantsAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionParticipants
            .AsNoTracking()
            .Where(participant => participant.CompetitionSeasonId == competitionSeasonId)
            .ToListAsync(cancellationToken);

    public async Task AddParticipantsAsync(
        IEnumerable<CompetitionParticipant> participants,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionParticipants.AddRangeAsync(participants, cancellationToken);
}
