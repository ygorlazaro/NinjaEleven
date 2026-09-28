using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// A club's books, and the two moments money moves: the gate on the day a match is played,
/// and the wages a club owes its squad for a season.
///
/// The service decides when a line is written. The line and the balance it leaves behind
/// belong to <see cref="FinanceMovement"/>, and storing them belongs to
/// <see cref="IFinanceRepository"/>; nothing here touches a DbContext.
/// </summary>
public class FinanceService
{
    private readonly IFinanceRepository _finance;
    private readonly ITeamRepository _teams;
    private readonly IPlayerRepository _players;
    private readonly IFixtureRepository _fixtures;
    private readonly IRoundRepository _rounds;
    private readonly IMatchDayRepository _matchDays;
    private readonly ISeasonRepository _seasons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FinanceService> _logger;

    public FinanceService(
        IFinanceRepository finance,
        ITeamRepository teams,
        IPlayerRepository players,
        IFixtureRepository fixtures,
        IRoundRepository rounds,
        IMatchDayRepository matchDays,
        ISeasonRepository seasons,
        IUnitOfWork unitOfWork,
        ILogger<FinanceService> logger)
    {
        _finance = finance;
        _teams = teams;
        _players = players;
        _fixtures = fixtures;
        _rounds = rounds;
        _matchDays = matchDays;
        _seasons = seasons;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// A page of a club's book, newest line first, with the totals of whatever the page was
    /// narrowed to.
    ///
    /// The balance is the club's, and it is not the sum of the page: it comes from the last
    /// line written in the club's whole book. A manager who has chosen a season to read is
    /// reading that season's spending, and a balance that followed the filter would tell him
    /// his club had as much as it had spent, which is the one number in the game that would
    /// be plainly wrong.
    /// </summary>
    public async Task<FinanceLedger> GetLedgerAsync(
        Guid teamId,
        Guid? seasonId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var sizeOfPage = Math.Clamp(pageSize, 1, FinanceRules.MaxPageSize);

        var last = await _finance.GetLastAsync(teamId, cancellationToken);
        var totals = await _finance.TotalsAsync(teamId, seasonId, cancellationToken);
        var total = await _finance.CountAsync(teamId, seasonId, cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)sizeOfPage));

        // A page past the end of a book is answered with the last page rather than with
        // nothing: a manager who was reading the tenth page of a season and then chose a
        // filter should land on the end of what he asked for, not on an empty screen.
        var wanted = Math.Clamp(page, 1, totalPages);

        var lines = await _finance.ListAsync(
            teamId,
            seasonId,
            skip: (wanted - 1) * sizeOfPage,
            take: sizeOfPage,
            cancellationToken);

        var seasonNames = await SeasonNamesAsync(cancellationToken);
        var seasonNumbers = await SeasonNumbersAsync(cancellationToken);

