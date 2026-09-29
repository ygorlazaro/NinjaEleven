using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What a match costs a player and what a window of rest gives him back, measured against
/// the two things that decide both: how long he was on the pitch, and how old he is.
/// </summary>
public class EnergyRecoveryAndPenaltyTests
{
    [Fact]
    public void AManWhoNeverLeftThePitchHasPlayedEveryMinuteOfTheMatch()
    {
        var player = PlayerAt(energy: 90);

        Assert.Equal(MatchRules.MinutesInAMatch, player.MinutesPlayed(MatchRules.MinutesInAMatch));
    }

    [Fact]
    public void AManWhoNeverWentOnHasNoMinutesToRecoverFrom()
    {
        // He sat on the bench with both stamps untouched, and the difference of the two
        // would be a full match he never played. The recovery is measured against the
        // minutes, so this is the difference between a day off and a game.
        var benched = PlayerAt(energy: 90);
        benched.PlayedInMatch = false;

        Assert.Equal(0, benched.MinutesPlayed(MatchRules.MinutesInAMatch));
    }

    [Fact]
    public void ASubstituteIsCreditedWithTheMinutesHeWasOutThereAndNoMore()
    {
        var outgoing = PlayerAt(energy: 90);
        var incoming = PlayerAt(energy: 90);
        var state = StateWith(outgoing, incoming);

        state.Minute = 70;

        MatchSubstitution.Swap(state, home: true, outgoing, incoming);

        Assert.Equal(70, incoming.EnteredAtMinute);
        Assert.Equal(70, outgoing.LeftAtMinute);

        // The man who came off played seventy minutes and the man who came on played twenty,
        // and the recovery at the end of the match is the only thing that can tell them apart.
        Assert.Equal(70, outgoing.MinutesPlayed(90));
        Assert.Equal(20, incoming.MinutesPlayed(90));
    }

    [Fact]
    public void ASubstitutionMovesNobodyEnergy()
    {
        // The eleven, the bench and the opposition, all read before and after the change.
        // A swap is two men changing places; energy is what ninety minutes costs a player,
        // and the ninety minutes are the ticks. A change of shape is not a tick, so no man on
        // either pitch is a point more tired for it — and the man who comes on arrives with
        // the energy he was built with rather than a number the change invented for him.
        var outgoing = PlayerAt(energy: 62, position: Position.ATT);
        var incoming = PlayerAt(energy: 95, position: Position.DEF);
        var teammate = PlayerAt(energy: 71, position: Position.MID);
        var keeper = PlayerAt(energy: 80, position: Position.GK);
        var opponent = PlayerAt(energy: 58, position: Position.ATT);
        var reserve = PlayerAt(energy: 99, position: Position.MID);

        var state = StateWith(outgoing, incoming, extra: [teammate, keeper], opponent: opponent, reserve: [reserve]);
        state.Minute = 70;

        var before = EveryoneIn(state).ToDictionary(pair => pair.Key, pair => pair.Value);

        MatchSubstitution.Swap(state, home: true, outgoing, incoming);

        foreach (var (player, energy) in EveryoneIn(state))
        {
            Assert.Equal(before[player], energy, 6);
        }
    }

    [Fact]
    public void APlayerSentOffAtThirtyHasPlayedThirtyMinutes()
    {
        var player = PlayerAt(energy: 90);

        player.SendOff(30);

        Assert.Equal(30, player.MinutesPlayed(90));
    }

    [Fact]
    public void APlayerWhoWalksOffWithASeriousKnockHasPlayedUntilHeWalkedOff()
    {
        var player = PlayerAt(energy: 90);

        player.Injure(Injury.Grave, matchesOut: 3, minute: 55);

        Assert.Equal(55, player.MinutesPlayed(90));
    }

    [Fact]
    public void ALightKnockIsNotAMinuteOffThePitch()
    {
        var player = PlayerAt(energy: 90);

        player.Injure(Injury.Light, minute: 55);

        Assert.Equal(90, player.MinutesPlayed(90));
    }

    [Fact]
    public void APlayerWhoLeftTwiceIsCreditedWithTheFirstTimeHeLeft()
    {
        var player = PlayerAt(energy: 90);

        player.SendOff(30);
        player.LeaveThePitchAt(80);

        // He was on the bench when the second thing happened to him, and the bench is not
        // playing. A card for a man who is no longer in the match must not hand him the
        // minutes between leaving and being sent off.
        Assert.Equal(30, player.MinutesPlayed(90));
    }

    [Fact]
    public void AFullMatchIsWorthMoreOfARecoveryThanTheLastFiveMinutesOfIt()
    {
        var full = RecoveryOf(minutes: 90, age: 26);
        var cameo = RecoveryOf(minutes: 5, age: 26);

        Assert.True(full > cameo, $"a full match recovered {full} and a five-minute cameo recovered {cameo}.");
    }

