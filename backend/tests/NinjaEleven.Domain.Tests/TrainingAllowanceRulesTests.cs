using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The two numbers a club's training day turns on: how many sessions it has, and what one
/// costs it.
/// </summary>
public class TrainingAllowanceRulesTests
{
    [Fact]
    public void AClubPlayingHasOneSessionAndAClubAtRestHasTwo()
    {
        // The whole rule in two lines. One on a matchday, two without: a rest day is a
        // doubling and not a holiday, because the day a first team does not play is the day
        // its reserve side does.
        Assert.Equal(1, TrainingRules.DailyBudget(hasMatch: true));
        Assert.Equal(2, TrainingRules.DailyBudget(hasMatch: false));
    }

    [Fact]
    public void ASessionCostsTheClubAFifteenthOfTheMansWage()
    {
        // The fee is a share of a wage and not a number of its own, so a club's whole
        // development bill is a share of its wage bill and a manager can check one against
        // the other on the same screen.
        Assert.Equal(1_800m, TrainingRules.SessionFee(12_000m));
    }

    [Fact]
    public void AFreeAgentWithNoWageIsTrainedForNothing()
    {
        // Zero, and not a refusal: a man with no contract is not a cost to anybody. The
        // session is refused further up, by the rule that only a contracted man has a club to
        // spend an allowance on, and this is here so that the arithmetic never has to invent
        // a wage to divide.
        Assert.Equal(0m, TrainingRules.SessionFee(0m));
    }

    [Fact]
    public void ASessionFeeIsRoundedToTheCentAndNotCarried()
    {
        // Money is money and not a fraction of it: a statement that carried fifteen figures
        // behind the comma would not add up to the balance printed on the same page, and a
        // fee of 0.015 is a fee nobody can pay.
        Assert.Equal(0.02m, TrainingRules.SessionFee(0.1m));
        Assert.Equal(1_851.85m, TrainingRules.SessionFee(12_345.67m));
    }

    [Fact]
    public void ASessionIsRecordedAsHistoryAndIsNeverRewritten()
    {
        var session = TrainingSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 10, 9, 30, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            PlayerAttribute.Accuracy,
            energyCost: 12,
            fee: 1_800m);

        Assert.Equal(new DateOnly(2026, 3, 10), session.Day);
        Assert.Equal(12, session.EnergyCost);
        Assert.Equal(1_800m, session.Fee);
    }

    [Fact]
    public void ASessionIsChargedToTheCalendarDayAndNotToTheHourItHappenedIn()
    {
        // The day is what the allowance is spent against, so a session at one minute to
        // midnight belongs to the day it started rather than to the day it ended: a club that
        // trained at 23:59 and again at 00:01 has had two days' football, and folding them
        // into one would hand it back a session.
        var late = TrainingSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 10, 23, 59, 0, TimeSpan.Zero),
            null, PlayerAttribute.Speed, 6, 100m);

        Assert.Equal(new DateOnly(2026, 3, 10), late.Day);
    }

    [Fact]
    public void ASessionKnowsWhichOneOfTheDayItIs()
    {
        // Its own place in the day's allowance, so the count and the rows cannot drift: a
        // second session claiming a place already taken cannot be written, whatever a read
        // of the allowance said a moment before.
        var first = TrainingSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 10), DateTimeOffset.UnixEpoch,
            null, PlayerAttribute.Speed, 6, 100m, ordinal: 0);

        var second = TrainingSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 10), DateTimeOffset.UnixEpoch,
            null, PlayerAttribute.Speed, 6, 100m, ordinal: 1);

        Assert.Equal(0, first.Ordinal);
        Assert.Equal(1, second.Ordinal);
    }

    [Fact]
    public void ASessionCannotClaimAPlaceBeforeTheStartOfTheDay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 10), DateTimeOffset.UnixEpoch,
            null, PlayerAttribute.Speed, 6, 100m, ordinal: -1));
    }

    [Fact]
    public void ASessionWithNoClubToPayForItIsNotASession()
    {
        Assert.Throws<ArgumentException>(() => TrainingSession.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
            new DateOnly(2026, 3, 10), DateTimeOffset.UnixEpoch,
            null, PlayerAttribute.Speed, 6, 100m));
    }

    [Fact]
    public void AFreeSessionIsNotASession()
    {
        // The energy is what the man spent, and a session that cost him nothing is a button
        // that did nothing — which is a defect in the engine, not a cheap way to develop a
        // player.
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 10), DateTimeOffset.UnixEpoch,
            null, PlayerAttribute.Speed, energyCost: 0, fee: 100m));
    }
}
