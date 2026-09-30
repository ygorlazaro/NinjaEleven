using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A club's training day: how many sessions it has, what each one costs, and the fact that
/// both come out of the club rather than out of the manager's goodwill.
///
/// <para>
/// The allowance is counted from the sessions themselves, and that is the property worth
/// holding: a rule kept in a counter can be out of step with the thing it counts, and a rule
/// out of step with its own history hands out a session nobody paid for.
/// </para>
/// </summary>
public class TrainingAllowanceTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public async Task ARestDayBuysTheClubTwoSessionsAndAMatchdayOne()
    {
        // The two halves of the rule, which is one number with a condition on it and not two
        // numbers: a club that has not drawn its calendar is a club at rest, and a club on a
        // matchday gets one session because the morning belongs to the afternoon.
        using var resting = await AWorldAsync();
        using var playing = await AWorldAsync();
        await playing.GivenAMatchOnAsync(Today);

        var rest = await resting.Training().QuoteAsync(TrainingWorld.TheClub);
        var match = await playing.Training().QuoteAsync(TrainingWorld.TheClub);

        Assert.False(rest.PlaysToday);
        Assert.Equal(TrainingRules.SessionsOnARestDay, rest.SessionsAllowed);

        Assert.True(match.PlaysToday);
        Assert.Equal(TrainingRules.SessionsOnAMatchDay, match.SessionsAllowed);
    }

    [Fact]
    public async Task TheAllowanceIsTheClubsAndNotThePlayers()
    {
        // The squad of one: twenty-three men share two sessions, and training the striker has
        // spent the same one that training the reserve goalkeeper would have. An allowance per
        // man would be a limit that stopped mattering the moment a club had a squad.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        var first = squad[0];
        var second = squad[1];

        await TrainAsync(world, first.PlayerId, PlayerAttribute.Speed);
        await TrainAsync(world, second.PlayerId, PlayerAttribute.Speed);

        var after = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        Assert.Equal(2, after.SessionsSpent);
        Assert.Equal(0, after.SessionsAllowed - after.SessionsSpent);
    }

    [Fact]
    public async Task ASpentAllowanceIsRefusedRatherThanChargedFor()
    {
        // The refusal is the feature. A club on a matchday has one session; the second click
        // on a different man is refused, and refused before the man is told what it would
        // have cost — because he is not being offered anything.
        using var world = await AWorldAsync();
        await world.GivenAMatchOnAsync(Today);

        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;
        var first = squad[0];
        var second = squad[1];

        await TrainAsync(world, first.PlayerId, PlayerAttribute.Speed);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, second.PlayerId, PlayerAttribute.Speed));

        Assert.Equal("TrainingAllowanceSpent", error.Code);

        // And nothing was written: the man refused is as he was, and the club's book has one
        // line in it rather than two.
        var refused = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players
            .Single(player => player.PlayerId == second.PlayerId);

        Assert.Equal(second.Energy, refused.Energy);

        var sessions = await world.Sessions.ListByTeamAndDayAsync(TrainingWorld.TheClub, Today);
        Assert.Single(sessions);
    }

    [Fact]
    public async Task TomorrowTheAllowanceIsWholeAgain()
    {
        // A day's allowance is a day's allowance. Nothing is carried, nothing is owed, and a
        // club that spent yesterday's is not invoiced for it: the count is read from the
        // sessions of the day in question and from no other day.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        await TrainAsync(world, squad[0].PlayerId, PlayerAttribute.Speed);
        await TrainAsync(world, squad[1].PlayerId, PlayerAttribute.Speed);

        world.Clock.AdvanceTo(Today.AddDays(1));

        var tomorrow = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        Assert.Equal(0, tomorrow.SessionsSpent);
        Assert.Equal(TrainingRules.SessionsOnARestDay, tomorrow.SessionsAllowed);
    }

    [Fact]
    public async Task ASessionCostsTheClubAFifteenthOfTheMansWage()
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
    public async Task ATrainedManCostsTheClubWhatHisOwnWageWouldHaveSaid()
    {
        // Worked out from the man rather than from the quote, because the fee is a share of a
        // wage and a fee that is only ever checked against itself proves nothing.
        using var world = await AWorldAsync();
        await world.GivenAnOpenedBookAsync();

        var quoted = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players.First();
        var player = await world.Players.GetAsync(quoted.PlayerId);
        var state = (await world.Players.ListSeasonStatesAsync(world.SeasonId, TrainingWorld.TheClub))
            .Single(candidate => candidate.PlayerId == quoted.PlayerId);

        var expected = PlayerValuation.SeasonWage(player, state) * TrainingRules.SessionFeeRate;

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

        // Its own place in the day's allowance, so the count and the rows cannot be two
        // answers to the same question that drift apart.
        Assert.Equal(0, session.Ordinal);

        var line = await world.Finance.GetLastAsync(TrainingWorld.TheClub);
        Assert.Equal(FinanceMovementKind.Training, line!.Kind);
        Assert.Equal(TrainingService.TrainingReference(session.Id), line.Reference);
    }

    [Fact]
    public async Task TheAllowanceIsCountedFromTheSessionsAndNotFromAStoredTally()
    {
        // The property the whole rule rests on. A session inserted into the history — by a
        // migration, a backfill, a process that crashed between writing the row and raising
        // the point — is a session the club has had, and a tally that had not heard of it
        // would hand out one more.
        using var world = await AWorldAsync();
        await world.GivenAMatchOnAsync(Today);

        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        await world.Sessions.AddAsync(TrainingSession.Create(
            squad[0].PlayerId, TrainingWorld.TheClub, world.SeasonId, Today,
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero),
            null, PlayerAttribute.Speed, 6, 100m));
        await world.UnitOfWork.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, squad[1].PlayerId, PlayerAttribute.Speed));

        Assert.Equal("TrainingAllowanceSpent", error.Code);
    }

    [Fact]
    public async Task ASessionsAreNumberedInTheOrderTheDayWasSpentInIt()
    {
        // The second session of a rest day is the second one, and a day's sessions carry their
        // own places. This is the property the unique index over club, day and place rests
        // on: a session that claimed a place already taken cannot be written at all, so two
        // clicks arriving together cannot both be told yes.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        await TrainAsync(world, squad[0].PlayerId, PlayerAttribute.Speed);
        await TrainAsync(world, squad[1].PlayerId, PlayerAttribute.Speed);

        var sessions = await world.Sessions.ListByTeamAndDayAsync(TrainingWorld.TheClub, Today);

        Assert.Equal(new[] { 0, 1 }, sessions.Select(session => session.Ordinal).Order().ToArray());
    }

    [Fact]
    public async Task AManWithNoClubHasNoAllowanceToSpend()
    {
        // A free agent's session is nobody's cost. The allowance is the club's, and there is
        // no club, so there is nothing to charge and nothing to spend.
        using var world = await AWorldAsync();
        var quoted = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players.First();

        // He leaves: the season state loses its club, which is what a contract ending is.
        // The read is detached, so the change has to be handed to the repository that owns
        // the row — the same reason every command in the game updates rather than saves.
        var state = (await world.Players.ListSeasonStatesAsync(world.SeasonId, TrainingWorld.TheClub))
            .Single(candidate => candidate.PlayerId == quoted.PlayerId);
        state.SetTeam(null);
        world.Players.UpdateSeasonState(state);
        await world.UnitOfWork.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, quoted.PlayerId, PlayerAttribute.Speed));

        Assert.Equal("PlayerNotContracted", error.Code);
        Assert.Empty(await world.Sessions.ListByPlayerAsync(quoted.PlayerId));
    }

    // --- Helpers ------------------------------------------------------------------

    private static Task<TrainingWorld> AWorldAsync() =>
        TrainingWorld.GivenAsync(new[]
        {
            TrainingWorld.APlayer("Zagueiro", 23, Position.DEF, 78),
            TrainingWorld.APlayer("Meia", 26, Position.MID, 84),
            TrainingWorld.APlayer("Goleiro", 29, Position.GK, 80)
        });

    private static Task<TrainingResult> TrainAsync(TrainingWorld world, Guid playerId, PlayerAttribute attribute) =>
        world.Training().TrainAsync(playerId, attribute);

    private static async Task<decimal> TheOnlyTrainingLineAsync(TrainingWorld world)
    {
        var lines = await world.Finance.ListAsync(TrainingWorld.TheClub, world.SeasonId, 0, 10);

        return Assert.Single(lines.Where(line => line.Kind == FinanceMovementKind.Training)).Amount;
    }
}
