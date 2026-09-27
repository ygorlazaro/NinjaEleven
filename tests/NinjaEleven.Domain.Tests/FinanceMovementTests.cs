using NinjaEleven.Domain.Finance;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A club's books, read as a book.
///
/// The rules that matter here are the ones a balance could get wrong: that a line continues
/// from the last one, that the two lines which state a balance do not move it, and that a
/// club which has spent more than it took is told so rather than protected from it.
/// </summary>
public class FinanceMovementTests
{
    private static readonly Guid TeamId = Guid.NewGuid();
    private static readonly Guid SeasonId = Guid.NewGuid();

    [Fact]
    public void A_club_is_founded_with_a_line_and_not_with_a_number()
    {
        var movement = FinanceMovement.Seed(TeamId, SeasonId, FinanceRules.StartingBalance);

        Assert.Equal(FinanceMovementKind.Seed, movement.Kind);
        Assert.Equal(1, movement.Sequence);
        Assert.Equal(FinanceRules.StartingBalance, movement.Amount);
        Assert.Equal(FinanceRules.StartingBalance, movement.BalanceAfter);
    }

    [Fact]
    public void The_capital_a_club_is_given_is_not_income()
    {
        var movement = FinanceMovement.Seed(TeamId, SeasonId, FinanceRules.StartingBalance);

        Assert.False(movement.IsIncome);
        Assert.False(movement.IsExpense);
    }

    [Fact]
    public void A_movement_continues_from_the_balance_the_club_had()
    {
        var first = FinanceMovement.Seed(TeamId, SeasonId, FinanceRules.StartingBalance);

        var gate = FinanceMovement.Create(
            TeamId, SeasonId, 2, 1, FinanceMovementKind.GateRevenue, "Bilheteria", 25_000m, first.BalanceAfter);

        var wages = FinanceMovement.Create(
            TeamId, SeasonId, 3, 1, FinanceMovementKind.Wages, "Folha salarial", -600_000m, gate.BalanceAfter);

        Assert.Equal(1_025_000m, gate.BalanceAfter);
        Assert.Equal(425_000m, wages.BalanceAfter);
    }

    [Fact]
    public void A_carried_balance_states_the_balance_rather_than_adding_to_it()
    {
        var closing = 425_000m;

        var carried = FinanceMovement.OpenWithBalance(
            TeamId,
            Guid.NewGuid(),
            sequence: 4,
            FinanceMovementKind.CarryOver,
            "Saldo transportado da Temporada I",
            closing);

        Assert.Equal(closing, carried.Amount);
        Assert.Equal(closing, carried.BalanceAfter);
        Assert.Null(carried.MatchDayNumber);
    }

    [Fact]
    public void A_carried_balance_is_neither_income_nor_an_expense()
    {
        var carried = FinanceMovement.OpenWithBalance(
            TeamId, Guid.NewGuid(), 4, FinanceMovementKind.CarryOver, "Saldo transportado", 425_000m);

        Assert.False(carried.IsIncome);
        Assert.False(carried.IsExpense);
    }

    [Fact]
    public void Only_the_founding_capital_and_a_carried_balance_may_open_a_book()
    {
        Assert.Throws<ArgumentException>(() => FinanceMovement.OpenWithBalance(
            TeamId, SeasonId, 1, FinanceMovementKind.GateRevenue, "Bilheteria", 100m));
    }

    [Fact]
    public void A_club_that_spends_more_than_it_takes_is_told_it_is_in_debt()
    {
        var opening = FinanceMovement.Seed(TeamId, SeasonId, FinanceRules.StartingBalance);

        var bill = FinanceMovement.Create(
            TeamId, SeasonId, 2, 1, FinanceMovementKind.Wages, "Folha salarial", -1_500_000m, opening.BalanceAfter);

        Assert.Equal(-500_000m, bill.BalanceAfter);
    }

    [Fact]
    public void A_line_of_the_book_says_what_it_is_for()
    {
        Assert.Throws<ArgumentException>(() => FinanceMovement.Create(
            TeamId, SeasonId, 1, 1, FinanceMovementKind.GateRevenue, "  ", 100m, 0m));
    }

    [Fact]
    public void A_movement_that_moves_no_money_is_refused()
    {
        // Zero is not a movement: a line of nothing in a ledger is a line a manager has to
        // read and cannot understand, and the only kinds allowed to state a balance are the
        // two that say so out loud.
        Assert.Throws<ArgumentOutOfRangeException>(() => FinanceMovement.Create(
            TeamId, SeasonId, 1, 1, FinanceMovementKind.GateRevenue, "Bilheteria", 0m, 0m));
    }

    [Fact]
    public void A_club_has_no_lines_before_its_first()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FinanceMovement.Create(
            TeamId, SeasonId, 0, 1, FinanceMovementKind.GateRevenue, "Bilheteria", 100m, 0m));
    }

    [Fact]
    public void A_season_has_no_day_before_its_first()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FinanceMovement.Create(
            TeamId, SeasonId, 1, -1, FinanceMovementKind.GateRevenue, "Bilheteria", 100m, 0m));
    }

    [Fact]
    public void A_line_belongs_to_a_club_and_to_a_season()
    {
        Assert.Throws<ArgumentException>(() => FinanceMovement.Create(
            Guid.Empty, SeasonId, 1, 1, FinanceMovementKind.GateRevenue, "Bilheteria", 100m, 0m));

        Assert.Throws<ArgumentException>(() => FinanceMovement.Create(
            TeamId, Guid.Empty, 1, 1, FinanceMovementKind.GateRevenue, "Bilheteria", 100m, 0m));
    }

    [Fact]
    public void A_club_is_never_founded_in_debt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FinanceMovement.Seed(TeamId, SeasonId, -1m));
    }
}