    [Fact]
    public void ARecoveryIsProportionalToTheTimeOnThePitch()
    {
        var half = RecoveryOf(midpoints: true, minutes: 45, age: 26);
        var full = RecoveryOf(midpoints: true, minutes: 90, age: 26);

        // Forty-five minutes of a match earns about half of what ninety minutes earns, give
        // or take the rounding of a number between three and seven: the band is the ceiling
        // of the recovery, not the recovery itself.
        Assert.InRange((double)half / full, 0.45, 0.75);
    }

    [Fact]
    public void ACameoIsNotNothing()
    {
        Assert.True(RecoveryOf(minutes: 2, age: 26) >= MatchRules.MinRecoveryForACameo);
    }

    [Fact]
    public void AYoungManIsGivenBackMoreOfAWindowThanAnOldOne()
    {
        var young = RecoveryOf(minutes: 90, age: 20);
        var old = RecoveryOf(minutes: 90, age: 35);

        Assert.True(young > old, $"twenty recovered {young} and thirty-five recovered {old}.");

        // And the same is true of a man who sat the window out on the bench: he was there
        // for the whole of it, and thirty-four is not twenty-one.
        Assert.True(RecoveryOnTheBench(age: 20) > RecoveryOnTheBench(age: 35));
    }

    [Fact]
    public void AWindowThatIsNotAMatchIsNotScaledByMinutes()
    {
        // A man who did not travel has no minutes to be paid for, and the band is the band.
        var rested = EnergyRecoveryRules.Recovery(WindowEffort.NoMatch, 0, age: 26, new FixedRandom());

        Assert.InRange(rested, EnergyRecoveryRules.MinFullRest, EnergyRecoveryRules.MaxFullRest);
    }

    [Fact]
    public void RestingIsStillWorthMoreThanPlayingEvenForTheSameMan()
    {
        var played = EnergyRecoveryRules.Recovery(
            WindowEffort.Played(true), MatchRules.MinutesInAMatch, 26, new FixedRandom());
        var rested = EnergyRecoveryRules.Recovery(
            WindowEffort.SatOut(true), 0, 26, new FixedRandom());

        // The two bands do not overlap and the multipliers cannot make them meet: a man who
        // sat a whole match out is never worse off than a man who played all of it.
        Assert.True(
            rested > played,
            $"sitting it out recovered {rested} and playing it recovered {played}.");
    }

    [Fact]
    public void TirednessIsReadAgainstAReferenceAndNotAgainstZero()
    {
        Assert.Equal(0, PlayerMetric.Fatigue(PlayerAt(energy: MatchRules.ReferenceEnergy)), 6);
        Assert.True(PlayerMetric.Fatigue(PlayerAt(energy: 20)) > 0, "A spent man is tired.");
        Assert.True(PlayerMetric.Fatigue(PlayerAt(energy: 100)) < 0, "A fresh man is fresher than the reference.");
    }

    [Fact]
    public void ATiredTakerConvertsWorseThanAFreshOneWithTheSameAttributes()
    {
        var tired = PlayerAt(energy: 25, position: Position.ATT);
        var fresh = PlayerAt(energy: 100, position: Position.ATT);
        var keeper = PlayerAt(energy: MatchRules.ReferenceEnergy, position: Position.GK);

        Assert.True(
            MatchEngine.PenaltyConversion(tired, keeper) < MatchEngine.PenaltyConversion(fresh, keeper),
            "the same man, ninety minutes apart, is not the same penalty.");
    }

    [Fact]
    public void ATiredKeeperSavesWorseThanAFreshOne()
    {
        var taker = PlayerAt(energy: MatchRules.ReferenceEnergy, position: Position.ATT);
        var tiredKeeper = PlayerAt(energy: 25, position: Position.GK);
        var freshKeeper = PlayerAt(energy: 100, position: Position.GK);

        Assert.True(
            MatchEngine.PenaltyConversion(taker, tiredKeeper) > MatchEngine.PenaltyConversion(taker, freshKeeper),
            "a keeper at the end of ninety minutes keeps out less than a keeper at the start of it.");
    }

    [Fact]
    public void AFullStrengthPenaltyIsExactlyWhatItWasBeforeTirednessExisted()
    {
        // Both men on the reference: the tiredness terms are zero, so a penalty between two
        // healthy players is decided by the attributes alone and the number has not moved.
        // The two men here are identical apart from the gloves, so the finishing term and
        // the saving term cancel and what is left is the base conversion.
        var taker = PlayerAt(energy: MatchRules.ReferenceEnergy, position: Position.ATT);
        var keeper = PlayerAt(energy: MatchRules.ReferenceEnergy, position: Position.GK);

        Assert.Equal(0.78, MatchEngine.PenaltyConversion(taker, keeper), 6);
    }

