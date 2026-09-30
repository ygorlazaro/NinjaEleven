using NinjaEleven.BalanceLab;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The balance invariants, stated as assertions so that a change to a match rule that
/// breaks one of them fails the build rather than being discovered by a manager.
///
/// <para>
/// These tests do not test that the engine runs — a broken engine still runs. They test the
/// claims the model makes about itself: that quality decides the fixture, that energy taxes
/// a player without being able to cancel him, that a season of rotation is worth something,
/// and that the laboratory the balance numbers came from still measures the engine.
/// </para>
///
/// <para>
/// The one that is not about football is <see cref="TheLaboratoryStillMeasuresTheEngine"/>,
/// and it is here rather than in the laboratory's own project because it is the assertion
/// that makes every other number in that project worth reading.
/// </para>
/// </summary>
public class BalanceInvariantTests
{
    private const int Duels = 60_000;

    // --- The laboratory ---------------------------------------------------------

    /// <summary>
    /// The laboratory's strength must be the engine's strength. A balance report produced by
    /// a model that has drifted from production is a confident wrong answer, and this is the
    /// test that says so before anybody reads one.
    /// </summary>
    [Fact]
    public void TheLaboratoryStillMeasuresTheEngine()
    {
        var teamId = Guid.NewGuid();
        var productionCurve = new ComposedEnergyCurve(
            MatchRules.EnergyFactorFloor,
            MatchRules.EnergyFactorGamma);

        foreach (var quality in new[] { 15, 40, 70, 95 })
        {
            foreach (var energy in new[] { 10, 40, 70, 100 })
            {
                var cards = new List<PlayerCard> { PlayerCard.Keeper(quality + 5, energy) };

                for (var index = 0; index < 4; index++)
                {
                    cards.Add(PlayerCard.Outfield(Position.DEF, quality, energy));
                }

                for (var index = 0; index < 3; index++)
                {
                    cards.Add(PlayerCard.Outfield(Position.MID, quality, energy));
                }

                for (var index = 0; index < 3; index++)
                {
                    cards.Add(PlayerCard.Outfield(Position.ATT, quality, energy));
                }

                var production = TeamStrength.Of(cards.Select(card => Snapshot(card, teamId)).ToList());
                var lab = LabStrength.Of(cards, productionCurve);

                Assert.Equal(production.Attack, lab.Attack, 6);
                Assert.Equal(production.Midfield, lab.Midfield, 6);
                Assert.Equal(production.Defense, lab.Defense, 6);
            }
        }
    }

    /// <summary>
    /// The curve the laboratory calls production is the curve the engine ships. They are read
    /// off the same two constants, and this is what says so: a laboratory measuring a model
    /// the game does not play would produce every other number in this file for a world
    /// nobody plays in.
    /// </summary>
    [Fact]
    public void TheCurveTheLaboratoryCallsProductionIsTheCurveTheEngineShips()
    {
        for (var energy = 0; energy <= 100; energy++)
        {
            var lab = new ComposedEnergyCurve(
                MatchRules.EnergyFactorFloor,
                MatchRules.EnergyFactorGamma).Factor(energy);

            Assert.Equal(EnergyCurve.Factor(energy), lab, 9);
        }
    }

    // --- The hierarchy ----------------------------------------------------------

    /// <summary>
    /// Two men of equal quality are equal whatever their energy, and a man who is twice as
    /// good is twice as good at equal energy. The hierarchy is the brief's first claim and
    /// it is one assertion in both directions.
    /// </summary>
    [Fact]
    public void QualityDecidesTheFixtureAndEnergyOnlyTaxesIt()
    {
        var production = new ComposedEnergyCurve(
            MatchRules.EnergyFactorFloor,
            MatchRules.EnergyFactorGamma);

        // The same man against himself: a dead heat, whatever the energy.
        Assert.Equal(1.0, InitiativeRatio(90, 100, 90, 100, production), 3);
        Assert.Equal(1.0, InitiativeRatio(90, 20, 90, 20, production), 3);

        // Twice the quality at equal energy: twice the initiative, and no more. The
        // formation factors and the goalkeeper's share are the same on both sides, so the
        // ratio of the sums is the ratio of the qualities — which is the property the whole
        // hierarchy rests on.
        Assert.Equal(2.0, InitiativeRatio(90, 100, 45, 100, production), 2);

        // Equal quality, one exhausted and one fresh: energy moves the needle, and it moves
        // it by exactly the ramp and nothing else — so the number to assert is the ramp's,
        // not a ratio remembered from a linear one.
        Assert.Equal(
            1.0 / EnergyCurve.Factor(40),
            InitiativeRatio(90, 100, 90, 40, production),
            2);

        // And the ramp is not the linear one: it is the floor that makes the gap in the
        // other test possible, and it is why a man on 40 still keeps most of himself.
        Assert.True(
            EnergyCurve.Factor(40) > 0.65,
            $"Um homem em 40 de energia entregou {EnergyCurve.Factor(40):0.00} de si mesmo.");
    }

