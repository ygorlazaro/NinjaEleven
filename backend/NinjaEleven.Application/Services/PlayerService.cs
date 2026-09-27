using NinjaEleven.Domain.Enums;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

public class PlayerService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMatchRepository _matchRepository;

    public PlayerService(
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository,
        ISeasonRepository seasonRepository,
        IMatchRepository matchRepository)
    {
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
        _seasonRepository = seasonRepository;
        _matchRepository = matchRepository;
    }

    /// <summary>
    /// Everything a manager wants to know about one player, in one call: who he is, what
    /// he is worth this season, and every match he has a line in.
    ///
    /// It used to be assembled on the screen, from a player record, a season state and a
    /// list the screen did not have. That put the arithmetic — what counts as an appearance,
    /// which matches belong to this season — in the client, where two screens answering the
    /// same question would be free to answer it differently.
    /// </summary>
    public async Task<PlayerProfile> GetProfileAsync(
        Guid playerId,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        var player = await _playerRepository.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var profile = new PlayerProfile
        {
            PlayerId = player.Id,
            Name = player.Name,
            Position = player.Position.ToString(),
            Age = player.CalculateAge(),
            BirthDate = player.BirthDate,
            Speed = player.Speed,
            Accuracy = player.Accuracy,
            Dribbling = player.Dribbling,
            Heading = player.Heading,
            Strength = player.Strength,
            GoalkeeperPower = player.GoalkeeperPower,
            Reflexes = player.Reflexes,
            Face = player.Face
        };

        if (seasonId is { } season)
        {
            var state = await _playerRepository.GetSeasonStateAsync(playerId, season, cancellationToken);
            if (state is not null)
            {
                profile.SeasonId = state.SeasonId;
                profile.TeamId = state.TeamId;
                profile.Energy = state.Energy;
                profile.IsAvailable = state.IsAvailable;
                profile.Injury = state.Injury.ToString();
                profile.InjuryMatchesRemaining = state.InjuryMatchesRemaining;
            }
        }

        if (profile.TeamId is { } teamId)
        {
            var team = await _teamRepository.GetAsync(teamId, cancellationToken);
            profile.TeamName = team?.Name ?? string.Empty;
        }

        var history = await _matchRepository.GetPlayerHistoryAsync(playerId, cancellationToken);
        profile.History = history.Select(line => new PlayerMatchLine
        {
            MatchId = line.MatchId,
            SeasonId = line.SeasonId,
            Started = line.Started,
            CameOn = line.CameOn,
            SubbedOff = line.SubbedOff,
            WasOnBenchUnused = line.WasOnBenchUnused,
            Goals = line.Goals,
            OwnGoals = line.OwnGoals,
            Saves = line.Saves,
            YellowCards = line.YellowCards,
            RedCards = line.RedCards,
            WasInjured = line.WasInjured,
            InjuredOff = line.InjuredOff,
            IsHome = line.IsHome,
            OpponentName = line.OpponentName,
            HomeGoals = line.HomeGoals,
            AwayGoals = line.AwayGoals,
            RoundNumber = line.RoundNumber
        }).ToList();

        // The season's totals come from the same lines as the career's, so the two columns
        // of the table can never disagree: one is the other filtered. The season state
        // carries a goals counter of its own, and it is deliberately not what is shown —
        // a profile that printed two different career goals numbers in two places would be
        // a profile nobody could believe.
        var ofSeason = history
            .Where(line => seasonId is not null && line.SeasonId == seasonId)
            .ToList();

        profile.Total = Sum(history);
        profile.Season = Sum(ofSeason);

        return profile;
    }

    private static PlayerCareerLine Sum(IReadOnlyList<PlayerMatchRecord> lines)
    {
        var total = new PlayerCareerLine { Appearances = lines.Count };

        foreach (var line in lines)
        {
            if (line.Started) total.Started++;
            if (line.CameOn) total.CameOn++;
            if (line.WasOnBenchUnused) total.BenchUnused++;
            total.Goals += line.Goals;
            total.OwnGoals += line.OwnGoals;
            total.Saves += line.Saves;
            total.YellowCards += line.YellowCards;
            total.RedCards += line.RedCards;
            if (line.WasInjured) total.Injuries++;
            if (line.InjuredOff) total.MatchesMissed++;
        }

        return total;
    }

    public async Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _playerRepository.ListAsync(cancellationToken);

    public async Task<Player> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _playerRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Player), id);

    public async Task<PlayerSeasonState> GetSeasonStateAsync(
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _playerRepository.GetAsync(playerId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Player), playerId);
        }

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        return await _playerRepository.GetSeasonStateAsync(playerId, seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);
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

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        var seasonStates = await _playerRepository.ListSeasonStatesAsync(seasonId, teamId, cancellationToken);
        var squad = new List<SquadPlayer>(seasonStates.Count);

        foreach (var seasonState in seasonStates)
        {
            var player = await _playerRepository.GetAsync(seasonState.PlayerId, cancellationToken);
            if (player is not null)
            {
                squad.Add(new SquadPlayer { Player = player, SeasonState = seasonState });
            }
        }

        // A squad is read top to bottom, so it comes back the way a manager reads it:
        // goalkeepers, defenders, midfielders, attackers, and by name inside each group.
        return squad
            .Apply(player => player.Player.Position, player => player.Player.Name)
            .ToList();
    }
}
