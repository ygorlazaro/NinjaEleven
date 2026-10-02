using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// Training a squad rather than a man: one decision, several sessions, and an answer per man.
///
/// <para>
/// The batch exists because a manager's real decision is usually not about one player. It is
/// "put the whole squad through it this morning", and a screen that made that twelve round
/// trips would be a screen where half the squad got worked before the manager was told the
/// sixth man had no energy left.
/// </para>
///
/// <para>
/// So the property worth holding is that a refusal is per man and not per batch. A man who
/// cannot train is a fact about that man; a batch that gave up on the other eleven because of
/// him would turn one tired player into a squad that was not trained.
/// </para>
/// </summary>
public class TrainingBatchTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public async Task EveryManOfASelectionIsTrainedAndEveryManIsAnsweredFor()
    {
        // The whole squad, in one call, and one line of answer per man asked about. An answer
        // that is silently shorter than the selection is a manager who cannot tell which of
        // his twelve men were worked.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;
        var selection = squad
            .Select(player => new TrainingRequest(player.PlayerId, PlayerAttribute.Speed))
            .ToList();

        var outcomes = await world.Training().TrainManyAsync(selection);

        Assert.Equal(selection.Count, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.True(outcome.Worked));

        // And the sessions are on the record, one per man, rather than the batch having
        // reported success without writing anything down.
        foreach (var man in squad)
        {
            Assert.Single(await world.Sessions.ListByPlayerAsync(man.PlayerId));
        }
    }

    [Fact]
    public async Task ARefusalSaysWhichRuleRefusedAndNotThatTheManCouldNotTrain()
    {
        // The code travels with the outcome so a screen can tell a spent allowance from a spent
        // man. "Não pôde treinar" for both would send the manager to fix two different problems
        // the same way: one of them is tomorrow, the other is a substitute.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        // A man on no club has no allowance to spend, which is a different rule from a man who
        // has spent his, and the batch has to keep them apart.
        var freeAgent = squad[0];
        var state = (await world.Players.ListSeasonStatesAsync(world.SeasonId, TrainingWorld.TheClub))
            .Single(candidate => candidate.PlayerId == freeAgent.PlayerId);
        state.SetTeam(null);
        world.Players.UpdateSeasonState(state);
        await world.UnitOfWork.SaveChangesAsync();

        var outcomes = await world.Training().TrainManyAsync(
            squad.Select(player => new TrainingRequest(player.PlayerId, PlayerAttribute.Speed)).ToList());

        var refused = Assert.Single(outcomes.Where(outcome => !outcome.Worked));

        Assert.Equal(freeAgent.PlayerId, refused.PlayerId);
        Assert.Equal("PlayerNotContracted", refused.RefusalCode);
        Assert.False(string.IsNullOrWhiteSpace(refused.Refusal));
    }

    [Fact]
    public async Task ARefusedManIsChargedForNothing()
    {
        // The book follows the sessions, and a refused man has no session. A batch that billed
        // the whole selection and then reported that one of them was refused would be charging
        // a manager for a morning that did not happen.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        // Take the first man off his club so he cannot train: a player with no contract has no
        // club to pay a fee and is refused before any session is recorded.
        var state = (await world.Players.ListSeasonStatesAsync(world.SeasonId, TrainingWorld.TheClub))
            .Single(candidate => candidate.PlayerId == squad[0].PlayerId);
        state.SetTeam(null);
        world.Players.UpdateSeasonState(state);
        await world.UnitOfWork.SaveChangesAsync();

        var outcomes = await world.Training().TrainManyAsync(
            squad.Select(player => new TrainingRequest(player.PlayerId, PlayerAttribute.Speed)).ToList());

        var lines = await world.Finance.ListAsync(TrainingWorld.TheClub, world.SeasonId, 0, 50);
        var training = lines.Where(line => line.Kind == FinanceMovementKind.Training).ToList();

        // One line for each of the eleven trainable men. The refused man is not among them.
        Assert.Equal(squad.Count - 1, training.Count);
        Assert.All(training, line => Assert.True(line.Amount < 0m));

        // And the refused man's record is still empty: the batch added nothing to it.
        var refused = outcomes.Single(outcome => !outcome.Worked);

        Assert.Empty(await world.Sessions.ListByPlayerAsync(refused.PlayerId));
    }

    [Fact]
    public async Task ASquadIsBilledForEachManAndNotOnceForTheMorning()
    {
        // A batch is several sessions and not one purchase. A club training eleven men is
        // paying eleven fees, and a fee charged once for the whole squad would make a squad of
        // twelve cost a twelfth of what a squad of one costs per man.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;
        var contracts = (await world.Teams.GetSquadAsync(TrainingWorld.TheClub, world.SeasonId))
            .ToDictionary(membership => membership.PlayerId, membership => membership.Wage);

        var outcomes = await world.Training().TrainManyAsync(
            squad.Select(player => new TrainingRequest(player.PlayerId, PlayerAttribute.Speed)).ToList());

        var expected = outcomes.Sum(outcome => contracts[outcome.PlayerId] * TrainingRules.SessionFeeRate);
        var billed = (await world.Finance.ListAsync(TrainingWorld.TheClub, world.SeasonId, 0, 50))
            .Where(line => line.Kind == FinanceMovementKind.Training)
            .Sum(line => line.Amount);

        Assert.Equal(-expected, billed);
    }

    [Fact]
    public async Task AnEmptySelectionIsAMorningNobodySpent()
    {
        // A manager who selected nobody and pressed the button has not trained anybody, and
        // the answer is no rows rather than an error: the request was well formed, it simply
        // asked for nothing.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var outcomes = await world.Training().TrainManyAsync(new List<TrainingRequest>());

        Assert.Empty(outcomes);
        Assert.Empty(await world.Sessions.ListByTeamAndDayAsync(TrainingWorld.TheClub, Today));
    }

    // --- Helpers ------------------------------------------------------------------

    private static Task<TrainingWorld> AWorldAsync() =>
        TrainingWorld.GivenAsync(
            new[] { TrainingWorld.APlayer("Goleiro", 29, Position.GK, 80) }
                .Concat(Enumerable.Range(1, 4).Select(number =>
                    TrainingWorld.APlayer($"Zagueiro {number}", 23 + number, Position.DEF, 74 + number)))
                .Concat(Enumerable.Range(1, 4).Select(number =>
                    TrainingWorld.APlayer($"Meia {number}", 26 + number, Position.MID, 80 + number)))
                .Concat(Enumerable.Range(1, 3).Select(number =>
                    TrainingWorld.APlayer($"Atacante {number}", 22 + number, Position.ATT, 82 + number))));

    private static Task<TrainingResult> TrainAsync(
        TrainingWorld world, Guid playerId, PlayerAttribute attribute) =>
        world.Training().TrainAsync(playerId, attribute);
}