using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Application.Services;

/// <summary>
/// The treasurer's weekly statement: the club's week, added up once, said once.
///
/// <para>
/// This exists because the box used to carry one message per line of the book. A division
/// club pays its wages thirty times a season and takes the gate on every home matchday, so
/// that is forty messages a season about money the ledger already says, arriving in a column
/// a manager learns to swipe past — and the messages that actually mattered, a bid accepted,
/// a sponsor's deal ended, a prize paid, had to compete with them for attention.
/// </para>
///
/// <para>
/// <b>The week is the unit because the week is the cadence a manager reads at.</b> It is also
/// the shortest span in which the four things a club spends money on have all happened at
/// least once, so a weekly number is a number and not an accident of when the report ran.
/// </para>
///
/// <para>
/// <b>Only the manager's club is written to</b>, by the same rule and through the same door as
/// every other message in the game. Thirty five computer-controlled clubs running a weekly
/// statement would be thirty five rows nobody reads, written every seventh day for ever.
/// </para>
/// </summary>
public class StatementService
{
    private readonly IFinanceRepository _finance;
    private readonly ITeamRepository _teams;
    private readonly InboxService _inbox;
    private readonly ILogger<StatementService> _logger;

    public StatementService(
        IFinanceRepository finance,
        ITeamRepository teams,
        InboxService inbox,
        ILogger<StatementService> logger)
    {
        _finance = finance;
        _teams = teams;
        _inbox = inbox;
        _logger = logger;
    }

    /// <summary>
    /// Whether a day of the season ends a statement, asked of the day rather than of a clock.
    ///
    /// It is here as a static so the caller does not need the rule and so a test can say "on
    /// day seven, and on the last day of the championship whatever its number is" without
    /// standing up a week of football.
    /// </summary>
    public static bool ClosesTheWeekOn(int matchDayNumber) =>
        FinanceRules.ClosesTheBooksOn(matchDayNumber);

    /// <summary>
    /// Writes the week that ends on this day, if this day ends one.
    ///
    /// <para>
    /// A week in which nothing moved is not written at all. That is the same decision the
    /// per-line rule makes from the other end: a statement about nothing is the one message
    /// a manager is guaranteed to stop reading, and the days the club did not spend anything
    /// are the days the ledger is already a perfectly good description of.
    /// </para>
    /// </summary>
    public async Task<bool> CloseTheWeekAsync(
        Guid teamId,
        Guid seasonId,
        int matchDayNumber,
        CancellationToken cancellationToken = default)
    {
        if (!ClosesTheWeekOn(matchDayNumber))
        {
            return false;
        }

        var club = await _teams.GetAsync(teamId, cancellationToken);
        if (club is not { IsManagerClub: true })
        {
            return false;
        }

        var first = FinanceRules.FirstDayOfTheStatementEndingOn(matchDayNumber);

        var lines = await _finance.ListBetweenMatchDaysAsync(
            teamId, seasonId, first, matchDayNumber, cancellationToken);

        if (lines.Count == 0)
        {
            return false;
        }

        // The opening balance is the balance the week starts from, which is the balance after
        // the last line before the window rather than a subtraction that could be got wrong.
        var opening = lines[0].BalanceAfter - lines[0].Amount;
        var closing = lines[^1].BalanceAfter;

        var gate = Total(lines, FinanceMovementKind.GateRevenue);
        var signings = Total(lines, FinanceMovementKind.TransferOut);
        var sales = Total(lines, FinanceMovementKind.TransferIn);
        var wages = Total(lines, FinanceMovementKind.Wages);
        var training = Total(lines, FinanceMovementKind.Training);

        var otherIncome = lines
            .Where(line => line.IsIncome && !IsOneOf(
                line.Kind, FinanceMovementKind.GateRevenue, FinanceMovementKind.TransferIn))
            .Sum(line => line.Amount);

        var otherExpenses = -lines
            .Where(line => line.IsExpense && !IsOneOf(
                line.Kind, FinanceMovementKind.TransferOut, FinanceMovementKind.Wages, FinanceMovementKind.Training))
            .Sum(line => line.Amount);

        // The home count is asked of the gate itself rather than of the calendar: the number
        // of matches that actually paid is the number the receipts are for, and a statement
        // saying "bilheteria: L$ 40.000" over one game and another saying the same over two
        // are different weeks being described by the same sentence.
        var homeMatches = lines.Count(line => line.Kind == FinanceMovementKind.GateRevenue);

        await _inbox.PostStatementAsync(
            new StatementFacts
            {
                RecipientTeamId = teamId,
                ClubName = club.Name,
                FromMatchDay = first,
                ToMatchDay = matchDayNumber,
                GateRevenue = gate,
                Signings = signings,
                Sales = sales,
                OtherIncome = otherIncome,
                Wages = wages,
                Training = training,
                OtherExpenses = otherExpenses,
                OpeningBalance = opening,
                ClosingBalance = closing,
                HomeMatches = homeMatches,
                Reference = $"statement:{seasonId}:{matchDayNumber}"
            },
            cancellationToken);

        _logger.LogInformation(
            "Closed the books of {TeamId} for days {From} to {To}: {Lines} lines, {Home} at the gate.",
            teamId, first, matchDayNumber, lines.Count, homeMatches);

        return true;
    }

    private static bool IsOneOf(FinanceMovementKind kind, params FinanceMovementKind[] kinds) =>
        Array.IndexOf(kinds, kind) >= 0;

    /// <summary>The size of a bucket in a week, as a positive amount whatever side it was on.</summary>
    private static decimal Total(IReadOnlyList<FinanceMovement> lines, FinanceMovementKind kind) =>
        lines.Where(line => line.Kind == kind).Sum(line => Math.Abs(line.Amount));
}
