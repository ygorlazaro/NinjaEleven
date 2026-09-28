using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Services;

/// <summary>
/// What a competition pays its artilharia, and who is paid it.
/// </summary>
/// <remarks>
/// <para>
/// It is a reading of the domain's rules rather than a second copy of them: the order of the
/// table is <see cref="TopScorerTable"/>, the shares are <see cref="PrizeRules"/>, and what is
/// left here is the walking — which edition, whose goals, and which title the money is a share
/// of.
/// </para>
/// <para>
/// <b>Every competition's artilharia is a share of its own champion's prize.</b> A division's is
/// a share of that division's title and the cup's is a share of the cup's, so a third-division
/// forward who tops the cup's scoring is paid what a first-division one is paid for the same
/// goals: the cup runs across the pyramid, and the prize for scoring in it is the cup's. Reading
/// a cup striker's share out of his own club's division would price the same goals three
/// different ways according to the shirt he was wearing.
/// </para>
/// <para>
/// <b>It is new money in both.</b> A club that wins a competition with its own top scorer on the
/// sheet is paid the title and the artilharia, because winning it and scoring in it are two
/// things the club did.
/// </para>
/// </remarks>
public class ScorerPrizeService
{
    private readonly ICompetitionRepository _competitions;
    private readonly IPlayerRepository _players;
    private readonly ITeamRepository _teams;
    private readonly FinanceService _finance;
    private readonly ILogger<ScorerPrizeService> _logger;

    public ScorerPrizeService(
        ICompetitionRepository competitions,
        IPlayerRepository players,
        ITeamRepository teams,
        FinanceService finance,
        ILogger<ScorerPrizeService> logger)
    {
        _competitions = competitions;
        _players = players;
        _teams = teams;
        _finance = finance;
        _logger = logger;
    }

    /// <summary>
    /// Pays an edition's artilharia to the clubs whose players won it, and reports how many
    /// cheques were written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is called when the edition is <b>finished</b> — a division's last window closing,
    /// the cup's final going to penalties — rather than when the season is closed, because that
    /// is the moment the football that the artilharia is an artilharia of has all been played.
    /// A manager whose division ended in November should be paid in November: a season close
    /// pays out everything at once, which means a cup that was decided in June leaves a club
    /// waiting three months for a cheque for a cup it had already won.
    /// </para>
    /// <para>
    /// The money goes to the club the goals were scored for and not to the player, because the
    /// player is not a bank. The reference is the player rather than the place, so a pay-out
    /// asked for twice pays once — the call is made from the finish of a window, and a window
    /// can be closed twice (once when its last match ends, once when a process that was down
    /// over the weekend comes back) without a striker being paid two cheques for one season.
    /// </para>
    /// </remarks>
    public async Task<int> PayAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var prizes = await GetAsync(competitionSeasonId, cancellationToken);
        var paid = 0;

        foreach (var winner in prizes.Winners.Where(winner => winner.Amount is > 0))
        {
            var line = await _finance.RecordPrizeAsync(
                winner.TeamId,
                prizes.SeasonId,
                FinanceMovementKind.PrizeMoney,
                $"Artilharia {prizes.CompetitionName} — {PlaceName(winner.PrizeSlot)}",
                winner.Amount!.Value,
                $"artilharia:{competitionSeasonId}:{winner.PlayerId}",
                cancellationToken);

            if (line is not null)
            {
                paid++;
            }
        }

        if (prizes.Winners.Any(winner => winner.Amount is null))
        {
            _logger.LogWarning(
                "The artilharia of {Competition} has a scorer whose club is in no division of the season, so his share of a champion's prize cannot be paid.",
                prizes.CompetitionName);
        }

        if (paid > 0)
        {
            _logger.LogInformation(
                "Paid {Count} artilharia prizes of {Competition}.",
                paid,
                prizes.CompetitionName);
        }

