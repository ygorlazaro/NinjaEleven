using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// The audit of the engine's own action formulas, swept over the scale the attributes
/// actually live on.
///
/// <para>
/// The complaint that started this was "energy has too much weight". The laboratory found the
/// reason before it found the cure: <b>quality has almost no weight anywhere else</b>. The
/// engine's action formulas were written when an attribute was a number from 1 to 20, and
/// they still were — the constants said so — but <c>Player.Create</c> clamps attributes to
/// 1..100 and the seeder draws them from 1..100. Every formula that divided by a 20-point span
/// and clamped the result therefore clamped at its ceiling for every player in the world.
/// </para>
///
/// <para>
/// That is why energy dominated: it multiplied inside <c>TeamStrength</c>, which was one of
/// the very few places a raw 1..100 attribute was still read linearly, so the only live path
/// for quality was the one energy was standing in.
/// </para>
///
/// <para>
/// This class sweeps each formula and reports the interval of the attribute scale over which
/// it actually changes. A formula whose live interval is empty is dead code wearing a
/// constant's clothes — and every formula the audit names now reads through
/// <see cref="AttributeScale"/> and <see cref="AttributeWeights"/>, so what it reports is the
/// engine's answer rather than a copy of it.
/// </para>
/// </summary>
public static class ScaleAudit
{
    /// <summary>
    /// The band of the 1..100 scale over which a formula is neither on its floor nor on its
    /// ceiling, and how much of the scale that band covers.
    /// </summary>
    /// <remarks>
    /// "Live" is not "moves": a formula can move a little at 20 and then be bolted at its
    /// ceiling from 60 upwards, and a player at 95 is then rated identically to a player at
    /// 65. What matters for the balance argument is the band where a difference between two
    /// players is still a difference, which is the band between the two clamps the formula
    /// declares for itself.
    /// </remarks>
    public readonly record struct Sensitivity(
        string Name,
        string Formula,
        int Low,
        int High,
        double Range)
    {
        /// <summary>Whether the formula is bolted for the whole world and moves for nobody.</summary>
        public bool IsDead => High <= Low;
    }

    /// <summary>
    /// Sweeps a formula over the 1..100 scale and reports the band in which its output is
    /// strictly inside the clamps it declares.
    ///
    /// <para>
    /// A clamp passed as null means the formula has none on that side, and then the whole
    /// scale is live — which is the truth about <c>TeamStrength</c>, the one formula in the
    /// engine that reads a raw 1..100 attribute without ever bolting it, and therefore the
    /// one place a difference in quality still reaches the match.
    /// </para>
    /// </summary>
    private static Sensitivity Sweep(
        string name,
        string formula,
        double? floor,
        double? ceiling,
        Func<int, double> read)
    {
        var low = -1;
        var high = -1;

        for (var value = 1; value <= 100; value++)
        {
            var result = read(value);

            if (floor is { } floorValue && result <= floorValue + 1e-9)
            {
                continue;
            }

            if (ceiling is { } ceilingValue && result >= ceilingValue - 1e-9)
            {
                continue;
            }

            if (low < 0)
            {
                low = value;
            }

            high = value;
        }

        if (low < 0)
        {
            return new Sensitivity(name, formula, 0, 0, 0);
        }

        return new Sensitivity(name, formula, low, high, (high - low + 1) / 100.0);
    }

    /// <summary>
    /// <c>AttributeScale.Factor</c>, which is what every action formula now reads a player
    /// through. Sweeping it is sweeping the scale repair itself: a factor live over the whole
    /// of 1..100 means two players twenty points apart are the same distance apart wherever
    /// they are on the scale.
    /// </summary>
    public static Sensitivity SkillFactor() =>
        Sweep("Escala de atributos",
            "(atributo − 50) / 45, clamp(−1, 1)",
            -1.0, 1.0,
            quality => AttributeScale.Factor(quality));

    /// <summary>
    /// The dribble duel inside <c>SimulateAttackSequence</c>, through the shared
    /// <c>DuelChance</c> the engine rolls it with.
    /// </summary>
    public static Sensitivity DribbleDuel()
    {
        const int marker = 50;

        return Sweep("Duelo de drible",
            "0.56 + 0.40 × (drible − marcador), clamp(0.08, 0.94)",
            MatchRules.MinDuelChance, MatchRules.MaxDuelChance,
            dribbling => Chance(dribbling, marker));
    }