        return new FinanceLedger
        {
            Balance = last?.BalanceAfter ?? 0m,
            Income = totals.Income,
            Expenses = totals.Expenses,
            Page = wanted,
            PageSize = sizeOfPage,
            TotalItems = total,
            TotalPages = totalPages,
            Movements = lines
                .Select(line => new FinanceLedgerLine
                {
                    Id = line.Id,
                    SeasonId = line.SeasonId,
                    SeasonNumber = seasonNumbers.GetValueOrDefault(line.SeasonId),
                    SeasonName = seasonNames.GetValueOrDefault(line.SeasonId, string.Empty),
                    MatchDayNumber = line.MatchDayNumber,
                    Kind = line.Kind,
                    Description = line.Description,
                    Amount = line.Amount,
                    BalanceAfter = line.BalanceAfter,
                    StatesABalance = FinanceMovement.StatesABalance(line.Kind)
                })
                .ToList()
        };
    }

    /// <summary>
    /// Writes what a finished match was worth to the two clubs that played it.
    ///
    /// Both clubs are paid, not only the one a manager follows. A match the engine plays with
    /// nobody watching still took a ticket from somebody, and a world where the thirty-five
    /// clubs the manager does not manage earned nothing would be a table of rich managers
    /// surrounded by bankrupt rivals.
    ///
    /// The two thirds and the one third are the match's own gate, already split by
    /// <see cref="GateReceipt"/>, so the two lines of one match add up to what the stand took
    /// and the books of the whole country add up to every gate of the season.
    /// </summary>
    public async Task RecordMatchGateAsync(
        Guid fixtureId,
        Guid matchId,
        Guid seasonId,
        decimal homeRevenue,
        decimal awayRevenue,
        CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtures.GetAsync(fixtureId, cancellationToken);
        if (fixture is null)
        {
            _logger.LogWarning(
                "Fixture {FixtureId} finished and could not be read back, so its gate was not paid out.",
                fixtureId);

            return;
        }

        var home = await _teams.GetAsync(fixture.HomeTeamId, cancellationToken);
        var away = await _teams.GetAsync(fixture.AwayTeamId, cancellationToken);
        if (home is null || away is null)
        {
            _logger.LogWarning(
                "Fixture {FixtureId} finished with a club that is not in the world, so its gate was not paid out.",
                fixtureId);

            return;
        }

        var day = await ResolveMatchDayForRoundAsync(fixture.RoundId, cancellationToken);

        if (homeRevenue != 0m)
        {
            await AppendAsync(
                home.Id,
                seasonId,
                day,
                FinanceMovementKind.GateRevenue,
                $"Bilheteria contra {away.Name}",
                homeRevenue,
                matchId,
                cancellationToken);
        }

        if (awayRevenue != 0m)
        {
            await AppendAsync(
                away.Id,
                seasonId,
                day,
                FinanceMovementKind.GateRevenue,
                $"Bilheteria contra {home.Name}",
                awayRevenue,
                matchId,
                cancellationToken);
        }
    }

    /// <summary>
    /// Pays a club its squad's wages for one matchday of the league.
    ///
    /// A wage is a hundredth of what a player is worth, so a squad's season bill is decided
    /// by who is in it and not by a number somebody typed into a settings file. The bill is
    /// settled at the end of every match of the championship and only ever that: a season of
    /// twenty-two league matchdays is twenty-two payments of a twenty-second of the bill,
    /// which is the same money in total and a book that says what each matchday cost. A cup
    /// tie and a supercup are not in the league's calendar and are not paid for here.
    ///
    /// The line's existence for that match is what makes it happen once. There is no flag to
    /// keep in step with the book, and a match settled twice — by the tick that blew the
    /// whistle and by one that arrives after it — leaves one line and not two.
    /// </summary>
    public async Task<bool> RecordMatchWagesAsync(
        Guid teamId,
        Guid seasonId,
        int? matchDayNumber,
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        if (await _finance.ExistsForMatchAsync(teamId, matchId, FinanceMovementKind.Wages, cancellationToken))
        {
            return false;
        }

        var team = await _teams.GetAsync(teamId, cancellationToken);
        if (team is null)
        {
            return false;
        }

        var season = await _seasons.GetAsync(seasonId, cancellationToken);
        var squad = await _players.ListSeasonStatesAsync(seasonId, teamId, cancellationToken);
        var contracts = await _teams.GetSquadAsync(teamId, seasonId, cancellationToken);

        if (squad.Count == 0 || contracts.Count == 0)
        {
            return false;
        }

        var contracted = contracts.Select(m => m.PlayerId).ToHashSet();
        var bill = 0m;

        foreach (var state in squad)
        {
            // A player with a season state but no membership is on nobody's books: his
            // contract ran out while the state carrying his goals is still around. A club
            // does not pay a man who is not on its books, whatever the season says he did.
            if (!contracted.Contains(state.PlayerId))
            {
                continue;
            }

            var player = await _players.GetAsync(state.PlayerId, cancellationToken);
            if (player is null)
            {
                continue;
            }

            // The age is today's, the same age the squad screen and the profile show. A wage
            // read from a different day than the price on the same player would be two clubs'
            // books disagreeing about one man.
            bill += PlayerValuation.SeasonWage(player, state);
        }

        if (bill <= 0m)
        {
            return false;
        }

        // The bill of the season, divided by the number of league matchdays it is settled
        // over. A club pays the same money across a season either way, and what a book gains
        // by saying it per match is that a manager can see which matchday cost what.
        var perMatch = decimal.Round(bill / FinanceRules.WageMatchDays, 2, MidpointRounding.AwayFromZero);

        if (perMatch <= 0m)
        {
            return false;
        }

        await AppendAsync(
            teamId,
            seasonId,
            matchDayNumber,
            FinanceMovementKind.Wages,
            $"Folha salarial — rodada {matchDayNumber?.ToString() ?? "·"}",
            -perMatch,
            matchId,
            cancellationToken);

        _logger.LogInformation(
            "Paid {TeamName} {Wages} limos of wages for matchday {MatchDay} of {SeasonName}.",
            team.Name,
            perMatch,
            matchDayNumber,
            season?.Name);

        return true;
    }

    /// <summary>
    /// Opens a season with the balance every club closed the last one with.
    ///
    /// The line is written for every club in the world, not for the manager's: a club
    /// promoted with nine hundred thousand limos brings them with it, and a club relegated
    /// into debt brings the debt.
    ///
    /// A club with no book at all is seeded instead of carried, because a club with neither
    /// money nor history is a club that has to be given something to start on.
    /// </summary>
    public async Task<int> CarryBalancesIntoSeasonAsync(
        Guid previousSeasonId,
        Guid nextSeasonId,
        CancellationToken cancellationToken = default)
    {
        var previousSeason = await _seasons.GetAsync(previousSeasonId, cancellationToken);
        var teams = await _teams.ListAsync(cancellationToken);
        var written = 0;

        foreach (var team in teams)
        {
            if (await _finance.ExistsInSeasonAsync(
                    team.Id,
                    nextSeasonId,
                    FinanceMovementKind.CarryOver,
                    cancellationToken))
            {
                continue;
            }

            var last = await _finance.GetLastAsync(team.Id, cancellationToken);

            if (last is null)
            {
                await _finance.AddAsync(FinanceMovement.Seed(team.Id, nextSeasonId, FinanceRules.StartingBalance));
                written++;
                continue;
            }

            await _finance.AddAsync(FinanceMovement.OpenWithBalance(
                team.Id,
                nextSeasonId,
                sequence: last.Sequence + 1,
                FinanceMovementKind.CarryOver,
                $"Saldo transportado da {previousSeason?.Name ?? "temporada anterior"}",
                last.BalanceAfter));

            written++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Carried the closing balances of {SeasonName} into season {SeasonNumber} for {ClubCount} clubs.",
            previousSeason?.Name,
            (await _seasons.GetAsync(nextSeasonId, cancellationToken))?.Number,
            written);

        return written;
    }

    /// <summary>
    /// Pays a club a prize, once and only once.
    ///
    /// The guard is the club, the season and what the money is for, so a prize that is paid
    /// twice — because a season was closed twice, or because a cup tie was settled twice — is
    /// refused rather than doubled. A club paid a championship purse for finishing third
    /// twice has been paid for a season it played once, and a ledger that says otherwise is a
    /// ledger no manager can use to decide anything.
    /// </summary>
    /// <returns>The line, or null when the club has already been paid this prize.</returns>
    public async Task<FinanceMovement?> RecordPrizeAsync(
        Guid teamId,
        Guid seasonId,
        FinanceMovementKind kind,
        string description,
        decimal amount,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "A prize says what it is the prize for, or it cannot be told from the next one.",
                nameof(reference));
        }

        if (await _finance.ExistsWithReferenceAsync(teamId, seasonId, kind, reference, cancellationToken))
        {
            return null;
        }

        return await AppendAsync(
            teamId,
            seasonId,
            matchDayNumber: null,
            kind,
            description,
            amount,
            matchId: null,
            cancellationToken,
            reference);
    }

    /// <summary>
    /// Records the money that moves when a transfer completes. The selling club is paid the
    /// fee and the buying club is charged it, and both lines carry the transfer's own
    /// reference so a deal that is settled twice is settled once.
    ///
    /// The two lines are written in the same call because a transfer is one transaction: a
    /// club that is paid and a club that is charged are the two halves of it, and writing
    /// one without the other would be a book that disagrees with itself about what happened.
    /// </summary>
    public async Task RecordTransferAsync(
        Guid transferId,
        Guid sellingClubId,
        Guid buyingClubId,
        Guid seasonId,
        int? matchDayNumber,
        decimal fee,
        CancellationToken cancellationToken = default)
    {
        var sellingReference = $"transfer:{transferId}:sale";
        var buyingReference = $"transfer:{transferId}:purchase";

        if (await _finance.ExistsWithReferenceAsync(
                sellingClubId, seasonId, FinanceMovementKind.TransferIn, sellingReference, cancellationToken))
        {
            return;
        }

        var sellingLine = await AppendAsync(
            sellingClubId,
            seasonId,
            matchDayNumber,
            FinanceMovementKind.TransferIn,
            $"Venda de jogador — multa {fee:N2}",
            fee,
            transferId,
            cancellationToken,
            sellingReference);

        await AppendAsync(
            buyingClubId,
            seasonId,
            matchDayNumber,
            FinanceMovementKind.TransferOut,
            $"Compra de jogador — multa {fee:N2}",
            -fee,
            transferId,
            cancellationToken,
            buyingReference);

        _logger.LogInformation(
            "Transfer {TransferId} settled: {SellingClub} received {Fee}, {BuyingClub} paid {Fee}.",
            transferId,
            sellingLine.BalanceAfter,
            fee,
            fee);
    }

    /// <summary>
    /// Writes a line on top of what the club already has.
    ///
    /// The balance and the sequence both come from the club's own last line, so every line is
    /// written against the state the book was really in. A club plays one match at a time, so
    /// two lines can never be written from the same balance, which is the only way the running
    /// balance in a book could be wrong.
    /// </summary>
    private async Task<FinanceMovement> AppendAsync(
        Guid teamId,
        Guid seasonId,
        int? matchDayNumber,
        FinanceMovementKind kind,
        string description,
        decimal amount,
        Guid? matchId,
        CancellationToken cancellationToken,
        string? reference = null)
    {
        var last = await _finance.GetLastAsync(teamId, cancellationToken);

        var line = FinanceMovement.Create(
            teamId,
            seasonId,
            sequence: (last?.Sequence ?? 0) + 1,
            matchDayNumber,
            kind,
            description,
            amount,
            balanceBefore: last?.BalanceAfter ?? 0m,
            matchId,
            reference);

        await _finance.AddAsync(line, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return line;
    }

    /// <summary>
    /// The day of the season a fixture was played on, which is the day a line of money is
    /// written against. A fixture scheduled for no day has no number, and its lines are
    /// written without one rather than with a number invented for it.
    /// </summary>
    public async Task<int?> ResolveMatchDayAsync(Guid fixtureId, CancellationToken cancellationToken)
    {
        var fixture = await _fixtures.GetAsync(fixtureId, cancellationToken);
        if (fixture is null)
        {
            return null;
        }

        return await ResolveMatchDayForRoundAsync(fixture.RoundId, cancellationToken);
    }

    private async Task<int?> ResolveMatchDayForRoundAsync(Guid roundId, CancellationToken cancellationToken)
    {
        var round = await _rounds.GetAsync(roundId, cancellationToken);
        if (round?.MatchDayId is not { } matchDayId)
        {
            return null;
        }

        var matchDay = await _matchDays.GetAsync(matchDayId, cancellationToken);

        return matchDay?.Number;
    }

    private async Task<Dictionary<Guid, string>> SeasonNamesAsync(CancellationToken cancellationToken)
    {
        var seasons = await _seasons.ListAsync(cancellationToken);

        return seasons.ToDictionary(season => season.Id, season => season.Name);
    }

    private async Task<Dictionary<Guid, int>> SeasonNumbersAsync(CancellationToken cancellationToken)
    {
        var seasons = await _seasons.ListAsync(cancellationToken);

        return seasons.ToDictionary(season => season.Id, season => season.Number);
    }
}
