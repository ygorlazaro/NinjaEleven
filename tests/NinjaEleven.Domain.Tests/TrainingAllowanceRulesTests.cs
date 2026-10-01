using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The two numbers a training day turns on: how many sessions a man has, and what one costs
/// the club.
/// </summary>
public class TrainingAllowanceRulesTests
{
    [Fact]
    public void AManPlayingHasOneSessionAndAManAtRestHasTwo()
    {
        // The whole rule in two lines. One on a matchday, two without: a rest day is a
        // doubling and not a holiday, because the day a first team does not play is the day
        // its reserve side does. The budget is the man's and not the club's, which is why
        // nothing here mentions a squad: a club-wide budget means the second man a manager
        // trains is told there is nothing left.
        Assert.Equal(1, TrainingRules.DailyBudget(hasMatch: true));
        Assert.Equal(2, TrainingRules.DailyBudget(hasMatch: false));

        Assert.Equal(TrainingRules.SessionsOnAMatchDay, TrainingRules.DailyBudget(hasMatch: true));
        Assert.Equal(TrainingRules.SessionsOnARestDay, TrainingRules.DailyBudget(hasMatch: false));
    }

    [Fact]
    public void ASessionCostsTheClubHalfOfTheWageOnTheContract()
    {
        // The fee is a share of a wage and not a number of its own, so a club's whole
        // development bill is a share of its wage bill and a manager can check one against
        // the other on the same screen.
        //
        // Half, and not a token: a fee small enough to be ignorable makes development a
        // decision nobody ever has to think about, and the wage bill it is a share of is the
        // one number a club is already watching.
        Assert.Equal(0.50m, TrainingRules.SessionFeeRate);
        Assert.Equal(6_000m, TrainingRules.SessionFee(12_000m));
    }

    [Fact]
    public void AContractWithNoWageIsTrainedForNothing()
    {
        // Zero, and not a refusal: a man on no contract is not a cost to anybody. The session
        // is refused further up, by the rule that only a contracted man has a club to spend an
        // allowance on, and this is here so that the arithmetic never has to invent a wage to
        // divide. It is the wage on the contract that is read, so a world whose contracts
        // carry no wage is a world whose development is free rather than one that is broken.
        Assert.Equal(0m, TrainingRules.SessionFee(0m));
    }

    [Fact]
    public void ASessionFeeIsRoundedToTheCentAndNotCarried()
    {
        // Money is money and not a fraction of it: a statement that carried fifteen figures
        // behind the comma would not add up to the balance printed on the same page, and a
        // fee of 0.015 is a fee nobody can pay.
        Assert.Equal(0.05m, TrainingRules.SessionFee(0.1m));
        Assert.Equal(6_172.84m, TrainingRules.SessionFee(12_345.67m));
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
            fee: 6_000m);

        Assert.Equal(new DateOnly(2026, 3, 10), session.Day);
        Assert.Equal(12, session.EnergyCost);

        // The fee is held, not recomputed: a session that re-derived its own price from the
        // man on the day it is read back would be a session that changed its mind about what
        // it cost whenever the man changed.
        Assert.Equal(6_000m, session.Fee);
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
        // of the allowance said a moment before. The place is the man's and not the club's,
        // which is the same width as the rule the count enforces.
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
