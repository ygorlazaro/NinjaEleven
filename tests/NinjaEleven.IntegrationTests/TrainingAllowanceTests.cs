using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A club's training day: how many sessions a man has, what each one costs, and the fact that
/// both come out of the club rather than out of the manager's goodwill.
///
/// <para>
/// The allowance is counted from the sessions themselves, and that is the property worth
/// holding: a rule kept in a counter can be out of step with the thing it counts, and a rule
/// out of step with its own history hands out a session nobody paid for.
/// </para>
///
/// <para>
/// The allowance belongs to the man, not to the club. A club-wide allowance is a limit that
/// stops mattering the moment a club has a squad: training the striker spends the same session
/// training the reserve goalkeeper would have, so the second man a manager trains is told there
/// is nothing left. The rule has to be per man for the day's work to be the day's work.
/// </para>
/// </summary>
public class TrainingAllowanceTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public async Task ARestDayBuysEachManTwoSessionsAndAMatchdayOne()
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
    public async Task TheWholeSquadTrainsOnTheSameDayBecauseTheAllowanceIsEachMans()
    {
        // The property the rule rests on. Every man of the eleven holds his own allowance, so
        // the day's work is the day's work and not the first click of the morning: a club-wide
        // allowance means that training one striker has spent the goalkeeper's session too, and
        // a manager who opens the squad screen on a rest day cannot train anybody.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        Assert.Equal(12, squad.Count);

        foreach (var player in squad)
        {
            await TrainAsync(world, player.PlayerId, PlayerAttribute.Speed);
        }

        // Each man has his own place left on the day, and nobody has run out of his own. The
        // club has spent twelve sessions and still has a full day's work in front of it, which
        // is the whole difference between an allowance per man and one for the squad: the
        // quote counts what the day has cost the club, and each man's own column says what he
        // may still do.
        var after = await world.Training().QuoteAsync(TrainingWorld.TheClub);

        Assert.All(
            after.Players,
            player => Assert.Equal(TrainingRules.SessionsOnARestDay - 1, player.SessionsLeft));
        Assert.Equal(squad.Count, after.SessionsSpent);
    }

    [Fact]
    public async Task ASpentAllowanceIsRefusedRatherThanChargedFor()
    {
        // The refusal is the feature. A man who has had both of a rest day's sessions is told
        // so, and told before he is billed for it — because he is not being offered anything.
        using var world = await AWorldAsync();

        var man = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players[0];

        await TrainAsync(world, man.PlayerId, PlayerAttribute.Speed);
        await TrainAsync(world, man.PlayerId, PlayerAttribute.Dribbling);

        // Read him as he is after his day's two sessions, not as he was this morning: a
        // refusal that held his energy still would be worth nothing if the sessions he was
        // allowed had not moved it.
        var spent = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players
            .Single(player => player.PlayerId == man.PlayerId);

        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, man.PlayerId, PlayerAttribute.Strength));

        Assert.Equal("TrainingAllowanceSpent", error.Code);

        // And nothing was written: the man refused is as he was, and the club's book has two
        // lines in it rather than three.
        var refused = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players
            .Single(player => player.PlayerId == man.PlayerId);

        Assert.Equal(spent.Energy, refused.Energy);

        var sessions = await world.Sessions.ListByPlayerAsync(man.PlayerId);
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task TomorrowTheAllowanceIsWholeAgain()
    {
        // A day's allowance is a day's allowance. Nothing is carried, nothing is owed, and a
        // man who spent yesterday's is not invoiced for it: the count is read from the sessions
        // of the day in question and from no other day.
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
        // the point — is a session the man has had, and a tally that had not heard of it
        // would hand out one more.
        using var world = await AWorldAsync();
        await world.GivenAMatchOnAsync(Today);

        var man = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players[0];

        await world.Sessions.AddAsync(TrainingSession.Create(
            man.PlayerId, TrainingWorld.TheClub, world.SeasonId, Today,
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero),
            null, PlayerAttribute.Speed, 6, 100m));
        await world.UnitOfWork.SaveChangesAsync();

        // The matchday leaves him one session, and the inserted row is that one.
        var error = await Assert.ThrowsAsync<DomainValidationException>(
            () => TrainAsync(world, man.PlayerId, PlayerAttribute.Speed));

        Assert.Equal("TrainingAllowanceSpent", error.Code);
    }

    [Fact]
    public async Task ASessionsAreNumberedInTheOrderTheDayWasSpentInIt()
    {
        // A man's second session of a rest day is his second one, and a day's sessions carry
        // their own places. This is the property the unique index over man, day and place
        // rests on: a session that claimed a place already taken cannot be written at all, so
        // two clicks arriving together cannot both be told yes. The index is per man rather
        // than per club because the allowance is, and an index wider than the rule would hand
        // the eleventh man a place the tenth had taken.
        using var world = await AWorldAsync();
        var man = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players[0];

        await TrainAsync(world, man.PlayerId, PlayerAttribute.Speed);
        await TrainAsync(world, man.PlayerId, PlayerAttribute.Dribbling);

        var sessions = await world.Sessions.ListByPlayerAsync(man.PlayerId);

        Assert.Equal(new[] { 0, 1 }, sessions.Select(session => session.Ordinal).Order().ToArray());
    }

    [Fact]
    public async Task TwoMenOfOneDayDoNotShareTheirPlaces()
    {
        // The companion to the numbering: the places are numbered inside a man's own day, so
        // every man's first session is his zeroth. A shared counter would make the squad's
        // second starter his second session and refuse him a session he had not had.
        using var world = await AWorldAsync();
        var squad = (await world.Training().QuoteAsync(TrainingWorld.TheClub)).Players;

        foreach (var player in squad)
        {
            await TrainAsync(world, player.PlayerId, PlayerAttribute.Speed);
        }

        var sessions = await world.Sessions.ListByTeamAndDayAsync(TrainingWorld.TheClub, Today);

        Assert.Equal(12, sessions.Count);
        Assert.All(sessions, session => Assert.Equal(0, session.Ordinal));
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

    /// <summary>
    /// A squad of a whole eleven and a keeper in reserve, because a rule about what a man may
    /// spend can only be held against a club that has more men than sessions: a squad of three
    /// would pass a club-wide allowance exactly as happily as a per-man one.
    /// </summary>
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