    [Fact]
    public void ATiredPenaltyIsStillAPenaltyAndNotARoulette()
    {
        var taker = PlayerAt(energy: 0, position: Position.ATT);
        var keeper = PlayerAt(energy: 0, position: Position.GK);

        // Two exhausted men and one penalty: the number has to stay inside the band, or a
        // tired taker stops being a taker and a tired keeper starts winning shootouts.
        Assert.InRange(
            MatchEngine.PenaltyConversion(taker, keeper),
            0.60,
            0.92);
    }

    [Fact]
    public void APenaltyWithNobodyInGoalIsTheTakerAndHisTirednessAlone()
    {
        var taker = PlayerAt(energy: MatchRules.ReferenceEnergy, position: Position.ATT);
        var spentTaker = PlayerAt(energy: 20, position: Position.ATT);

        // Nobody in goal is not a free penalty for the taker to stroll in: the saving term is
        // the only thing that disappears, and a taker at the end of ninety minutes still
        // converts worse than one at the start of it.
        Assert.Equal(0.78, MatchEngine.PenaltyConversion(taker, null), 6);
        Assert.True(MatchEngine.PenaltyConversion(spentTaker, null) < MatchEngine.PenaltyConversion(taker, null));
    }

    /// <summary>Every player the match is holding, whoever he is playing for or sitting out.</summary>
    private static IEnumerable<KeyValuePair<MatchPlayerSnapshot, double>> EveryoneIn(MatchState state) =>
        state.HomeLineup
            .Concat(state.HomeBench)
            .Concat(state.AwayLineup)
            .Concat(state.AwayBench)
            .Select(player => new KeyValuePair<MatchPlayerSnapshot, double>(player, player.PreciseEnergy));

    /// <summary>
    /// A match with a home side and an away side. One man on the pitch and one on the bench
    /// is the shape most of these tests need; the rest of the pitch, the opposition and the
    /// rest of the bench are there for the tests that are about everybody rather than about
    /// one pair.
    /// </summary>
    private static MatchState StateWith(
        MatchPlayerSnapshot onPitch,
        MatchPlayerSnapshot onBench,
        IReadOnlyList<MatchPlayerSnapshot>? extra = null,
        MatchPlayerSnapshot? opponent = null,
        IReadOnlyList<MatchPlayerSnapshot>? reserve = null)
    {
        var home = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 50);
        var away = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var homeLineup = new List<MatchPlayerSnapshot> { onPitch };
        if (extra is not null)
        {
            homeLineup.AddRange(extra);
        }

        var awayLineup = new List<MatchPlayerSnapshot>
        {
            opponent ?? PlayerAt(energy: 90, position: Position.GK)
        };

        var homeBench = new List<MatchPlayerSnapshot> { onBench };
        if (reserve is not null)
        {
            homeBench.AddRange(reserve);
        }

        var context = new MatchContext(
            Guid.NewGuid(),
            home,
            away,
            homeLineup,
            awayLineup,
            homeBench,
            [],
            new FixedRandom());

        return new MatchState(context);
    }

    private static int RecoveryOf(int minutes, int age, bool midpoints = false) =>
        EnergyRecoveryRules.Recovery(
            WindowEffort.Played(true), minutes, age, new FixedRandom(midpoints));

    private static int RecoveryOnTheBench(int age) =>
        EnergyRecoveryRules.Recovery(WindowEffort.SatOut(true), 0, age, new FixedRandom());

    private static MatchPlayerSnapshot PlayerAt(
        int energy,
        Position position = Position.MID,
        int age = 26)
    {
        // The attributes sit on the reference, so a test about tiredness and minutes is not
        // also a test about a man who happens to be worse than average.
        var skill = (int)MatchRules.ReferenceAttribute;

        var player = Player.Create(
            "Test Player",
            age,
            position,
            speed: skill,
            accuracy: skill,
            dribbling: skill,
            heading: skill,
            strength: skill,
            goalkeeperPower: skill,
            reflexes: skill);

        var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), Guid.NewGuid(), energy);

        var snapshot = MatchPlayerSnapshot.FromPlayerSeasonState(player, state);

        // Everybody this helper hands out is a man who was on the pitch, which is the flag
        // the state sets for the eleven at kick-off. A man who watched from the bench is
        // built by hand in the one test that is about him.
        snapshot.PlayedInMatch = true;

        return snapshot;
    }

    /// <summary>
    /// A source that always returns the bottom of every band, so a test can compare the two
    /// multipliers without the draw deciding the result. The recovery asks for a number
    /// between the two ends of a band; the first it asks for is the lower one every time.
    /// </summary>
    private sealed class FixedRandom(bool midpoints = false) : IRandomSource
    {
        public int Next(int minValue, int maxValue) =>
            midpoints ? (minValue + maxValue) / 2 : minValue;

        public int Next() => 0;

        public int Next(int maxValue) => 0;

        public double NextDouble() => 0.0;
    }
}
