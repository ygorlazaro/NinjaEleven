using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A club's training day: what a session costs, what it improves, and the fact that both
/// come out of the club rather than out of the manager's goodwill.
///
/// <para>
/// Training has no daily session cap — a player may train as many times as his energy allows
/// in a day — so the allowance tests have been retired. What remains is the price and the
/// refusal: a session still costs the club half the man's season wage, and still refuses on
/// energy, potential and availability.
/// </para>
/// </summary>
public class TrainingAllowanceTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public async Task ASessionCostsTheClubHalfOfTheContractWage()
    {
        // The two numbers on the sheet are the two numbers charged. A manager who reads a fee
        // and is billed another one has been told a price he could not rely on, and the only
        // way to find out which one was the lie is to compare them.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var quoted = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players
            .First(player => player.IsAvailable);

        var result = await TrainAsync(world, quoted.PlayerId, PlayerAttribute.Speed);

        Assert.Equal(quoted.SessionFee, result.Fee);
        Assert.Equal(-result.Fee, await TheOnlyTrainingLineAsync(world));
    }

    [Fact]
    public async Task ATrainedManCostsTheClubWhatHisOwnContractWouldHaveSaid()
    {
        // Worked out from the contract rather than from the quote, because the fee is a share
        // of a wage and a fee that is only ever checked against itself proves nothing. The
        // wage is read off the membership the man is on, which is the whole reason it lives
        // there: two men of the same ability under two contracts are two fees, and only one of
        // them is the man the club actually signed.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var quoted = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players.First();
        var contract = (await world.Teams.GetSquadAsync(TrainingWorld.TheClub, world.SeasonId))
            .Single(membership => membership.PlayerId == quoted.PlayerId);

        var expected = contract.Wage * TrainingRules.SessionFeeRate;

        await TrainAsync(world, quoted.PlayerId, PlayerAttribute.Speed);

        Assert.Equal(expected, await TheOnlyTrainingLineAsync(world) * -1m);
    }

    [Fact]
    public async Task ASessionIsPaidForInOneCommitAndNamesItselfInTheBook()
    {
        // The line names the session that caused it, so a statement a manager is holding can
        // be checked against the training history rather than taken on trust.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var quoted = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players.First();
        var result = await TrainAsync(world, quoted.PlayerId, PlayerAttribute.Speed);

        var session = Assert.Single(await world.Sessions.ListByPlayerAsync(quoted.PlayerId));

        Assert.Equal(quoted.PlayerId, session.PlayerId);
        Assert.Equal(TrainingWorld.TheClub, session.TeamId);
        Assert.Equal(Today, session.Day);
        Assert.Equal(result.Fee, session.Fee);

        var line = await world.Finance.GetLastAsync(TrainingWorld.TheClub);
        Assert.Equal(FinanceMovementKind.Training, line!.Kind);
        Assert.Equal(TrainingService.TrainingReference(session.Id), line.Reference);
    }

    [Fact]
    public async Task AnInjuredManCannotTrain()
    {
        // The feature no longer refuses on the basis of an existing knock, it refuses on the
        // basis of a suspension — but an actual injury still blocks a session.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var man = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players[0];

        var state = (await world.Players.ListSeasonStatesAsync(world.SeasonId, TrainingWorld.TheClub))
            .Single(candidate => candidate.PlayerId == man.PlayerId);
        state.AddInjury(Injury.Light, 3);
        world.Players.UpdateSeasonState(state);
        await world.UnitOfWork.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, man.PlayerId, PlayerAttribute.Speed));

        Assert.Equal("PlayerInjured", error.Code);
    }

    [Fact]
    public async Task AnAttributeAlreadyAtPotentialIsRefused()
    {
        using var world = await AWorldAsync();
        var man = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players[0];

        // Set the attribute to the player's potential directly, so there is no random
        // injury risk in the loop that trained up to it.
        var player = await world.Players.GetAsync(man.PlayerId);
        player.Set(PlayerAttribute.Speed, player.Potential);
        world.Players.Update(player);
        await world.UnitOfWork.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, man.PlayerId, PlayerAttribute.Speed));

        Assert.Equal("PotentialReached", error.Code);
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

    private static Task<TrainingResult> TrainAsync(TrainingWorld world, Guid playerId, PlayerAttribute attribute) =>
        world.Training().TrainAsync(playerId, attribute);

    private static async Task<decimal> TheOnlyTrainingLineAsync(TrainingWorld world)
    {
        var lines = await world.Finance.ListAsync(TrainingWorld.TheClub, world.SeasonId, 0, 10);

        return Assert.Single(lines.Where(line => line.Kind == FinanceMovementKind.Training)).Amount;
    }
}