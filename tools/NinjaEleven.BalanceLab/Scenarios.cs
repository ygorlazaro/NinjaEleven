using NinjaEleven.Domain.Enums;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// A scenario: two attackers with a keeper behind each of them, rolled a hundred thousand
/// times, and the three ways it can come out counted.
///
/// <para>
/// A defender is in the scenario as well as an attacker and a keeper because a duel that
/// skips the defender measures only half of what energy touches. The man who has to be
/// beaten is a man whose own energy is in the arithmetic — a tired defender is a defender
/// who can be beaten — and a laboratory that left him out would report an energy effect
/// about half the size of the real one.
/// </para>
/// </summary>
public sealed record Scenario(
    string Name,
    PlayerCard Attacker,
    PlayerCard Defender,
    PlayerCard Keeper)
{
    public static Scenario Create(string name, PlayerCard attacker, int defenderQuality, int keeperQuality) =>
        new(name, attacker, PlayerCard.Outfield(Position.DEF, defenderQuality, attacker.Energy), PlayerCard.Keeper(keeperQuality, attacker.Energy));
}

/// <summary>
/// The scenarios the laboratory runs, and the tables it prints.
///
/// <para>
/// The order matters. The curve table comes first because it is the claim being examined, the
/// quality-sensitivity table second because it is what the claim is measured against, and the
/// scenarios last because they are the consequence of the two.
/// </para>
/// </summary>
public static class Scenarios
{
    private const int DefaultDuels = 100_000;

    /// <summary>How the ramp reads, and what it costs at the bottom of the scale.</summary>
    public static void PrintCurveTable(IEnergyCurve curve)
    {
        Console.WriteLine($"Curva: {curve.Name}");
        Console.WriteLine("  E:   100   90   80   70   60   50   40   30   20   10    1");
        Console.Write("  F:  ");

        foreach (var energy in new[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 1 })
        {
            Console.Write($"{curve.Factor(energy),5:0.000} ");
        }

        Console.WriteLine();
        Console.WriteLine($"  Razão 100/1 = {curve.Factor(100) / curve.Factor(1):0.000}"
            + $"  |  Razão 100/40 = {curve.Factor(100) / curve.Factor(40):0.000}"
            + $"  |  Razão 80/60 = {curve.Factor(80) / curve.Factor(60):0.000}");
        Console.WriteLine();
    }

    /// <summary>
    /// The most important number in the whole exercise: how far apart two energies have to
    /// be before they can cancel a given gap in quality.
    ///
    /// <para>
    /// For each quality ratio — a striker of 90 against a man of 50, which is a difference a
    /// manager would describe as a world apart — the table says how much of the performance
    /// gap is left after the energy ramp, and whether any energy the weaker man could
    /// physically have would erase it. A ratio under one in the third column means a tired
    /// great is already the worse player; "NÃO" in the last column means the ramp cannot
    /// invert this fixture at all, which is the answer the design is after.
    /// </para>
    /// </summary>
    public static void PrintInversionTable(IEnergyCurve curve)
    {
        Console.WriteLine($"Quanto de diferença de qualidade a energia apaga — curva: {curve.Name}");
        Console.WriteLine();
        Console.WriteLine("  qualidade  E_fresco  E_cansado  razão  |  inversão possível?");
        Console.WriteLine("  ----------  --------  ---------  -----  |  ------------------");

        var pairs = new[] { (90, 50), (90, 60), (80, 50), (95, 70), (70, 50), (90, 40) };

        foreach (var (strong, weak) in pairs)
        {
            var qualityRatio = strong / (double)weak;

            var freshFactor = curve.Factor(100);
            var tiredFactor = curve.Factor(30);
            var survives = qualityRatio * tiredFactor / freshFactor;

            // The energy the weaker man would need for the ramp to cancel the quality gap,
            // searched only inside the scale energy actually lives on.
            var needed = EnergyForParity(curve, strong, weak);

            Console.WriteLine(
                $"  {strong,3} vs {weak,-3}  {freshFactor,8:0.000}  {tiredFactor,9:0.000}  {survives,5:0.000}  |  " +
                (needed is null ? "NÃO — a curva não inverte" : $"SIM, a partir de E={needed:0.0}"));
        }

        Console.WriteLine();
        Console.WriteLine("  razão = (qualidade_A/qualidade_B) × (curva(30)/curva(100)); abaixo de 1.00 o");
        Console.WriteLine("  jogador mais fraco, só que descansado, já é o melhor dos dois.");
        Console.WriteLine();
    }

