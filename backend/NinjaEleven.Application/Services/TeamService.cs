using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Teams;

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

    public TeamService(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository,
        IMatchRepository matchRepository)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
        _matchRepository = matchRepository;
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

        foreach (var line in lines)
        {
            var player = await _playerRepository.GetAsync(line.PlayerId, cancellationToken);
            if (player is null)
            {
                continue;
            }

            scorers.Add(new ClubScorerRow
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Age = player.CalculateAge(),
                Goals = line.Goals,
                OwnGoals = line.OwnGoals,
                Started = line.Started,
                CameOn = line.CameOn,
                IsStillAtClub = stillHere.Contains(player.Id)
            });
        }

        var ordered = scorers
            .OrderByDescending(row => row.Goals)
            .ThenBy(row => row.Started + row.CameOn)
            .ThenBy(row => row.PlayerName, StringComparer.Ordinal)
            .Take(ScorerRules.Clamp(topN))
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var row = ordered[index];
            var appearances = row.Started + row.CameOn;

            row.Position = index + 1;
            row.GoalsPerAppearance = appearances > 0
                ? Math.Round((double)row.Goals / appearances, 2)
                : null;
        }

        return ordered;
    }
}
