using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// What opening a season's rosters did, so a caller can be told rather than left to look.
/// </summary>
/// <param name="SeasonId">The season that was opened.</param>
/// <param name="SquadsCreated">How many players were given a season state.</param>
/// <param name="Announced">How many players the retirement rule announced in this season.</param>
/// <param name="Retired">How many players left the game on the season's last day.</param>
/// <param name="TransfersLinked">How many waiting deals were pointed at the new season.</param>
public record SeasonRosterResult(
    Guid SeasonId,
    int SquadsCreated,
    int Announced,
    int Retired,
    int TransfersLinked);

/// <summary>
/// Opens the rosters of a season: ages the world a year, announces the retirements the rule
/// says, gives every player a state for the season, carries out the retirements that were
/// announced in the season before, and points the deals that were waiting for this season at
/// its row.
///
/// A season state is a player's season — his club, his energy, what he has done — and the man
/// himself is the thing that lasts. So a new season does not create players, it creates
/// seasons of players: every one of them gets a state again, in his club if he still has one and
/// in no club if he does not. A world that skipped this would open a second season in which no
/// player had played for it: no squad, no goals, no market, and a transfer arriving into a
/// season with nothing behind it.
///
/// The order of the three steps is the whole of it, and it is not a style of writing them
/// down. Everybody is a year older first, because the age is the only thing the retirement rule
/// reads: a man who turns thirty-seven in this season has to be announced in this season, and a
/// world that announced its retirements off last season's ages would lose a player a year and
/// keep him a year too long. The announcement is second, for the same reason — it is a fact
/// about the age, not about the man. The states are built third, already carrying the flag.
///
/// The retirements are carried out here because this is the only moment a season can end a
/// contract without anybody deciding anything. A man the rule announced in the first matchday
/// was announced about the end of that season, and the club that carried him for it stops
/// carrying him on the same day the season stops existing — the membership is given an end date
/// and he is a free agent, which is what a retirement is in a game that has no other way for a
/// man to leave.
///
/// A retired man is not deleted and does not go on the market as a signing. He has no season
/// state in the new season, so nothing can propose to sign him, and his history stays where it
/// was: a career that ended is a fact about him, not a gap in the world's memory.
/// </summary>
public class RosterService
{
    /// <summary>
    /// The energy a player starts a season with. Energy is spent over a season and measured
    /// against the calendar's matchdays, so a season opens with everybody fit — a squad that
    /// opened the year tired would open it tired for no reason anybody could point at.
    /// </summary>
    private const int OpeningEnergy = 100;

    private readonly IPlayerRepository _players;
    private readonly ITeamRepository _teams;
    private readonly ITransferRepository _transfers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RosterService> _logger;

    public RosterService(
        IPlayerRepository players,
        ITeamRepository teams,
        ITransferRepository transfers,
        IUnitOfWork unitOfWork,
        ILogger<RosterService> logger)
    {
        _players = players;
        _teams = teams;
        _transfers = transfers;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Opens a season: the world ages, the rule announces, every player is given the season he
    /// is about to play, and the season before's retirements are carried out.
    /// </summary>
    public async Task<SeasonRosterResult> OpenSeasonAsync(
        Season previous,
        Season season,
        CancellationToken cancellationToken = default)
    {
        var retired = await CarryOutTheRetirementsAsync(previous, cancellationToken);

        // Everybody is a year older, and this is the only place it happens. It comes first
        // because everything below reads the age of the world as it now stands, and the rule
        // that decides who is finished is a rule about ages rather than about men.
        var everyone = (await _players.ListAsync(cancellationToken)).ToList();

        foreach (var player in everyone)
        {
            player.AgeUp();
            _players.Update(player);
        }

        var contracts = (await _teams.ListAllContractsAsync(season.Id, cancellationToken))
            .GroupBy(membership => membership.PlayerId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(m => m.StartDate).First());

        var states = new List<PlayerSeasonState>();
        var announced = 0;

        foreach (var player in everyone)
        {
            if (retired.Contains(player.Id))
            {
                continue;
            }

            contracts.TryGetValue(player.Id, out var contract);

            var state = contract is null
                ? PlayerSeasonState.CreateFreeAgent(player.Id, season.Id, OpeningEnergy)
                : PlayerSeasonState.Create(player.Id, season.Id, contract.TeamId, OpeningEnergy);

            // The rule announces, and there is nobody to ask: a man in the band has said his
            // last season was this one, his club has been told the same week every club was,
            // and the flag is what the squad table, the market and the profile read. It is set
            // for free agents too — a man with no club has nobody to tell, and a man of
            // forty-one who is out of contract is finished exactly as much as one who is not.
            if (RetirementRules.CanRetireAt(player.Age))
            {
                state.AnnounceRetirement();
                announced++;
            }

            states.Add(state);
        }

        foreach (var state in states)
        {
            await _players.AddSeasonStateAsync(state, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var linked = await LinkTheWaitingDealsAsync(season, cancellationToken);

        _logger.LogInformation(
            "Opened season {Number}: {Squads} season states, {Announced} retirements announced by " +
            "the rule, {Retired} carried out, {Linked} deals pointed at it.",
            season.Number, states.Count, announced, retired.Count, linked);

        return new SeasonRosterResult(season.Id, states.Count, announced, retired.Count, linked);
    }

    /// <summary>
    /// Ends the contract of every player who announced, in the season he was playing, that he
    /// would stop at its end.
    ///
    /// The announcement lives on the closing season's state and is acted on here and nowhere
    /// else: a flag nobody reads is a decoration, and a flag read in the middle of a season would
    /// take a man's contract away with two matchdays still to play. The membership gets an end
    /// date on the last day of the season he played, which is what makes him a free agent rather
    /// than a player under contract with a club he no longer plays for.
    /// </summary>
    private async Task<HashSet<Guid>> CarryOutTheRetirementsAsync(
        Season previous,
        CancellationToken cancellationToken)
    {
        var announced = (await _players.ListAllSeasonStatesAsync(previous.Id, cancellationToken))
            .Where(state => state.Retiring)
            .Select(state => state.PlayerId)
            .ToHashSet();

        if (announced.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var retired = new HashSet<Guid>();
        var lastDay = previous.EndDate;

        foreach (var team in await _teams.ListAsync(cancellationToken))
        {
            foreach (var membership in await _teams.GetLiveContractsAsync(team.Id, cancellationToken))
            {
                if (!announced.Contains(membership.PlayerId))
                {
                    continue;
                }

                membership.End(lastDay < membership.StartDate ? membership.StartDate : lastDay);
                _teams.UpdateMembership(membership);
                retired.Add(membership.PlayerId);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Count} players retired at the end of season {Number}.", retired.Count, previous.Number);

        return retired;
    }

    /// <summary>
    /// Points the deals that named this season at its row, so a completion sweep can find the
    /// season and a screen can say where a man is going.
    /// </summary>
    private async Task<int> LinkTheWaitingDealsAsync(Season season, CancellationToken cancellationToken)
    {
        var waiting = await _transfers.ListWaitingForSeasonAsync(season.Number, cancellationToken);
        if (waiting.Count == 0)
        {
            return 0;
        }

        foreach (var transfer in waiting)
        {
            transfer.LinkArrivalSeason(season.Id);
            _transfers.Update(transfer);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return waiting.Count;
    }
}
