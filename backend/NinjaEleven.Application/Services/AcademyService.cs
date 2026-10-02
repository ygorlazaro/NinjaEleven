using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Manages the youth academy: listing a club's academy players, promoting them to the
/// first team, and evolving their attributes while they develop.
/// </summary>
public class AcademyService
{
    private readonly IPlayerRepository _players;
    private readonly ITeamRepository _teams;
    private readonly ISeasonRepository _seasons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FinanceService _finance;
    private readonly InboxService _inbox;
    private readonly ILogger<AcademyService> _logger;

    public AcademyService(
        IPlayerRepository players,
        ITeamRepository teams,
        ISeasonRepository seasons,
        IUnitOfWork unitOfWork,
        FinanceService finance,
        InboxService inbox,
        ILogger<AcademyService> logger)
    {
        _players = players;
        _teams = teams;
        _seasons = seasons;
        _unitOfWork = unitOfWork;
        _finance = finance;
        _inbox = inbox;
        _logger = logger;
    }

    /// <summary>
    /// The full list of a club's academy players for a season, ready for the Base screen.
    /// Each academy player is paired with his current player identity so the screen can
    /// render the face, the attributes, and the potential reading.
    /// </summary>
    public async Task<IReadOnlyList<AcademyPlayer>> ListAcademyPlayersAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var states = await _players.ListAcademyPlayersAsync(seasonId, teamId, cancellationToken);

        if (states.Count == 0)
        {
            return Array.Empty<AcademyPlayer>();
        }

        var playerIds = states.Select(s => s.PlayerId).ToList();
        var playerDict = (await _players.ListByIdsAsync(playerIds, cancellationToken))
            .ToDictionary(p => p.Id);

        var result = new List<AcademyPlayer>(states.Count);

