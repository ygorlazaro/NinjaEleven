using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What a body has in the tank, and what the minutes a man was on the pitch are worth.
///
/// <para>
/// These are the two halves of the same claim. Stamina is the only attribute that is about
/// the ninety minutes rather than about the game, and the minutes are the only measurement
/// that can tell a starter taken off at the sixtieth from a substitute who came on at the
/// sixtieth. Neither means anything on its own: a stamina that cost nothing and a recovery
/// that ignored the minutes are the same bug seen from two sides.
/// </para>
/// </summary>
public class StaminaAndMinutesTests
{
    // --- Stamina is both the tank and the refill ---------------------------------

    /// <summary>
    /// A man with more in the tank empties more slowly. That is the tank, and it is the
    /// reason a low-stamina player is a decision a manager has to make.
    /// </summary>
    [Fact]
    public void MoreStaminaEmptiesMoreSlowly()
    {
        Assert.True(StaminaRules.TankCost(95) < StaminaRules.TankCost(50));
        Assert.True(StaminaRules.TankCost(50) < StaminaRules.TankCost(5));
    }

    /// <summary>
    /// And the same man puts back more of what the match took. It is the other end of one
    /// fact: a body that empties slowly is a body that fills quickly, so a stamina that only
    /// bought one of the two would be half an attribute — and a player who is hard to tire
    /// and slow to mend is worse than either a tireless one or a durable one.
    /// </summary>
    [Fact]
    public void MoreStaminaMendsMoreQuickly()
    {
        Assert.True(StaminaRules.Refill(95) > StaminaRules.Refill(50));
        Assert.True(StaminaRules.Refill(50) > StaminaRules.Refill(5));
    }

    /// <summary>
    /// The reference is the middle of the scale and costs neither more nor less than one
    /// unit of either. A stamina rule that taxed the average player would be a rule that
    /// quietly taxed half the world.
    /// </summary>
    [Fact]
    public void TheAverageBodyPaysNeitherTaxNorBonus()
    {
        Assert.Equal(1.0, StaminaRules.TankCost(Player.StaminaReference), 6);
        Assert.Equal(1.0, StaminaRules.Refill(Player.StaminaReference), 6);
    }

    /// <summary>
    /// Both ends of the scale must be reachable. A stamina whose extremes were the same
    /// number would be a column.
    /// </summary>
    [Fact]
    public void BothEndsOfTheScaleAreReachable()
    {
        Assert.True(StaminaRules.TankCost(0) > StaminaRules.TankCost(100));
        Assert.True(StaminaRules.Refill(0) < StaminaRules.Refill(100));

        Assert.InRange(StaminaRules.TankCost(0), 1.0, 1.5);
        Assert.InRange(StaminaRules.Refill(100), 1.0, 1.5);
    }

    /// <summary>
    /// A stamina of no endurance must still be able to complete a match. The drain has a
    /// floor for the same reason: a player who cannot finish a game is not a tired player,
    /// he is an unavailable one, and that is a different design.
    /// </summary>
    [Fact]
    public void ANobodyOfAStaminaStillFinishesTheMatch()
    {
        Assert.InRange(StaminaRules.TankCost(0), 1.0, 1.5);

        var frail = Eleven(index => index == 0 ? Position.GK : Position.DEF, stamina: 0);
        var opponent = Eleven(index => index == 0 ? Position.GK : Position.ATT, stamina: Player.StaminaReference);

        var (state, engine) = FullMatch(frail, opponent, seed: 7);
        RunFullMatch(engine, state);

        Assert.All(
            frail.Concat(opponent),
            player => Assert.True(
                player.Energy >= MatchRules.EnergyFloorDuringMatch,
                $"Um jogador de stamina 0 terminou a partida em {player.Energy}."));
    }

    /// <summary>
    /// Stamina must reach the drain. A rule that priced everything and cost nothing would be
    /// a number on a column and not a fact about the match.
    /// </summary>
    [Fact]
    public void StaminaChangesWhatAMatchCosts()
    {
        var positions = (int index) => index == 0 ? Position.GK : Position.ATT;

        var durable = Eleven(positions, stamina: 95);
        var frail = Eleven(positions, stamina: 5);
        var opponent = Eleven(index => index == 0 ? Position.GK : Position.DEF, stamina: Player.StaminaReference);

        var (durableState, durableEngine) = FullMatch(durable, opponent, seed: 7);
        RunFullMatch(durableEngine, durableState);

        var (frailState, frailEngine) = FullMatch(frail, opponent, seed: 7);
        RunFullMatch(frailEngine, frailState);

        var durableCost = durable.Average(player => 100 - player.Energy);
        var frailCost = frail.Average(player => 100 - player.Energy);

        Assert.True(
            frailCost > durableCost,
            $"O elenco de stamina 5 perdeu {frailCost:0.0} e o de stamina 95 perdeu {durableCost:0.0}.");
    }

