using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public TeamRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Team>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .Include(team => team.Managers)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);

    public async Task<Team?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .Include(team => team.Managers)
            .FirstOrDefaultAsync(team => team.Id == id, cancellationToken);

    /// <summary>
    /// The one club a person is playing. It is read tracked like <see cref="GetAsync"/> is not,
    /// because the market sweeps only ever read it and a tracked entity nobody saves is a
    /// needless attach — but the flag is a column, not a navigation, so nothing here can be
    /// lazily loaded away underneath a caller that expected a whole club.
    /// </summary>
    public async Task<Team?> GetManagerClubAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .FirstOrDefaultAsync(team => team.IsManagerClub, cancellationToken);

    /// <summary>
    /// Marks the club as the manager's, tracked so the flag can be written, and unmarks the one
    /// that held it. The rows come back tracked rather than through <see cref="GetAsync"/>,
    /// which reads without tracking and would leave the change unattached to the context.
    /// </summary>
    public async Task<Team> MarkAsManagerClubAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var club = await _dbContext.Teams
            .FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var others = await _dbContext.Teams
            .Where(team => team.Id != teamId && team.IsManagerClub)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.ClearManagerClub();
        }

        if (!club.IsManagerClub)
        {
            club.MarkAsManagerClub();
        }

        return club;
    }

    public async Task ClearManagerClubAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var club = await _dbContext.Teams
            .FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);

        club?.ClearManagerClub();
    }

    public async Task<IReadOnlyList<Team>> ListByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .Include(team => team.Managers)
            .Where(team => ids.Contains(team.Id))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default) =>
        await _dbContext.Teams.AddAsync(team, cancellationToken);

    public void Update(Team team) => _dbContext.Teams.Update(team);

    public void Remove(Team team) => _dbContext.Teams.Remove(team);

    public async Task<IReadOnlyList<TeamMembership>> GetSquadAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        var referenceDate = season?.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .Where(membership => membership.StartDate <= referenceDate
                                 && (membership.EndDate == null || membership.EndDate >= referenceDate))
            .OrderBy(membership => membership.PlayerId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The contracts a club still holds. A membership with no end date is a deal that has not
    /// been called off, and that is the whole test: the day a player leaves — by transfer or
    /// by retirement — his membership is given an end date, and he stops being a man of this
    /// club on the same day, whatever else the game grows to say about him.
    /// </summary>
    public async Task<IReadOnlyList<TeamMembership>> GetLiveContractsAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId && membership.EndDate == null)
            .OrderBy(membership => membership.PlayerId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every membership that is in force right now, in one read, so a market search can build
    /// its listings without asking a club at a time.
    ///
    /// The membership that has an end date inside the season is deliberately left out, and that
    /// is the whole correction. A player let go on the ninth round touched the season and is
    /// part of its football, but he is not on any book's books today, and a market that counted
    /// him would be wrong in both directions at once: his old club's book is one man larger than
    /// it is — so the club is allowed to sell below the minimum and does — and the man himself
    /// is not a free agent, because a contract that was given up is still in the list and the
    /// market reads "already contracted" off it. The player who has been released is the one
    /// player a short club is most likely to want.
    ///
    /// The season is still asked for, because a membership that ends before the season began
    /// was never part of it and a membership that starts after it ended is not either. What
    /// comes back is the book as it stands, which is the only book anybody can buy from or sell
    /// into.
    /// </summary>
    public async Task<IReadOnlyList<TeamMembership>> ListAllContractsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (season is null)
        {
            return Array.Empty<TeamMembership>();
        }

        return await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.EndDate == null
                                 && membership.StartDate <= season.EndDate)
            .OrderBy(membership => membership.TeamId)
            .ThenBy(membership => membership.PlayerId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddMembershipAsync(
        TeamMembership membership,
        CancellationToken cancellationToken = default) =>
        await _dbContext.TeamMemberships.AddAsync(membership, cancellationToken);

    public void UpdateMembership(TeamMembership membership) =>
        _dbContext.TeamMemberships.Update(membership);

    public async Task<IReadOnlyList<Team>> ListClubsWithoutManagerAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .Where(team => !team.Managers.Any(m => m.UserId.HasValue))
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Clubs without human manager in a specific season (participating in the league).
    /// </summary>
    public async Task<IReadOnlyList<Team>> ListClubsWithoutManagerInSeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        // Get clubs participating in league competitions for this season
        var participantTeamIds = await _dbContext.CompetitionParticipants
            .AsNoTracking()
            .Join(_dbContext.CompetitionSeasons,
                cp => cp.CompetitionSeasonId,
                cs => cs.Id,
                (cp, cs) => new { cp.TeamId, cs.CompetitionId, cs.SeasonId })
            .Where(x => x.SeasonId == seasonId)
            .Join(_dbContext.Competitions,
                x => x.CompetitionId,
                c => c.Id,
                (x, c) => new { x.TeamId, c.Type })
            .Where(x => x.Type == NinjaEleven.Domain.Enums.CompetitionType.League)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);

        return await _dbContext.Teams
            .AsNoTracking()
            .Include(team => team.Stadium)
            .Where(team => participantTeamIds.Contains(team.Id))
            .Where(team => !team.Managers.Any(m => m.UserId.HasValue))
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a player by ID.
    /// </summary>
    public async Task<Player?> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await _dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);

    /// <summary>
    /// Gets the division tier for teams in a season from competition participants.
    /// </summary>
    public async Task<Dictionary<Guid, int>> GetTeamDivisionsAsync(
        Guid seasonId,
        IEnumerable<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        var teamIdList = teamIds.ToList();
        
        return await _dbContext.CompetitionParticipants
            .AsNoTracking()
            .Join(_dbContext.CompetitionSeasons,
                cp => cp.CompetitionSeasonId,
                cs => cs.Id,
                (cp, cs) => new { cp.TeamId, cs.CompetitionId, cs.SeasonId, cs.DivisionId })
            .Where(x => x.SeasonId == seasonId && teamIdList.Contains(x.TeamId) && x.DivisionId.HasValue)
            .Join(_dbContext.Divisions,
                x => x.DivisionId!.Value,
                d => d.Id,
                (x, d) => new { x.TeamId, d.Tier })
            .ToDictionaryAsync(x => x.TeamId, x => x.Tier, cancellationToken);
    }

    /// <summary>
    /// Gets squads for multiple teams in a season in a single query.
    /// </summary>
    public async Task<Dictionary<Guid, IReadOnlyList<TeamMembership>>> GetSquadsAsync(
        IEnumerable<Guid> teamIds,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var teamIdList = teamIds.ToList();
        var seasonEntity = await _dbContext.Seasons
            .AsNoTracking()
            .FirstAsync(s => s.Id == seasonId, cancellationToken);

        var memberships = await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(m => teamIdList.Contains(m.TeamId)
                     && m.StartDate <= seasonEntity.EndDate
                     && (m.EndDate == null || m.EndDate >= seasonEntity.StartDate))
            .ToListAsync(cancellationToken);

        return memberships
            .GroupBy(m => m.TeamId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TeamMembership>)g.OrderBy(m => m.PlayerId).ToList());
    }

    /// <summary>
    /// Gets multiple players by their IDs in a single query.
    /// </summary>
    public async Task<Dictionary<Guid, Player>> GetPlayersAsync(
        IEnumerable<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        var playerIdList = playerIds.ToList();
        
        return await _dbContext.Players
            .AsNoTracking()
            .Where(p => playerIdList.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    /// <summary>
    /// Gets all squads for a season in a single query.
    /// </summary>
    public async Task<Dictionary<Guid, IReadOnlyList<TeamMembership>>> GetAllSquadsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var seasonEntity = await _dbContext.Seasons
            .AsNoTracking()
            .FirstAsync(s => s.Id == seasonId, cancellationToken);

        var memberships = await _dbContext.TeamMemberships
            .AsNoTracking()
            .Where(m => m.StartDate <= seasonEntity.EndDate
                     && (m.EndDate == null || m.EndDate >= seasonEntity.StartDate))
            .ToListAsync(cancellationToken);

        return memberships
            .GroupBy(m => m.TeamId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TeamMembership>)g.OrderBy(m => m.PlayerId).ToList());
    }
}