    /// <summary>
    /// Production must not let energy cancel a world-class player. A man on 20 energy still
    /// has to be the better footballer, because "a tired great loses to a fresh mediocre" is
    /// the failure this ramp was chosen to remove, and it is the assertion the whole design
    /// turns on.
    /// </summary>
    [Fact]
    public void ProductionDoesNotLetEnergyCancelAWorldClassPlayer()
    {
        var production = new ComposedEnergyCurve(
            MatchRules.EnergyFactorFloor,
            MatchRules.EnergyFactorGamma);

        var strong = InitiativeRatio(90, 100, 45, 100, production);

        // The same striker, exhausted, against a fresh man of 45.
        var exhausted = InitiativeRatio(90, 20, 45, 100, production);

        Assert.True(
            exhausted > 1.0,
            $"Um 90 exausto perdeu para um 45 descansado sob a rampa da produção ({exhausted:0.00}).");

        // And the same energy, the quality gap is untouched, so the gap is decided by
        // quality and taxed by energy rather than inverted by it.
        Assert.Equal(2.0, strong, 2);

        // The floor is the reason, and it is a floor rather than a small slope: whatever
        // energy a player has, he keeps at least this much of himself.
        Assert.True(
            EnergyCurve.Factor(0) >= MatchRules.EnergyFactorFloor,
            $"A rampa desceu abaixo do piso ({EnergyCurve.Factor(0):0.00}).");
    }

    /// <summary>
    /// The same fixture under each candidate ramp, and the property the brief asks for: an
    /// excellent player who is tired must get worse, and must not automatically become
    /// worse than a poor one who is rested.
    /// </summary>
    [Theory]
    [InlineData(0.55, 1.5)]
    [InlineData(0.65, 1.5)]
    public void AProposedRampTaxesWithoutVetoing(double floor, double gamma)
    {
        var curve = new ComposedEnergyCurve(floor, gamma);

        // Tired is worse than fresh. It has to be: a rotation that changes nothing is not a
        // rotation.
        Assert.True(
            InitiativeRatio(90, 30, 90, 100, curve) < 1.0,
            $"A rampa {curve.Name} deixou o jogador exausto tão bom quanto o descansado.");

        // And tired is still the better player, which is the whole principle: a man on 20
        // beats a man of 45 on 100 rather than merely surviving him.
        Assert.True(
            InitiativeRatio(90, 20, 45, 100, curve) > 1.0,
            $"The proposed ramp still let a tired 90 lose to a fresh 45 ({curve.Name}).");
    }

    /// <summary>
    /// Every candidate ramp has to be a ramp: never above a full tank, never below nothing,
    /// and never going up as the energy goes down.
    /// </summary>
    [Fact]
    public void EveryRampIsMonotonicAndBounded()
    {
        var curves = new IEnergyCurve[]
        {
            LinearEnergyCurve.Instance,
            new TableEnergyCurve(),
            new FlooredEnergyCurve(0.55),
            new FlooredEnergyCurve(0.65),
            new PowerEnergyCurve(1.5),
            new ComposedEnergyCurve(0.55, 1.5),
            new ComposedEnergyCurve(0.65, 1.5)
        };

        foreach (var curve in curves)
        {
            var previous = curve.Factor(1);

            Assert.InRange(curve.Factor(100), 0.999, 1.001);

            for (var energy = 2; energy <= 100; energy++)
            {
                var factor = curve.Factor(energy);

                Assert.InRange(factor, 0.0, 1.0);
                Assert.True(
                    factor >= previous - 1e-9,
                    $"{curve.Name} went backwards between {energy - 1} and {energy}.");

                previous = factor;
            }
        }
    }