        foreach (var state in states)
        {
            if (!playerDict.TryGetValue(state.PlayerId, out var player))
            {
                continue;
            }

            var overall = DevelopmentRules.Overall(player);
            var potential = player.Potential;
            var developmentRoom = Math.Max(0.0, potential - overall);

            result.Add(new AcademyPlayer
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
                Potential = potential,
                OverallRating = (int)Math.Round(overall),
                Stars = PlayerRating.CalculateStars(player),
                DevelopmentRoom = (int)Math.Round(developmentRoom),
                Energy = state.Energy,
                IsAvailable = state.IsAvailable,
                Injury = state.Injury.ToString(),
                InjuryMatchesRemaining = state.InjuryMatchesRemaining,
                // Carried so the row can say which of the two absences it is. The screen used
                // to read "not available" and print "Suspenso" beside it, which sent a manager
                // looking for a red card a youth player had never been shown.
                SuspensionMatches = state.SuspensionMatches,
                Retiring = state.Retiring
            });
        }

        return result
            .OrderBy(p => p.Position)
            .ThenBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Promotes an academy player to the first team. The player is given a one-season contract
    /// at the minimum wage for his current profile, and the academy flag is lifted from his
    /// season state. Promotion can happen at any time, even if the player is injured.
    /// </summary>
    public async Task<PromoteAcademyResult> PromoteAcademyPlayerAsync(
        Guid teamId,
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        // The manager can only act for his own club.
        var team = await _teams.GetAsync(teamId, cancellationToken);
        if (team is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var state = await _players.GetSeasonStateForUpdateAsync(playerId, seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        if (!state.IsAcademyPlayer || state.TeamId != teamId)
        {
            throw new DomainValidationException(
                "NotAnAcademyPlayer",
                $"Player {player.Name} is not an academy player of club {team.Name}.");
        }

        // The player is given a one-season contract at the wage the current profile commands.
        var wage = PlayerValuation.SeasonWage(player, state);
        var season = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var membership = TeamMembership.Create(
            playerId,
            teamId,
            DateOnly.FromDateTime(DateTime.Today),
            contractSeasons: FinanceRules.DefaultContractSeasons,
            startSeasonNumber: season.Number,
            shirtNumber: await DealShirtNumberAsync(teamId, cancellationToken),
            wage: wage);

        await _teams.AddMembershipAsync(membership, cancellationToken);
        state.PromoteFromAcademy();
        _players.UpdateSeasonState(state);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Promoted academy player {PlayerName} to the first team of {ClubName}.",
            player.Name, team.Name);

        return new PromoteAcademyResult
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            TeamId = team.Id,
            TeamName = team.Name,
            ShirtNumber = membership.ShirtNumber,
            Salary = membership.Wage,
            SeasonsLeft = membership.SeasonsLeft(season.Number)
        };
    }

    /// <summary>
    /// Evolves academy players each round: 10% of them receive a small attribute improvement.
    /// Injured players do not evolve, and players whose attributes are already at their potential
    /// are skipped.
    /// </summary>
    public async Task<AcademyEvolutionResult> EvolveAcademyAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var states = await _players.ListAllSeasonStatesAsync(seasonId, cancellationToken);

        var academyStates = states
            .Where(s => s.IsAcademyPlayer && s.TeamId.HasValue)
            .ToList();

        if (academyStates.Count == 0)
        {
            return new AcademyEvolutionResult { Evolved = 0, Total = 0 };
        }

        var playerIds = academyStates.Select(s => s.PlayerId).ToList();
        var playerDict = (await _players.ListByIdsAsync(playerIds, cancellationToken))
            .ToDictionary(p => p.Id);
        var random = Random.Shared;

        var eligible = new List<(Player player, PlayerSeasonState state)>();

        foreach (var state in academyStates)
        {
            if (!playerDict.TryGetValue(state.PlayerId, out var player))
            {
                continue;
            }

            // Injured players do not evolve.
            if (!state.IsAvailable)
            {
                continue;
            }

            // Players at their potential ceiling are done growing.
            if (DevelopmentRules.Headroom(player) <= 0)
            {
                continue;
            }

            eligible.Add((player, state));
        }

        if (eligible.Count == 0)
        {
            return new AcademyEvolutionResult { Evolved = 0, Total = academyStates.Count };
        }

        var countToEvolve = Math.Max(1, (int)Math.Round(eligible.Count * AcademyRules.EvolutionRate));
        var shuffled = eligible.OrderBy(_ => random.Next()).Take(countToEvolve).ToList();

        foreach (var (player, state) in shuffled)
        {
            var attributes = DevelopmentRules.GrowthWeights(player).ToList();
            if (attributes.Count == 0)
            {
                continue;
            }

            // Pick one attribute to improve, weighted by growth weight.
            var totalWeight = attributes.Sum(a => a.Weight);
            var roll = random.NextDouble() * totalWeight;
            double cumulative = 0;
            var chosen = attributes.Last().Attribute;

            foreach (var (attribute, weight) in attributes)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    chosen = attribute;
                    break;
                }
            }

            var before = player.Get(chosen);
            var growth = random.Next(1, AcademyRules.MaxEvolutionPerAttribute + 1);

            // Don't grow beyond potential.
            if (before + growth > player.Potential)
            {
                growth = player.Potential - before;
            }

            if (growth <= 0)
            {
                continue;
            }

            player.Set(chosen, before + growth);
            _players.Update(player);

            // Record the evolution in the inbox as a message.
            if (state.TeamId is { } clubId)
            {
                var clubs = await _teams.ListByIdsAsync(
                    new[] { clubId }, cancellationToken);
                var clubName = clubs.FirstOrDefault()?.Name ?? "o clube";

                await _inbox.PostAcademyEvolutionAsync(new AcademyEvolutionFacts
                {
                    RecipientTeamId = clubId,
                    ClubName = clubName,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    Attribute = chosen.ToString(),
                    Gained = growth,
                    Before = before,
                    After = before + growth
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcademyEvolutionResult
        {
            Evolved = shuffled.Count,
            Total = academyStates.Count
        };
    }

    /// <summary>
    /// The lowest free shirt number, goalkeeper-aware, matching the contract creation rule.
    /// </summary>
    private async Task<int> DealShirtNumberAsync(Guid teamId, CancellationToken ct)
    {
        var squad = await _teams.GetLiveContractsAsync(teamId, ct);
        var taken = squad
            .Where(m => m.ShirtNumber.HasValue)
            .Select(m => m.ShirtNumber!.Value)
            .ToHashSet();

        if (taken.Count == 0)
        {
            return 1;
        }

        for (var i = 1; i <= 99; i++)
        {
            if (!taken.Contains(i))
            {
                return i;
            }
        }

        // No gap found — shouldn't happen in practice.
        return taken.Max() + 1;
    }

    /// <summary>
    /// Places a player on the transfer list, making him visible to buying clubs.
    /// </summary>
    public async Task<TransferListResult> PutOnTransferListAsync(
        Guid teamId,
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var state = await _players.GetSeasonStateForUpdateAsync(playerId, seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        if (state.TeamId != teamId || state.IsAcademyPlayer)
        {
            throw new DomainValidationException(
                "CannotListPlayer",
                state.IsAcademyPlayer
                    ? "Academy players cannot be listed for transfer."
                    : $"Player {player.Name} is not a squad player of {team.Name}.");
        }

        var previous = state.OnTransferList;
        state.PutOnTransferList();
        _players.UpdateSeasonState(state);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!previous)
        {
            await _inbox.PostTransferListedAsync(new TransferListedFacts
            {
                RecipientTeamId = team.Id,
                ClubName = team.Name,
                PlayerId = player.Id,
                PlayerName = player.Name
            }, cancellationToken);
        }

        return new TransferListResult
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            TeamId = team.Id,
            TeamName = team.Name,
            OnTransferList = true
        };
    }

    /// <summary>
    /// Takes a player off the transfer list.
    /// </summary>
    public async Task<TransferListResult> TakeOffTransferListAsync(
        Guid teamId,
        Guid playerId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var state = await _players.GetSeasonStateForUpdateAsync(playerId, seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        if (state.TeamId != teamId || state.IsAcademyPlayer)
        {
            throw new DomainValidationException(
                "CannotUnlistPlayer",
                state.IsAcademyPlayer
                    ? "Academy players are not on the transfer list."
                    : $"Player {player.Name} is not a squad player of {team.Name}.");
        }

        state.TakeOffTransferList();
        _players.UpdateSeasonState(state);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TransferListResult
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            TeamId = team.Id,
            TeamName = team.Name,
            OnTransferList = false
        };
    }
}