    private static double Chance(int dribbling, int marker) =>
        Math.Clamp(
            MatchRules.DuelBaseChance + MatchRules.DuelSwing
                * (AttributeScale.Factor(dribbling) - AttributeScale.Factor(marker)),
            MatchRules.MinDuelChance,
            MatchRules.MaxDuelChance);

    /// <summary>
    /// The chance a shot from a built-up attack is on target, inside the same method.
    /// </summary>
    public static Sensitivity OnTargetFromSequence()
    {
        const int dribbling = 50;

        return Sweep("Alvo (ataque construído)",
            "0.46 + 0.22 × escala(precisão), clamp(0.20, 0.88)",
            MatchRules.MinOnTargetChance, MatchRules.MaxOnTargetChance,
            accuracy => Math.Clamp(
                MatchRules.BaseOnTargetChance
                    + MatchRules.OnTargetSwing * AttributeScale.Factor(
                        AttributeWeights.Shot.Accuracy * accuracy
                        + AttributeWeights.Shot.Dribbling * dribbling
                        + AttributeWeights.Shot.Speed * dribbling),
                MatchRules.MinOnTargetChance,
                MatchRules.MaxOnTargetChance));
    }

    /// <summary>
    /// <c>ResolveShot</c>, which decides whether a shot on target is a goal: the base, plus
    /// the swing of the -1..1 gap between the striker's shot and the keeper's hands.
    /// </summary>
    public static Sensitivity GoalFromShot()
    {
        const int dribbling = 50;
        const int speed = 50;
        const int keeper = 50;

        return Sweep("Gol (ResolveShot)",
            "0.31 + 0.22 × (tiro − defesa), clamp(0.12, 0.50)",
            MatchRules.MinGoalChance, MatchRules.MaxGoalChance,
            accuracy =>
            {
                var shotPower = AttributeScale.Factor(
                    AttributeWeights.Shot.Accuracy * accuracy
                    + AttributeWeights.Shot.Dribbling * dribbling
                    + AttributeWeights.Shot.Speed * speed);

                var savePower = AttributeScale.Factor(
                    keeper * MatchRules.KeeperReflexShare + keeper * (1 - MatchRules.KeeperReflexShare));

                return Math.Clamp(
                    MatchRules.BaseGoalChance + MatchRules.GoalChanceSwing * (shotPower - savePower),
                    MatchRules.MinGoalChance,
                    MatchRules.MaxGoalChance);
            });
    }

    /// <summary>
    /// <c>TeamStrength</c>, which reads a weighted average of the unit's attributes with no
    /// clamp and no normalisation, so the whole of the scale reaches it.
    /// </summary>
    public static Sensitivity TeamStrengthAttack() =>
        Sweep("Ataque de equipe (TeamStrength)",
            "Σ pesos × atributos × energia",
            null, null,
            quality => quality * AttributeWeights.Attack.Accuracy);

    /// <summary>Every formula the audit covers, in the order they are printed.</summary>
    public static Sensitivity[] All() =>
    [
        SkillFactor(),
        DribbleDuel(),
        OnTargetFromSequence(),
        GoalFromShot(),
        TeamStrengthAttack()
    ];

    /// <summary>Prints the audit as a table.</summary>
    public static void Print()
    {
        Console.WriteLine("  fórmula                            expressão                                  banda viva   cobertura");
        Console.WriteLine("  ---------------------------------  -----------------------------------------  -----------  ---------");

        foreach (var audit in All())
        {
            var band = audit.IsDead
                ? "NUNCA      "
                : $"{audit.Low,4}–{audit.High,-5}";

            Console.WriteLine($"  {audit.Name,-33}  {audit.Formula,-41}  {band}   {audit.Range,7:P0}");
        }

        Console.WriteLine();
        Console.WriteLine("  \"banda viva\" é o intervalo em que a fórmula está entre o seu piso e o seu teto.");
        Console.WriteLine("  Fora dela, dois jogadores de qualidades diferentes recebem exatamente o mesmo número,");
        Console.WriteLine("  e a diferença entre eles deixa de existir para o motor.");
        Console.WriteLine();
    }
}
