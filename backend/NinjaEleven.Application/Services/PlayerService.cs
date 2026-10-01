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
            Age = player.Age,
            Speed = player.Speed,
            Accuracy = player.Accuracy,
            Dribbling = player.Dribbling,
            Heading = player.Heading,
            Strength = player.Strength,
            GoalkeeperPower = player.GoalkeeperPower,
            Reflexes = player.Reflexes,
            Stamina = player.Stamina,
            Potential = player.Potential,
            Face = player.Face
        };

        // A card without a season is not a card without a price. A player is worth what he is
        // worth today, and the season that says so is the one being played, so a caller that
        // asks for a player rather than for a player's season gets the current one. The season
        // asked for still decides the season's own line of numbers; this only decides the
        // season the money is read in.
        var moneySeasonId = seasonId ?? (await _seasonRepository.GetCurrentAsync(cancellationToken))?.Id;

        if (moneySeasonId is { } season)
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

                // The price is asked for with the season the caller asked about, because the
                // season state is where a player's knocks and sendings-off live: a price read
                // from last season's knocks would be a different man's price, and a profile
                // without a season has no knocks to read and no price to quote.
                TeamMembership? contract = null;
                if (state.TeamId is not null)
                {
                    contract = await ResolveContractAsync(
                        player.Id,
                        state.TeamId.Value,
                        state.SeasonId,
                        cancellationToken);
                }

                profile.MarketValue = PlayerValuation.MarketValue(player, state);
                profile.Salary = PlayerValuation.SeasonWage(player, state);

                // How much of the contract is left is read against the same season, so the
                // card and the squad table cannot say a man is two seasons from freedom in
                // one place and one season from it in the other.
                var contractSeason = await _seasonRepository.GetAsync(state.SeasonId, cancellationToken);
                if (contract is not null && contractSeason is not null)
                {
                    profile.ShirtNumber = contract.ShirtNumber;
                    profile.ContractSeasons = contract.ContractSeasons;
                    profile.SeasonsLeft = contract.SeasonsLeft(contractSeason.Number);
                    profile.IsInLastSeason = contract.IsInHisLastSeason(contractSeason.Number);
                    profile.AskingPrice = PlayerValuation.AskingPrice(
                        profile.MarketValue,
                        profile.IsInLastSeason);
                }
            }
        }

        if (profile.TeamId is { } teamId)
        {
            var team = await _teamRepository.GetAsync(teamId, cancellationToken);
            profile.TeamName = team?.Name ?? string.Empty;
            profile.TeamPrimaryColor = team?.PrimaryColor;
            profile.TeamSecondaryColor = team?.SecondaryColor;
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
            OpponentTeamId = line.OpponentTeamId,
            OpponentTeamPrimaryColor = line.OpponentTeamPrimaryColor,
            OpponentTeamSecondaryColor = line.OpponentTeamSecondaryColor,
            HomeGoals = line.HomeGoals,
            AwayGoals = line.AwayGoals,
            RoundNumber = line.RoundNumber,
            TeamName = line.TeamName,
            SeasonName = line.SeasonName,
            CompetitionName = line.CompetitionName,
            PhaseName = line.PhaseName,
            StadiumName = line.StadiumName,
            Attendance = line.Attendance
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

    /// <summary>
    /// How many seasons the club has him signed for, which is what his wage is a share of.
    ///
    /// A player with no active membership in that club and season is read at the default
    /// contract rather than at zero: he is on a squad, a squad is paid, and a profile that
    /// showed a wage of nothing for a player in the eleven would be wrong in the way a
    /// manager notices first.
    /// </summary>
    private async Task<TeamMembership?> ResolveContractAsync(
        Guid playerId,
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var memberships = await _teamRepository.GetSquadAsync(teamId, seasonId, cancellationToken);

        return memberships.FirstOrDefault(membership => membership.PlayerId == playerId);
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

        var season = await _seasonRepository.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        // The contracts come from the memberships rather than from the season states, because
        // a season state is what a player did and a contract is what the club owes: a player
        // whose contract has ended keeps his goals in the state and is not on the wage bill.
        var contracts = await _teamRepository.GetSquadAsync(teamId, seasonId, cancellationToken);
        var contractByPlayer = contracts.ToDictionary(
            membership => membership.PlayerId,
            membership => membership);

        var seasonStates = await _playerRepository.ListSeasonStatesAsync(seasonId, teamId, cancellationToken);
        var squad = new List<SquadPlayer>(seasonStates.Count);

        foreach (var seasonState in seasonStates)
        {
            var player = await _playerRepository.GetAsync(seasonState.PlayerId, cancellationToken);
            if (player is not null)
            {
                squad.Add(new SquadPlayer
                {
                    Player = player,
                    SeasonState = seasonState,
                    Membership = contractByPlayer.GetValueOrDefault(player.Id),
                    Season = season
                });
            }
        }

        // A squad is read top to bottom, so it comes back the way a manager reads it:
        // goalkeepers, defenders, midfielders, attackers, and by name inside each group.
        return squad
            .Apply(player => player.Player.Position, player => player.Player.Name)
            .ToList();
    }
}