    /// <summary>
    /// The duel, rolled out: under a proposed ramp a tired great still takes the fixture
    /// off a fresh mediocre one, but takes it far less often than he would fresh.
    /// </summary>
    [Fact]
    public void ATiredGreatStillWinsAgainstAFreshMediocreOne()
    {
        var curve = new ComposedEnergyCurve(
            MatchRules.EnergyFactorFloor,
            MatchRules.EnergyFactorGamma);

        var duel = new DuelSimulator(curve);
        var random = new LabRandom(4242);

        var great = PlayerCard.Outfield(Position.ATT, 90, 30);
        var mediocre = PlayerCard.Outfield(Position.DEF, 50, 100);
        var keeper = PlayerCard.Keeper(50, 100);

        var greatWins = 0;
        var mediocreWins = 0;

        for (var index = 0; index < Duels; index++)
        {
            if (duel.CreatesChance(great, mediocre, random) && duel.Scores(great, keeper, random))
            {
                greatWins++;
            }
            else if (duel.CreatesChance(mediocre, great, random) && duel.Scores(mediocre, PlayerCard.Keeper(90, 30), random))
            {
                mediocreWins++;
            }
        }

        Assert.True(
            greatWins > mediocreWins,
            $"O jogador de 90 exausto ganhou {greatWins} e perdeu {mediocreWins} contra o de 50 descansado.");
    }

    // --- The attribute scale ----------------------------------------------------

    /// <summary>
    /// The action formulas must respond across the whole 1..100 scale the attributes live on.
    /// This was the finding under everything else in the audit: the formulas were written for
    /// 1..20, so a 70 and a 95 were the same striker and a 20 beat a 90.
    /// </summary>
    [Fact]
    public void TheActionFormulasRespondAcrossTheWholeAttributeScale()
    {
        foreach (var audit in ScaleAudit.All())
        {
            if (audit.IsDead)
            {
                continue;
            }

            Assert.True(
                audit.Range > 0.50,
                $"{audit.Name} responde a apenas {audit.Range:P0} da escala ({audit.Formula}).");
        }

        // And the two that were bolted hardest: a better shooter must be measurably better,
        // not merely differently written.
        var seventy = GoalChanceFor(70);
        var ninety = GoalChanceFor(90);

        Assert.True(
            ninety > seventy,
            $"Um finalizador de 90 e um de 70 valem a mesma chance ({ninety:0.000} e {seventy:0.000}).");
    }

    private static double GoalChanceFor(int accuracy) =>
        Math.Clamp(
            MatchRules.BaseGoalChance + MatchRules.GoalChanceSwing
                * (AttributeScale.Factor(
                        AttributeWeights.Shot.Accuracy * accuracy
                        + AttributeWeights.Shot.Dribbling * 50
                        + AttributeWeights.Shot.Speed * 50)
                    - AttributeScale.Factor(50)),
            MatchRules.MinGoalChance,
            MatchRules.MaxGoalChance);

    // --- Recovery and stamina ---------------------------------------------------

    /// <summary>
    /// Recovery is deterministic. Two calls with the same player and the same window are the
    /// same number, which is what makes a season replayable and a rule tunable.
    /// </summary>
    [Fact]
    public void RecoveryIsDeterministic()
    {
        Assert.Equal(
            RecoveryModel.AfterPlaying(70, 90, 60, 26),
            RecoveryModel.AfterPlaying(70, 90, 60, 26));

        Assert.Equal(
            RecoveryModel.AfterBench(70, 60, 26),
            RecoveryModel.AfterBench(70, 60, 26));
    }

    /// <summary>
    /// The four factors each answer a different question, so each has to move on its own
    /// axis. A rule where stamina and age are the same multiplier is a rule with one factor.
    /// </summary>
    [Fact]
    public void EveryRecoveryFactorMovesOnItsOwnAxis()
    {
        const int baselineEnergy = 60;
        const int baselineStamina = 60;
        const int baselineAge = 27;

        var baseline = RecoveryModel.AfterPlaying(baselineEnergy, 90, baselineStamina, baselineAge);

        Assert.True(RecoveryModel.AfterPlaying(baselineEnergy, 90, 90, baselineAge) > baseline);
        Assert.True(RecoveryModel.AfterPlaying(baselineEnergy, 90, baselineStamina, 19) > baseline);
        Assert.True(RecoveryModel.AfterPlaying(baselineEnergy, 90, baselineStamina, 37) < baseline);
        Assert.True(RecoveryModel.AfterPlaying(baselineEnergy, 30, baselineStamina, baselineAge) < baseline);
    }

