using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly NinjaElevenDbContext _dbContext;

    public PlayerRepository(NinjaElevenDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Player>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Players
            .AsNoTracking()
            .OrderBy(player => player.Name)
            .ToListAsync(cancellationToken);

    public async Task<Player?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(player => player.Id == id, cancellationToken);

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default) =>
        await _dbContext.Players.AddAsync(player, cancellationToken);

    public void Update(Player player) => _dbContext.Players.Update(player);

    public void Remove(Player player) => _dbContext.Players.Remove(player);

    public async Task<PlayerSeasonState?> GetSeasonStateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.PlayerSeasonStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.PlayerId == playerId && state.SeasonId == seasonId, cancellationToken);

    public async Task<IReadOnlyList<PlayerSeasonState>> ListSeasonStatesAsync(
        Guid seasonId,
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PlayerSeasonStates
            .AsNoTracking()
            .Where(state => state.SeasonId == seasonId);

        if (teamId.HasValue)
        {
            query = query.Where(state => state.TeamId == teamId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerSeasonState>> ListSeasonStatesByPlayerIdsAsync(
        Guid seasonId,
        IEnumerable<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        var playerIdList = playerIds.ToList();

        if (playerIdList.Count == 0)
        {
            return Array.Empty<PlayerSeasonState>();
        }

        return await _dbContext.PlayerSeasonStates
            .AsNoTracking()
            .Where(state => state.SeasonId == seasonId && playerIdList.Contains(state.PlayerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlayerSeasonState?> GetSeasonStateForUpdateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.PlayerSeasonStates
            .FirstOrDefaultAsync(state => state.PlayerId == playerId && state.SeasonId == seasonId, cancellationToken);

    public void UpdateSeasonState(PlayerSeasonState seasonState) =>
        _dbContext.PlayerSeasonStates.Update(seasonState);

    /// <summary>
    /// The goals of a club's men in a season, summed from the match lines and grouped by
    /// player.
    /// </summary>
    public async Task<IReadOnlyList<ClubScorerLine>> ListClubScorerLinesAsync(
        Guid teamId,
        Guid seasonId,
        CompetitionType? competitionType = null,
        CancellationToken cancellationToken = default)
    {
        // The chain from a match line to a kind of competition: line → match → fixture →
        // round → edition → competition. Only the last link carries the kind, so the whole
        // walk is done in the database and nothing is loaded into memory to be filtered there.
        var query =
            from line in _dbContext.MatchPlayerStatistics.AsNoTracking()
            join match in _dbContext.Matches.AsNoTracking() on line.MatchId equals match.Id
            join fixture in _dbContext.Fixtures.AsNoTracking() on match.FixtureId equals fixture.Id
            join round in _dbContext.Rounds.AsNoTracking() on fixture.RoundId equals round.Id
            join edition in _dbContext.CompetitionSeasons.AsNoTracking()
                on round.CompetitionSeasonId equals edition.Id
            join competition in _dbContext.Competitions.AsNoTracking()
                on edition.CompetitionId equals competition.Id
            where line.TeamId == teamId && line.SeasonId == seasonId
            select new { line, competition.Type };

        if (competitionType.HasValue)
        {
            var wanted = competitionType.Value;
            query = query.Where(row => row.Type == wanted);
        }

        return ReadLines(await GroupAsync(query.Select(row => row.line), cancellationToken));
    }

    /// <summary>
    /// A season's goals by player, across every club in it, and optionally restricted to one
    /// kind of competition.
    /// </summary>
    /// <remarks>
    /// The chain is the club list's, without the club: a line knows its match, a match its
    /// fixture, a fixture its round, and only the round's edition knows the kind of competition
    /// the tie was. So a cup chart is a walk and not a column, and the same walk the club's own
    /// scorers page walks — which is the point of a rule being in one place.
    /// </remarks>
    public async Task<IReadOnlyList<ClubScorerLine>> ListSeasonScorerLinesAsync(
        Guid seasonId,
        CompetitionType? competitionType = null,
        CancellationToken cancellationToken = default)
    {
        var query =
            from line in _dbContext.MatchPlayerStatistics.AsNoTracking()
            join match in _dbContext.Matches.AsNoTracking() on line.MatchId equals match.Id
            join fixture in _dbContext.Fixtures.AsNoTracking() on match.FixtureId equals fixture.Id
            join round in _dbContext.Rounds.AsNoTracking() on fixture.RoundId equals round.Id
            join edition in _dbContext.CompetitionSeasons.AsNoTracking()
                on round.CompetitionSeasonId equals edition.Id
            join competition in _dbContext.Competitions.AsNoTracking()
                on edition.CompetitionId equals competition.Id
            where line.SeasonId == seasonId
            select new { line, competition.Type };

        if (competitionType.HasValue)
        {
            var wanted = competitionType.Value;
            query = query.Where(row => row.Type == wanted);
        }

        return ReadLines(await GroupAsync(query.Select(row => row.line), cancellationToken));
    }

    /// <summary>
    /// One edition's goals by player, across every club in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edition and not the kind of competition, because the championship is three editions of
    /// one kind. A season's list restricted to "League" counts all three divisions at once, which
    /// is right for a chart of the country and wrong for the artilharia of one division: the
    /// first division's prize list built that way would be topped by a second-division striker
    /// and then paid the first division's money.
    /// </para>
    /// <para>
    /// A cup is one edition of its own, so this is how the cup's chart is read as well — one
    /// walk, and the cup needs no special case to be counted correctly.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ClubScorerLine>> ListEditionScorerLinesAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var query =
            from line in _dbContext.MatchPlayerStatistics.AsNoTracking()
            join match in _dbContext.Matches.AsNoTracking() on line.MatchId equals match.Id
            join fixture in _dbContext.Fixtures.AsNoTracking() on match.FixtureId equals fixture.Id
            join round in _dbContext.Rounds.AsNoTracking() on fixture.RoundId equals round.Id
            where round.CompetitionSeasonId == competitionSeasonId
            select line;

        return ReadLines(await GroupAsync(query, cancellationToken));
    }

    /// <summary>
    /// Sums a set of match lines into one row per player and club.
    /// </summary>
    /// <remarks>
    /// The goals, the games and the cards are read out of the same lines here, in one
    /// projection, so no list of scorers can be built with one of the three missing — a table
    /// that could count goals and not bookings could not order two level strikers at all.
    ///
    /// Own goals are summed into their own column and never added to the goals: a goal conceded
    /// into his own net is a defender's error, and a scorers table that counted it would hand a
    /// centre-back a column of goals he did not score.
    /// </remarks>
    private static Task<List<ClubScorerLine>> GroupAsync(
        IQueryable<MatchPlayerStatistics> lines,
        CancellationToken cancellationToken) =>
        lines
            .GroupBy(line => new { line.PlayerId, line.TeamId })
            .Select(group => new ClubScorerLine
            {
                PlayerId = group.Key.PlayerId,
                TeamId = group.Key.TeamId,
                Goals = group.Sum(line => line.Goals),
                OwnGoals = group.Sum(line => line.OwnGoals),
                Started = group.Sum(line => line.Started ? 1 : 0),
                CameOn = group.Sum(line => line.CameOn ? 1 : 0),
                YellowCards = group.Sum(line => line.YellowCards),
                RedCards = group.Sum(line => line.RedCards)
            })
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Only men who scored, in the order they are read in.
    /// </summary>
    /// <remarks>
    /// A table of scorers that carried every man who played would put a defender who never
    /// scored in the same list as a striker, and the list would then be a squad.
    /// </remarks>
    private static IReadOnlyList<ClubScorerLine> ReadLines(List<ClubScorerLine> rows) =>
        rows
            .Where(line => line.Goals > 0)
            .OrderByDescending(line => line.Goals)
            .ThenBy(line => line.PlayerId)
            .ToList();

    public async Task<IReadOnlyList<PlayerSeasonState>> ListAllSeasonStatesAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.PlayerSeasonStates
            .AsNoTracking()
            .Where(state => state.SeasonId == seasonId)
            .OrderBy(state => state.PlayerId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every player's whole career, in one read: the sum of every match line he has, for every
    /// club at once.
    ///
    /// A market screen shows a hundred and twenty men at a time, and a career each read one
    /// player at a time is a hundred and twenty queries for a list. The career is therefore
    /// summed in the database and keyed by the player, which is also the only way the totals
    /// can be right: they are the same sum the profile assembles, from the same lines.
    /// </summary>
    public async Task<IReadOnlyList<CareerTotals>> ListCareerTotalsAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.MatchPlayerStatistics
            .AsNoTracking()
            .GroupBy(line => line.PlayerId)
            .Select(group => new CareerTotals
            {
                PlayerId = group.Key,
                Line = new PlayerCareerLine
                {
                    Appearances = group.Count(),
                    Started = group.Sum(line => line.Started ? 1 : 0),
                    CameOn = group.Sum(line => line.CameOn ? 1 : 0),
                    BenchUnused = group.Sum(line => line.WasOnBenchUnused ? 1 : 0),
                    Goals = group.Sum(line => line.Goals),
                    OwnGoals = group.Sum(line => line.OwnGoals),
                    Saves = group.Sum(line => line.Saves),
                    YellowCards = group.Sum(line => line.YellowCards),
                    RedCards = group.Sum(line => line.RedCards),
                    Injuries = group.Sum(line => line.WasInjured ? 1 : 0),
                    MatchesMissed = group.Sum(line => line.InjuredOff ? 1 : 0)
                }
            })
            .ToListAsync(cancellationToken);

    /// <summary>
    /// One player's career, split by the club he was wearing when he did each of these things.
    ///
    /// The split is on the match line's own club, and the season count is a distinct count
    /// rather than a sum: a man who played four seasons for one club has four seasons there and
    /// not sixteen, and a career read as a number of games cannot say how long a club had him.
    /// </summary>
    public async Task<IReadOnlyList<PlayerClubCareerLine>> ListClubCareerLinesAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.MatchPlayerStatistics
            .AsNoTracking()
            .Where(line => line.PlayerId == playerId)
            .GroupBy(line => line.TeamId)
            .Select(group => new
            {
                TeamId = group.Key,
                Seasons = group.Where(line => line.SeasonId != null)
                    .Select(line => line.SeasonId!.Value)
                    .Distinct()
                    .Count(),
                Line = new PlayerCareerLine
                {
                    Appearances = group.Count(),
                    Started = group.Sum(line => line.Started ? 1 : 0),
                    CameOn = group.Sum(line => line.CameOn ? 1 : 0),
                    BenchUnused = group.Sum(line => line.WasOnBenchUnused ? 1 : 0),
                    Goals = group.Sum(line => line.Goals),
                    OwnGoals = group.Sum(line => line.OwnGoals),
                    Saves = group.Sum(line => line.Saves),
                    YellowCards = group.Sum(line => line.YellowCards),
                    RedCards = group.Sum(line => line.RedCards),
                    Injuries = group.Sum(line => line.WasInjured ? 1 : 0),
                    MatchesMissed = group.Sum(line => line.InjuredOff ? 1 : 0)
                }
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new PlayerClubCareerLine
            {
                TeamId = row.TeamId,
                Seasons = row.Seasons,
                Total = row.Line
            })
            .ToList();
    }

    public async Task AddSeasonStateAsync(
        PlayerSeasonState state,
        CancellationToken cancellationToken = default) =>
        await _dbContext.PlayerSeasonStates.AddAsync(state, cancellationToken);
}
