using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Application.Abstractions;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Use cases around clubs. It coordinates the repositories and decides what is valid;
/// it never writes SQL and it never returns entities straight to the transport layer.
/// </summary>
public class TeamService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TeamService(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository,
        IMatchRepository matchRepository,
        IUnitOfWork unitOfWork)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
        _matchRepository = matchRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Marks the club a manager is running, and unmarks whichever one was marked before.
    ///
    /// Almost the whole game treats every club the same, because the engine plays the club
    /// nobody is watching and a manager's club is not special to a fixture. It is special to
    /// exactly one thing — it answers for itself — and the market is the only reader of the
    /// mark. Without it the clubs nobody is watching would settle a man's transfer by a roll of
    /// the dice while the one club a person is playing was treated the same, which is the one
    /// decision in the game that has to belong to a person.
    /// </summary>
    public async Task<Team> TakeOverAsManagerClubAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var club = await _teamRepository.MarkAsManagerClubAsync(teamId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return club;
    }

    /// <summary>
    /// The last matches a club has finished, newest first, for the form guide on its card.
    /// A club that does not exist has no form, and saying so beats drawing an empty list
    /// for an id that was never one.
    /// </summary>
    public async Task<IReadOnlyList<TeamMatchRecord>> GetRecentMatchesAsync(
        Guid teamId,
        int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        return await _matchRepository.GetTeamHistoryAsync(teamId, TeamHistoryRules.Clamp(limit), cancellationToken);
    }

    /// <summary>
    /// Head-to-head matches between two clubs, newest first. A club's history against a
    /// specific rival is a different question from its general run, and the screen that asks
    /// for it has a different purpose: it is the story of this particular rivalry.
    /// </summary>
    public async Task<IReadOnlyList<TeamMatchRecord>> GetHeadToHeadAsync(
        Guid teamId,
        Guid opponentId,
        int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        if (await _teamRepository.GetAsync(opponentId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), opponentId);
        }

        return await _matchRepository.GetHeadToHeadAsync(teamId, opponentId, TeamHistoryRules.Clamp(limit), cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _teamRepository.ListAsync(cancellationToken);

    public async Task<Team> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _teamRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), id);

    /// <summary>
    /// Updates the name of a club that the manager has taken charge of.
    /// </summary>
    public async Task<Team> UpdateNameAsync(
        Guid teamId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        team.SetName(name);
        _teamRepository.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return team;
    }

    /// <summary>
    /// Updates the kit colours of a club that the manager has taken charge of.
    /// </summary>
    public async Task<Team> UpdateColorsAsync(
        Guid teamId,
        string primaryColor,
        string secondaryColor,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        team.SetPrimaryColor(primaryColor);
        team.SetSecondaryColor(secondaryColor);
        _teamRepository.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return team;
    }

    public async Task<IReadOnlyList<SquadPlayer>> GetSquadAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        // The season is not only checked for existence: a contract's clock is read against
        // the calendar, so how much of a player's contract is left is a question about the
        // season the squad is being asked for and not a number the table may work out.
        var season = await _seasonRepository.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var memberships = await _teamRepository.GetSquadAsync(teamId, seasonId, cancellationToken);
        var squad = new List<SquadPlayer>(memberships.Count);

        foreach (var membership in memberships)
        {
            var player = await _playerRepository.GetAsync(membership.PlayerId, cancellationToken);
            if (player is null)
            {
                continue;
            }

            var seasonState = await _playerRepository.GetSeasonStateAsync(player.Id, seasonId, cancellationToken);
            if (seasonState is null)
            {
                continue;
            }

            squad.Add(new SquadPlayer
            {
                Player = player,
                SeasonState = seasonState,
                Membership = membership,
                Season = season
            });
        }

        return squad;
    }

    /// <summary>
    /// The club's scorers of a season, every man of the club who scored, and whether he is
    /// still there.
    ///
    /// Three things are decided here rather than left to the client, because all three are
    /// questions about the world and not about a table:
    ///
    /// - **What counts as a scorer.** Only the lines, and only goals: an own goal is a
    ///   defender's error and is carried apart. A table that added it would put a centre-back
    ///   on the list for the mistakes he made.
    /// - **Who is still at the club.** A membership with no end date. A scorer who has left is
    ///   kept in the answer with the flag against him, because a club's all-time scorers list
    ///   is the one page that must never lose a name — and a screen that only ever showed the
    ///   men under contract would quietly rewrite the club's history every time a window
    ///   opened.
    /// - **The order.** Goals first, then the fewest games for them, then the name. A striker
    ///   with eight goals in ten matches and one with eight in twenty are not the same
    ///   scorer, and the table that ranked them level would be hiding the whole difference.
    /// </summary>
    public async Task<IReadOnlyList<ClubScorerRow>> GetScorersAsync(
        Guid teamId,
        Guid seasonId,
        CompetitionType? competitionType = null,
        int topN = ScorerRules.DefaultScorers,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        var lines = await _playerRepository.ListClubScorerLinesAsync(
            teamId, seasonId, competitionType, cancellationToken);

        if (lines.Count == 0)
        {
            return Array.Empty<ClubScorerRow>();
        }

        var live = await _teamRepository.GetLiveContractsAsync(teamId, cancellationToken);
        var stillHere = live.Select(membership => membership.PlayerId).ToHashSet();
        var scorers = new List<ClubScorerRow>(lines.Count);
        var ageOf = new Dictionary<Guid, int>();

        foreach (var line in lines)
        {
            var player = await _playerRepository.GetAsync(line.PlayerId, cancellationToken);
            if (player is null)
            {
                continue;
            }

            ageOf[player.Id] = player.Age;
            scorers.Add(new ClubScorerRow
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Age = player.Age,
                Goals = line.Goals,
                OwnGoals = line.OwnGoals,
                Started = line.Started,
                CameOn = line.CameOn,
                YellowCards = line.YellowCards,
                RedCards = line.RedCards,
                IsStillAtClub = stillHere.Contains(player.Id)
            });
        }

        // The chain is the domain's and the same one a division's or the cup's artilharia is
        // settled on: goals, then fewest games, then fewest cards by weight, then oldest. The
        // club's own list is not a different competition with a different order — a striker who
        // is level on goals with a team-mate is level with him for the same reasons on the club's
        // page as he is on the league's.
        //
        // The name is the print order of a pair the chain could not separate, and nothing more:
        // the positions come off the chain, so two men level on the whole of it are both first
        // rather than being told apart by the alphabet.
        var byId = scorers.ToDictionary(row => row.PlayerId);
        var standings = scorers
            .OrderBy(row => row.PlayerName, StringComparer.Ordinal)
            .Select(row => ScorerStanding.From(
                row.PlayerId,
                row.Goals,
                row.Started + row.CameOn,
                row.YellowCards,
                row.RedCards,
                ageOf.TryGetValue(row.PlayerId, out var age) ? age : null))
            .ToList();

        var ordered = TopScorerTable.Rank(standings)
            .Take(ScorerRules.Clamp(topN))
            .Select(line =>
            {
                var row = byId[line.PlayerId];
                row.Position = line.Position;
                row.TiedWith = line.TiedWith;
                row.GoalsPerAppearance = row.Started + row.CameOn > 0
                    ? Math.Round((double)row.Goals / (row.Started + row.CameOn), 2)
                    : null;

                return row;
            })
            .ToList();

        return ordered;
    }
}