    /// <summary>
    /// Stamina must reach the recovery too, on the window he did not play in as well as the
    /// one he did. It is a fact about the body rather than about the window, so a man who
    /// sat out and a man who played are both mended at his own rate.
    /// </summary>
    [Fact]
    public void StaminaChangesWhatAWindowGivesBack()
    {
        var random = new BandBottomRandom();

        var durable = EnergyRecoveryRules.Recovery(
            WindowEffort.Played(true), 90, 26, random, stamina: 95);

        var frail = EnergyRecoveryRules.Recovery(
            WindowEffort.Played(true), 90, 26, random, stamina: 5);

        Assert.True(durable > frail);

        // And on the bench, where nothing was taken out of him either.
        var restedDurable = EnergyRecoveryRules.Recovery(
            WindowEffort.SatOut(true), 0, 26, random, stamina: 95);

        var restedFrail = EnergyRecoveryRules.Recovery(
            WindowEffort.SatOut(true), 0, 26, random, stamina: 5);

        Assert.True(restedDurable > restedFrail);
    }

    /// <summary>
    /// Recovery is deterministic, and stamina is part of it. A rotation decided on a
    /// stamina a replayed match would read differently is a decision that was not decided.
    /// </summary>
    [Fact]
    public void StaminaDoesNotMakeRecoveryRandom()
    {
        var first = EnergyRecoveryRules.Recovery(
            WindowEffort.Played(true), 65, 24, new BandBottomRandom(), stamina: 37);

        var second = EnergyRecoveryRules.Recovery(
            WindowEffort.Played(true), 65, 24, new BandBottomRandom(), stamina: 37);

        Assert.Equal(first, second);
    }

    // --- The minutes are persisted -----------------------------------------------

    /// <summary>
    /// The written line carries the minutes, measured to the last minute of the match. It is
    /// the only thing that separates a man who played from the whistle to the whistle from a
    /// substitute who came on at the same minute he was taken off.
    /// </summary>
    [Fact]
    public void TheWrittenLineCarriesTheMinutesHeActuallyPlayed()
    {
        var played = PlayerAt(energy: 80, played: true);
        var line = MatchPlayerStatistics.Create(
            Guid.NewGuid(), played.PlayerId, Guid.NewGuid(), Guid.NewGuid());

        line.ApplyFrom(played, started: true, finalMinute: MatchRules.MinutesInAMatch);

        Assert.Equal(MatchRules.MinutesInAMatch, line.MinutesPlayed);
    }

    /// <summary>
    /// A man who never went on has no minutes, whatever the stamps say, and the written line
    /// agrees with the recovery rather than being a second answer to the same question.
    /// </summary>
    [Fact]
    public void AManOnTheBenchAllMatchIsWrittenWithNoMinutes()
    {
        var benched = PlayerAt(energy: 80);
        benched.PlayedInMatch = false;

        var line = MatchPlayerStatistics.Create(
            Guid.NewGuid(), benched.PlayerId, Guid.NewGuid(), Guid.NewGuid());

        line.ApplyFrom(benched, started: false, wasOnBenchUnused: true, finalMinute: 90);

        Assert.Equal(0, line.MinutesPlayed);
        Assert.True(line.WasOnBenchUnused);
    }

