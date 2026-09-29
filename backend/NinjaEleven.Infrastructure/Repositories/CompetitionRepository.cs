using NinjaEleven.Application.Models;
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

    public async Task<CompetitionSeason?> GetSeasonForDivisionAsync(
        Guid competitionId,
        Guid seasonId,
        Guid divisionId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                competitionSeason => competitionSeason.CompetitionId == competitionId
                                     && competitionSeason.SeasonId == seasonId
                                     && competitionSeason.DivisionId == divisionId,
                cancellationToken);

    /// <summary>
    /// Every edition with the kind of competition it is and the tier it is the table of.
    ///
    /// It is a query rather than a set of includes because a service is handed repository
    /// interfaces and not a database context, and because the answer a caller actually wants
    /// is one row per edition: "the 1st division and the cup" is a list, not two entities
    /// with navigations the caller has to walk.
    /// </summary>
    private IQueryable<CompetitionSeasonView> SeasonViews =>
        from competitionSeason in _dbContext.CompetitionSeasons.AsNoTracking()
        join competition in _dbContext.Competitions.AsNoTracking()
            on competitionSeason.CompetitionId equals competition.Id
        join division in _dbContext.Divisions.AsNoTracking()
            on competitionSeason.DivisionId equals division.Id into divisions
        from division in divisions.DefaultIfEmpty()
        select new CompetitionSeasonView
        {
            Id = competitionSeason.Id,
            CompetitionId = competitionSeason.CompetitionId,
            SeasonId = competitionSeason.SeasonId,
            DivisionId = competitionSeason.DivisionId,
            Tier = division == null ? (int?)null : division.Tier,
            CompetitionName = competition.Name,
            Type = competition.Type
        };

    public async Task<CompetitionSeasonView?> GetSeasonViewByIdAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await SeasonViews
            .FirstOrDefaultAsync(view => view.Id == competitionSeasonId, cancellationToken);

    public async Task<IReadOnlyList<CompetitionSeasonView>> ListSeasonViewsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await SeasonViews
            .Where(view => view.SeasonId == seasonId)
            // Divisions first, top tier first, and the knockouts after them: that is the order
            // a season is read in and the order a calendar is laid out in.
            .OrderBy(view => view.Tier ?? int.MaxValue)
            .ThenBy(view => view.Type)
            .ThenBy(view => view.CompetitionName)
            .ToListAsync(cancellationToken);

    public async Task<Dictionary<Guid, IReadOnlyList<CompetitionSeasonView>>> ListSeasonViewsAsync(
        IEnumerable<Guid> seasonIds,
        CancellationToken cancellationToken = default)
    {
        var seasonIdList = seasonIds.ToList();
        
        var views = await SeasonViews
            .Where(view => seasonIdList.Contains(view.SeasonId))
            .OrderBy(view => view.SeasonId)
            .ThenBy(view => view.Tier ?? int.MaxValue)
            .ThenBy(view => view.Type)
            .ThenBy(view => view.CompetitionName)
            .ToListAsync(cancellationToken);

        return views
            .GroupBy(v => v.SeasonId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CompetitionSeasonView>)g.ToList());
    }

    public async Task<CompetitionSeasonView?> GetDivisionSeasonForTeamAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await (from view in SeasonViews
               join participant in _dbContext.CompetitionParticipants.AsNoTracking()
                   on view.Id equals participant.CompetitionSeasonId
               where participant.TeamId == teamId
                     && view.SeasonId == seasonId
                     && view.DivisionId != null
               // Best tier first: a club entered in more than one division of a season is a
               // broken enrolment, and the table a manager should be sent to is the one its
               // players are actually seeded in.
               orderby view.Tier
               select view)
            .FirstOrDefaultAsync(cancellationToken);

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
            // Ordered, because the list this builds is the input order of a league table and
            // a table is only as stable as the order it is handed. Without this the database
            // returns the rows in whatever order it likes today, and two clubs level on points,
            // goal difference, goals scored, head-to-head, cards and squad strength are drawn
            // in that arbitrary order — so a manager watching the live table sees rows swap
            // places for no football reason at all.
            .OrderBy(participant => participant.TeamId)
            .ToListAsync(cancellationToken);

    public async Task AddParticipantsAsync(
        IEnumerable<CompetitionParticipant> participants,
        CancellationToken cancellationToken = default) =>
        await _dbContext.CompetitionParticipants.AddRangeAsync(participants, cancellationToken);
}