    /// <summary>
    /// The energy the weaker man would need for the ramp to cancel the quality gap, or null
    /// when no energy between 1 and 100 manages it.
    ///
    /// <para>
    /// Solved by bisection over the scale itself rather than by a closed form, because the
    /// curve is not required to have one and a formula that only some candidate curves
    /// satisfy would quietly exclude the others from the comparison.
    /// </para>
    /// </summary>
    private static double? EnergyForParity(IEnergyCurve curve, int strong, int weak)
    {
        var target = curve.Factor(100) * strong;

        if (curve.Factor(1) * weak >= target)
        {
            return 1;
        }

        if (curve.Factor(100) * weak < target)
        {
            return null;
        }

        var low = 1.0;
        var high = 100.0;

        for (var step = 0; step < 60; step++)
        {
            var mid = (low + high) / 2;

            if (curve.Factor(mid) * weak < target)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return (low + high) / 2;
    }

    /// <summary>
    /// The matrix the brief asks for: attributes fixed on both sides, energy swept, and the
    /// three outcomes counted.
    /// </summary>
    public static void PrintEnergyMatrix(IEnergyCurve curve, int attackerQuality, int defenderQuality, int keeperQuality, int duels)
    {
        var energies = new[] { 100, 80, 60, 40, 20 };

        Console.WriteLine($"Matriz de energia — atacante {attackerQuality}, defensor/defesa {defenderQuality}, goleiro {keeperQuality}");
        Console.WriteLine($"Curva: {curve.Name}");
        Console.WriteLine();
        Console.Write("            ");

        foreach (var energy in energies)
        {
            Console.Write($"E={energy,-3} ".PadLeft(9));
        }

        Console.WriteLine();

        foreach (var attackerEnergy in energies)
        {
            Console.Write($"  A E={attackerEnergy,-3}  ".PadLeft(12));

            foreach (var defenderEnergy in energies)
            {
                var outcome = Run(Scenario.Create("matrix",
                    PlayerCard.Outfield(Position.ATT, attackerQuality, attackerEnergy),
                    defenderQuality,
                    keeperQuality), defenderEnergy, curve, duels);

                var mark = outcome.WinRate >= outcome.LossRate ? " " : "!";

                Console.Write($"{(outcome.WinRate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),4}%{mark}".PadLeft(9));
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("  ! = o defensor com mais energia venceu o confronto (inversão)");
        Console.WriteLine();
    }

    /// <summary>Rolls one scenario and counts the three outcomes.</summary>
    public static DuelOutcome Run(Scenario scenario, int defenderEnergy, IEnergyCurve curve, int duels, int seed = 20_260_930)
    {
        var random = new LabRandom(seed);
        var duel = new DuelSimulator(curve);

        var defender = scenario.Defender with { Energy = defenderEnergy };
        var keeper = scenario.Keeper with { Energy = defenderEnergy };

        var wins = 0;
        var losses = 0;
        var draws = 0;

        for (var index = 0; index < duels; index++)
        {
            if (duel.CreatesChance(scenario.Attacker, defender, random))
            {
                if (duel.Scores(scenario.Attacker, keeper, random))
                {
                    wins++;
                    continue;
                }
            }

            // The mirror: the defender's own chance against a keeper of the attacker's side.
            // A duel where only one side ever attacks is not a duel, it is a siege, and the
            // energy of the defending man would never be measured at all.
            var mirrorAttacker = defender with { Position = Position.ATT };
            var mirrorKeeper = PlayerCard.Keeper(scenario.Attacker.Reflexes, scenario.Attacker.Energy);

            if (duel.CreatesChance(mirrorAttacker, scenario.Attacker, random)
                && duel.Scores(mirrorAttacker, mirrorKeeper, random))
            {
                losses++;
            }
            else
            {
                draws++;
            }
        }

        return new DuelOutcome(wins, losses, draws);
    }

    /// <summary>The headline table: a fixed quality gap, swept across energy.</summary>
    public static void PrintScenarioSweep(IEnergyCurve curve, int duels)
    {
        Console.WriteLine($"Cenários — curva: {curve.Name}");
        Console.WriteLine();
        Console.WriteLine("  cenário                                        A vence   B vence   empate");
        Console.WriteLine("  ---------------------------------------------  --------   --------   ------");

        var scenarios = new (string Label, Scenario Scenario, int DefenderEnergy)[]
        {
            ("forte 90/100 vs fraco 50/100 (mesma energia)",
                Scenario.Create("a", PlayerCard.Outfield(Position.ATT, 90, 100), 50, 50), 100),

            ("forte 90/100 vs fraco 50/20",
                Scenario.Create("b", PlayerCard.Outfield(Position.ATT, 90, 100), 50, 50), 20),

            ("forte 90/20 vs fraco 50/100 (o caso do brief)",
                Scenario.Create("c", PlayerCard.Outfield(Position.ATT, 90, 20), 50, 50), 100),

            ("forte 90/20 vs fraco 50/40",
                Scenario.Create("d", PlayerCard.Outfield(Position.ATT, 90, 20), 50, 50), 40),

            ("equivalentes 70/20 vs 70/100",
                Scenario.Create("e", PlayerCard.Outfield(Position.ATT, 70, 20), 70, 70), 100),

            ("equivalentes 70/60 vs 70/80",
                Scenario.Create("f", PlayerCard.Outfield(Position.ATT, 70, 60), 70, 70), 80),

            ("mesmo homem, 100 vs 60",
                Scenario.Create("g", PlayerCard.Outfield(Position.ATT, 70, 100), 70, 70), 60)
        };

        foreach (var (label, scenario, defenderEnergy) in scenarios)
        {
            var outcome = Run(scenario, defenderEnergy, curve, duels);

            Console.WriteLine(
                $"  {label,-45}  {Percent(outcome.WinRate),8}   {Percent(outcome.LossRate),8}   {Percent(outcome.DrawRate),6}");
        }

        Console.WriteLine();
    }

    private static string Percent(double rate) =>
        (rate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
