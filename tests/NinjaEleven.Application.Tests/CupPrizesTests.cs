using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What a cup run is paid, and what the panel under the bracket is allowed to call it.
/// </summary>
public class CupPrizesTests
{
    // The rule is asked of the service with nothing behind it: the money a cup pays is a fact
    // about the competition and not a question asked of the database, so a test that seeded a
    // world to read it would be testing the seeder.
    private static readonly IReadOnlyList<Application.Models.CupPrize> Prizes =
        new CompetitionService(null!, null!, null!, null!).GetCupPrizes();

    [Fact]
    public void The_winner_and_the_runner_up_are_told_apart()
    {
        var champion = Prizes.Single(prize => prize.IsChampion);
        var runnerUp = Prizes.Single(prize => prize.IsRunnerUp);

        Assert.Equal(0, champion.TieRound);
        Assert.False(champion.IsRunnerUp);
        Assert.Equal(CompetitionRules.CupRounds, runnerUp.TieRound);
        Assert.False(runnerUp.IsChampion);
    }

    /// <summary>
    /// Losing the final is not being knocked out. The two take home money for opposite reasons
    /// — one for a result and one for a defeat — and a legend that filed them under the same
    /// heading would put the runner-up one place above a club that went out in the semifinal and
    /// no amount above a club that went out in the first round, which is exactly what it is not.
    /// </summary>
    [Fact]
    public void The_runner_up_is_paid_more_than_the_first_round_loser()
    {
        var runnerUp = Prizes.Single(prize => prize.IsRunnerUp);
        var firstRoundOut = Prizes.First(
            prize => !prize.IsChampion && !prize.IsRunnerUp && prize.TieRound == 1);

        Assert.True(runnerUp.Amount > firstRoundOut.Amount);
    }

    [Fact]
    public void There_is_exactly_one_champions_cheque_and_one_runner_up()
    {
        Assert.Single(Prizes, prize => prize.IsChampion);
        Assert.Single(Prizes, prize => prize.IsRunnerUp);
    }

    /// <summary>
    /// The consolation grows steeply as the round does, and the top of the ladder is the title.
    /// A legend that lists them in the order the rounds are played puts the biggest cheque of
    /// the competition next to the smallest.
    /// </summary>
    [Fact]
    public void The_champions_cheque_is_the_most_the_cup_pays()
    {
        Assert.Equal(Prizes.Max(prize => prize.Amount), Prizes.Single(p => p.IsChampion).Amount);
    }
}