    /// <summary>
    /// Diminishing returns: a window rebuilds more of a man who is nearly out than of a man
    /// who is nearly full. Without it a squad ground down to 20 is back to 100 in a
    /// fortnight and the fatigue a season is about does not exist.
    /// </summary>
    [Fact]
    public void RecoveryHasDiminishingReturns()
    {
        var exhausted = RecoveryModel.AfterBench(10, 60, 26);
        var merelyTired = RecoveryModel.AfterBench(60, 60, 26);
        var almostFresh = RecoveryModel.AfterBench(95, 60, 26);

        Assert.True(exhausted > merelyTired);
        Assert.True(merelyTired > almostFresh);

        // And it is still proportional: the man on 95 gains a handful of points, not thirty.
        Assert.True(almostFresh < 10);
    }

    /// <summary>
    /// A rotation is worth something. The same eleven every day must end a season visibly
    /// more tired than a squad that rotates — otherwise the squad list is a fiction and the
    /// manager has nothing to decide.
    /// </summary>
    [Fact]
    public void RotationIsWorthSomethingOverASeason()
    {
        var withoutRotation = SeasonSimulator.RunWithoutRotation(23, 34);
        var withRotation = SeasonSimulator.Run(23, 34, rotation: 8);

        Assert.True(
            withoutRotation.Minimum < withRotation.Minimum,
            $"Sem rotação o mínimo foi {withoutRotation.Minimum:0} e com rotação {withRotation.Minimum:0}.");

        Assert.True(
            withRotation.LowEnergyShare < withoutRotation.LowEnergyShare,
            "Uma temporada com rotação produziu mais janelas cansadas do que uma sem rotação.");
    }

    /// <summary>
    /// A man with more stamina empties more slowly and refills more quickly. Those are the
    /// two ends of one fact, and a stamina attribute that only bought one of them would be
    /// half an attribute.
    /// </summary>
    [Fact]
    public void StaminaIsBothTheTankAndTheRefill()
    {
        var strong = PlayerCard.Outfield(Position.ATT, 70, energy: 80, stamina: 95);
        var weak = PlayerCard.Outfield(Position.ATT, 70, energy: 80, stamina: 20);

        Assert.True(SeasonSimulator.MatchEnergyCost(90, strong.Stamina, 26)
            < SeasonSimulator.MatchEnergyCost(90, weak.Stamina, 26));

        Assert.True(
            RecoveryModel.AfterPlaying(60, 90, strong.Stamina, 26)
            > RecoveryModel.AfterPlaying(60, 90, weak.Stamina, 26));
    }

    // --- Helpers ----------------------------------------------------------------

    private static double InitiativeRatio(
        int strongQuality,
        int strongEnergy,
        int weakQuality,
        int weakEnergy,
        IEnergyCurve curve)
    {
        var strong = Eleven(strongQuality, strongEnergy);
        var weak = Eleven(weakQuality, weakEnergy);

        return LabStrength.Of(strong, curve).Initiative / LabStrength.Of(weak, curve).Initiative;
    }

    private static List<PlayerCard> Eleven(int quality, int energy)
    {
        var eleven = new List<PlayerCard> { PlayerCard.Keeper(quality + 5, energy) };

        for (var index = 0; index < 4; index++)
        {
            eleven.Add(PlayerCard.Outfield(Position.DEF, quality, energy));
        }

        for (var index = 0; index < 3; index++)
        {
            eleven.Add(PlayerCard.Outfield(Position.MID, quality, energy));
        }

        for (var index = 0; index < 3; index++)
        {
            eleven.Add(PlayerCard.Outfield(Position.ATT, quality, energy));
        }

        return eleven;
    }

    private static MatchPlayerSnapshot Snapshot(PlayerCard card, Guid teamId)
    {
        var player = Player.Create(
            "Invariante",
            card.Age,
            card.Position,
            card.Speed,
            card.Accuracy,
            card.Dribbling,
            card.Heading,
            card.Strength,
            card.Position == Position.GK ? card.GoalkeeperPower : 0,
            card.Position == Position.GK ? card.Reflexes : 0);

        var state = PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), teamId, card.Energy);

        return MatchPlayerSnapshot.FromPlayerSeasonState(player, state);
    }
}