        return paid;
    }

    /// <summary>
    /// What a place is called on a cheque. A tie is written out, because two men who were both
    /// second are both paid the second prize and a book that says only "2º" for both of them
    /// cannot be checked against anything.
    /// </summary>
    private static string PlaceName(int slot) => slot switch
    {
        1 => "1º lugar",
        2 => "2º lugar",
        3 => "3º lugar",
        _ => $"{slot}º lugar"
    };

    /// <summary>
    /// The prize list of one edition: the three places, who holds them, and what each club is
    /// paid. The shares come back even when nobody has scored yet, because a legend of a prize
    /// that has not been won is still the rule a manager is reading.
    /// </summary>
    /// <param name="competitionSeasonId">The edition: a division's table or the cup.</param>
    public async Task<TopScorerPrizeList> GetAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var view = await _competitions.GetSeasonViewByIdAsync(competitionSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);

        var rates = Enumerable.Range(1, PrizeRules.TopScorerPlaces)
            .Select(place => new TopScorerRate(place, PrizeRules.TopScorerRate(place)))
            .ToList();

        // The base is the title of the competition the goals were scored in, and it is read
        // before anybody has scored, so a season's opening day already says what a goal is worth
        // in it. A division's title is shared across the clubs of that division; a cup's is the
        // cup's own, and it is the same purse whether the striker who topped the scoring played
        // the final for a first-division club or a third-division one.
        var baseAmount = await ReadTheChampionPrizeAsync(view, cancellationToken);

        var lines = await _players.ListEditionScorerLinesAsync(competitionSeasonId, cancellationToken);
        if (lines.Count == 0)
        {
            return new TopScorerPrizeList
            {
                CompetitionSeasonId = view.Id,
                SeasonId = view.SeasonId,
                CompetitionName = view.Name,
                Tier = view.Tier,
                BaseAmount = baseAmount,
                Rates = rates
            };
        }

        // A player who changed clubs inside the season has a line per club, and the artilharia
        // is one line per man. He is counted once, under the club he scored for most in this
        // competition, and the games and the cards come off that same line — so the chain that
        // decides who is paid is settled on one club's football.
        var byPlayer = lines
            .GroupBy(line => line.PlayerId)
            .Select(group => group.OrderByDescending(line => line.Goals).First())
            .ToList();

        var players = (await _players.ListAsync(cancellationToken))
            .Where(player => byPlayer.Any(line => line.PlayerId == player.Id))
            .ToDictionary(player => player.Id);
        var clubs = await _teams.ListByIdsAsync(
            byPlayer.Select(line => line.TeamId), cancellationToken);
        var clubById = clubs.ToDictionary(club => club.Id);

        var standings = byPlayer
            .Where(line => players.ContainsKey(line.PlayerId))
            .Select(line => ScorerStanding.From(
                line.PlayerId,
                line.Goals,
                line.Appearances,
                line.YellowCards,
                line.RedCards,
                players[line.PlayerId].BirthDate))
            // A level pair is printed in the order the two names are. The names are the last
            // thing a reader has, not a rule that outranks a season's football.
            .OrderBy(line => players[line.PlayerId].Name, StringComparer.Ordinal)
            .ToList();

        var linesById = byPlayer.ToDictionary(line => line.PlayerId);

        var winners = TopScorerTable.Rank(standings)
            .Where(line => line.PrizeSlot > 0)
            .Select(line =>
            {
                var source = linesById[line.PlayerId];
                var player = players[line.PlayerId];
                var club = clubById.GetValueOrDefault(source.TeamId);

                return new TopScorerPrizeRow
                {
                    PlayerId = line.PlayerId,
                    PlayerName = player.Name,
                    Age = player.CalculateAge(),
                    TeamId = source.TeamId,
                    TeamName = club?.Name,
                    TeamPrimaryColor = club?.PrimaryColor,
                    TeamSecondaryColor = club?.SecondaryColor,
                    Position = line.Position,
                    PrizeSlot = line.PrizeSlot,
                    TiedWith = line.TiedWith,
                    Goals = source.Goals,
                    Appearances = source.Appearances,
                    CardPoints = source.CardPoints,
                    Rate = PrizeRules.TopScorerRate(line.PrizeSlot),
                    Amount = baseAmount is { } base_ ? PrizeRules.TopScorerShareOf(line.PrizeSlot, base_) : null
                };
            })
            .ToList();

        if (baseAmount is null)
        {
            _logger.LogWarning(
                "The artilharia of {Competition} has no champion's prize to take a share of, so its scorers cannot be priced.",
                view.Name);
        }

        return new TopScorerPrizeList
        {
            CompetitionSeasonId = view.Id,
            SeasonId = view.SeasonId,
            CompetitionName = view.Name,
            Tier = view.Tier,
            BaseAmount = baseAmount,
            Rates = rates,
            Winners = winners
        };
    }

    /// <summary>
    /// What the champion of this edition is paid, which is what its artilharia is a share of.
    /// </summary>
    /// <remarks>
    /// A division's title is a share of that division's purse, so the clubs of the division are
    /// what decide the figure and they are read from the enrolment rather than assumed. A cup
    /// pays its champion one cheque that is nobody's share of anything, so it is the domain's
    /// number and needs nobody's answer. An edition that is neither has no title and therefore
    /// no artilharia to price.
    /// </remarks>
    private async Task<decimal?> ReadTheChampionPrizeAsync(
        CompetitionSeasonView view,
        CancellationToken cancellationToken)
    {
        if (view.Tier is not { } tier)
        {
            return view.Type == CompetitionType.Cup ? PrizeRules.CupChampionPrize : null;
        }

        var clubs = (await _competitions.ListParticipantsAsync(view.Id, cancellationToken)).Count;

        return clubs > 0
            ? PrizeRules.ChampionshipPrize(1, clubs, PrizeRules.PurseForTier(tier))
            : null;
    }
}