    /// <summary>
    /// The column and the snapshot must be the same number. They are read by two different
    /// screens — a profile's history and a season's fatigue — and a disagreement between
    /// them is a season that cannot be reconciled with itself.
    /// </summary>
    [Fact]
    public void TheWrittenMinutesAreTheMinutesTheRecoveryWasMeasuredAgainst()
    {
        var outgoing = PlayerAt(energy: 80, position: Position.ATT, played: true);
        var incoming = PlayerAt(energy: 95, position: Position.DEF);
        var state = SwapState(outgoing, incoming);

        state.Minute = 70;
        MatchSubstitution.Swap(state, home: true, outgoing, incoming);

        var offLine = MatchPlayerStatistics.Create(
            Guid.NewGuid(), outgoing.PlayerId, Guid.NewGuid(), Guid.NewGuid());

        var onLine = MatchPlayerStatistics.Create(
            Guid.NewGuid(), incoming.PlayerId, Guid.NewGuid(), Guid.NewGuid());

        offLine.ApplyFrom(outgoing, started: true, finalMinute: MatchRules.MinutesInAMatch);
        onLine.ApplyFrom(incoming, started: false, finalMinute: MatchRules.MinutesInAMatch);

        Assert.Equal(outgoing.MinutesPlayed(MatchRules.MinutesInAMatch), offLine.MinutesPlayed);
        Assert.Equal(incoming.MinutesPlayed(MatchRules.MinutesInAMatch), onLine.MinutesPlayed);

        // Seventy and twenty: the two men the appearance flags alone could not tell apart.
        Assert.Equal(70, offLine.MinutesPlayed);
        Assert.Equal(20, onLine.MinutesPlayed);
    }

    // --- Helpers ----------------------------------------------------------------

    /// <summary>
    /// A player with every attribute on the reference, so a test about stamina and minutes
    /// is not also a test about a man who happens to be better at one thing than another.
    /// </summary>
    private static MatchPlayerSnapshot PlayerAt(
        int energy,
        Position position = Position.MID,
        int age = 26,
        int stamina = Player.StaminaReference,
        bool played = false)
    {
        var skill = (int)AttributeScale.Reference;

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
            reflexes: skill,
            stamina: stamina);

        var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), Guid.NewGuid(), energy);

        var snapshot = MatchPlayerSnapshot.FromPlayerSeasonState(player, state);

        // The engine stamps this as a match opens; a test that wants a man who played has to
        // say so, because a snapshot nobody started is a man who never went on.
        snapshot.PlayedInMatch = played;

        return snapshot;
    }

    private static void RunFullMatch(MatchEngine engine, MatchState state)
    {
        engine.Initialize(state, 0).ToList();

        for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
        {
            if (state.HalfTimePauseActive)
            {
                engine.ContinueSecondHalf(state);
            }

            engine.Tick(state).ToList();
        }
    }

    /// <summary>
    /// Two full elevens, both on the reference energy, with one of them given a stamina. The
    /// engine drives the match from the whistle, so what the two sides finish on is the only
    /// honest measure of what a tank cost them.
    /// </summary>
    private static (MatchState State, MatchEngine Engine) FullMatch(
        IEnumerable<MatchPlayerSnapshot> home,
        IEnumerable<MatchPlayerSnapshot> away,
        int seed)
    {
        var context = new MatchContext(
            Guid.NewGuid(),
            new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 50),
            new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50),
            home.ToList(),
            away.ToList(),
            [],
            [],
            new DeterministicRandomSource(seed));

        return (new MatchState(context), new MatchEngine(new DeterministicRandomSource(seed)));
    }

    private static List<MatchPlayerSnapshot> Eleven(Func<int, Position> position, int stamina) =>
        Enumerable.Range(0, 11)
            .Select(index => PlayerAt(energy: 100, position: position(index), stamina: stamina, played: true))
            .ToList();

    /// <summary>
    /// A state holding one man on the pitch and one on the bench, which is all a swap needs
    /// and all a swap must not disturb.
    /// </summary>
    private static MatchState SwapState(MatchPlayerSnapshot outgoing, MatchPlayerSnapshot incoming)
    {
        var home = new TeamInfo(Guid.NewGuid(), "Home United", "HU", "#FF0000", "#FFFFFF", 50);
        var away = new TeamInfo(Guid.NewGuid(), "Away City", "AC", "#0000FF", "#FFFFFF", 50);

        var context = new MatchContext(
            Guid.NewGuid(),
            home,
            away,
            new List<MatchPlayerSnapshot> { outgoing },
            new List<MatchPlayerSnapshot> { PlayerAt(energy: 90, position: Position.GK) },
            new List<MatchPlayerSnapshot> { incoming },
            [],
            new BandBottomRandom());

        return new MatchState(context);
    }

    /// <summary>
    /// A source that always returns the bottom of every band, so a test comparing two
    /// multipliers is not also a test of where in the band the draw landed.
    /// </summary>
    private sealed class BandBottomRandom : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;

        public int Next() => 0;

        public int Next(int maxValue) => 0;

        public double NextDouble() => 0.0;
    }
}
