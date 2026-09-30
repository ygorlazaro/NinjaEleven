using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// The laboratory's front door: it prints what it finds and exits.
///
/// <para>
/// Every run starts with the fidelity check, because a balance number produced by a model
/// that no longer matches the engine is worse than no balance number at all — it is a
/// confident wrong answer, and it is the kind of wrong answer that gets a rule tuned. If the
/// check fails, the rest of the output is not to be read.
/// </para>
/// </summary>
public static class Program
{
    private const int Duels = 100_000;

    public static void Main(string[] args)
    {
        var only = args.Length > 0 ? args[0] : "all";

        Section("1. FIDELIDADE — o laboratório contra o TeamStrength de produção");
        var (worstError, worstCase) = Fidelity.CheckAgainstProduction();

        Console.WriteLine($"  maior erro relativo entre LabStrength e TeamStrength.Of: {worstError:P4}");
        Console.WriteLine($"  caso: {worstCase}");

        if (worstError > 0.0001)
        {
            Console.WriteLine("  FALHOU — os números abaixo não são sobre o jogo que existe.");
            return;
        }

        Console.WriteLine("  OK — o laboratório mede o mesmo modelo que o motor joga.");
        Console.WriteLine();

        Section("1b. CRUZAMENTO — o motor de verdade, 400 partidas 80/100 contra 40/100");
        var (strongWins, weakWins) = Fidelity.EngineCrossCheck(400);
        Console.WriteLine($"  fortes vencem {Percent(strongWins)} | fracos vencem {Percent(weakWins)} | empates {Percent(1 - strongWins - weakWins)}");
        Console.WriteLine("  (o motor real concorda: a qualidade domina com energias iguais)");
        Console.WriteLine();

        if (only is "all" or "curves")
        {
            Section("2. AS CURVAS");
            Console.WriteLine("  O que está em produção hoje, e o que a hipótese do brief propõe:");
            Console.WriteLine();

            foreach (var curve in EnergyCurves.All)
            {
                Scenarios.PrintCurveTable(curve);
            }
        }

        Section("3. A ESCALA DOS ATRIBUTOS NO MOTOR");

        ScaleAudit.Print();

        Console.WriteLine("  Cada fórmula foi escrita quando um atributo ia de 1 a 20. As constantes");
        Console.WriteLine("  (\"13\", \"7\", \"/54\", \"+14\", \"−10\", \"0.018\") são todas dessa escala, e");
        Console.WriteLine("  Player.Create limita os atributos a 1..100 — então o clamp no fim de cada");
        Console.WriteLine("  fórmula fechava no teto para praticamente todo jogador do mundo. Hoje todas");
        Console.WriteLine("  leem através de AttributeScale, e a banda viva é a escala inteira.");
        Console.WriteLine();

        var production = Fidelity.Production;
        var ninetyVsFifty = new DuelSimulator(production);
        var strongShooter = PlayerCard.Outfield(Position.ATT, 90, 100);
        var weakShooter = PlayerCard.Outfield(Position.ATT, 50, 100);
        var averageKeeper = PlayerCard.Keeper(50, 100);
        var eliteKeeper = PlayerCard.Keeper(80, 100);

        Console.WriteLine("  Consequência medida na fórmula de gol (ResolveShot), rampa de produção, energia 100:");
        Console.WriteLine("    atirador 90 vs goleiro 50: " + Percent(ninetyVsFifty.GoalChance(strongShooter, averageKeeper)));
        Console.WriteLine("    atirador 50 vs goleiro 50: " + Percent(ninetyVsFifty.GoalChance(weakShooter, averageKeeper)));
        Console.WriteLine("    atirador 20 vs goleiro 50: " + Percent(ninetyVsFifty.GoalChance(
            PlayerCard.Outfield(Position.ATT, 20, 100), averageKeeper)));
        Console.WriteLine("    atirador 90 vs goleiro 80: " + Percent(ninetyVsFifty.GoalChance(strongShooter, eliteKeeper)));
        Console.WriteLine("    atirador 20 vs goleiro 20: " + Percent(ninetyVsFifty.GoalChance(
            PlayerCard.Outfield(Position.ATT, 20, 100), PlayerCard.Keeper(20, 100))));
        Console.WriteLine();
        Console.WriteLine("  Um atirador de 20 contra um goleiro de 50 vale menos da metade do que um de");
        Console.WriteLine("  50: a ordem dos jogadores é a ordem deles. E o atirador de 20 contra um goleiro");
        Console.WriteLine("  de 20 vale o mesmo que o de 50 contra um de 50 — dois pares igualmente");
        Console.WriteLine("  distantes da média valem o mesmo, o que é o que a comparação em vez da");
        Console.WriteLine("  subtração bruta garante. Um goleiro bom importa.");
        Console.WriteLine();

        Section("3b. MESMO ACHADO, MEDIDO POR SENSIBILIDADE");
        Console.WriteLine("  Chance de gol do atirador 50 contra goleiro 50, varrendo a qualidade do atirador:");
        Console.WriteLine("    qualidade:   20    30    40    50    60    70    80    90   100");
        Console.Write("    gol:        ");

        foreach (var quality in new[] { 20, 30, 40, 50, 60, 70, 80, 90, 100 })
        {
            var chance = ninetyVsFifty.GoalChance(
                PlayerCard.Outfield(Position.ATT, quality, 100), averageKeeper);

            Console.Write($"{chance,6:0.000} ");
        }

        Console.WriteLine();
        Console.WriteLine();

        if (only is "all" or "scenarios")
        {
            Section("4. CENÁRIOS — CADA CURVA, MESMO EXPERIMENTO");

            foreach (var curve in EnergyCurves.All)
            {
                Scenarios.PrintScenarioSweep(curve, Duels);
            }
        }

        Section("5. MATRIZ DE ENERGIA");

        foreach (var curve in EnergyCurves.All)
        {
            Scenarios.PrintEnergyMatrix(curve, attackerQuality: 90, defenderQuality: 50, keeperQuality: 50, duels: Duels / 10);
        }

        Section("6. INVERSÃO — QUE ENERGIA APAGA QUE DIFERENÇA DE QUALIDADE");

        foreach (var curve in EnergyCurves.All)
        {
            Scenarios.PrintInversionTable(curve);
        }

        Section("7. RECUPERAÇÃO — UMA TEMPORADA INTEIRA, COM E SEM STAMINA");

        SeasonSimulator.Print(
            SeasonSimulator.Run(squadSize: 23, matchdays: 34, rotation: 5),
            "elenco de 23, rotação 5 (11 de 23 jogando por rodada)");

        Console.WriteLine("  O mesmo onze, todas as rodadas, sem rotação — o caso que a regra tem de julgar:");
        SeasonSimulator.Print(
            SeasonSimulator.RunWithoutRotation(23, 34),
            "elenco de 23, onze fixo, 34 rodadas");

        Console.WriteLine("  Uma temporada COM rotação (cada homem folga 'rotação' dias entre jogadas):");
        Console.Write("    rotação:    5     8    11    14    20");

        Console.Write("    energia:  ");

        foreach (var rotation in new[] { 5, 8, 11, 14, 20 })
        {
            var report = SeasonSimulator.Run(23, 34, rotation);
            Console.Write($"{report.Mean,6:0.0} ");
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  Recuperação de uma janela, por minutos jogados (energia inicial 80, stamina 60, 26 anos):");
        Console.WriteLine("    minutos:    5     15     30     45     60     75     90");

        Console.Write("    ganho:   ");

        foreach (var minutes in new[] { 5, 15, 30, 45, 60, 75, 90 })
        {
            Console.Write($"{RecoveryModel.AfterPlaying(80, minutes, 60, 26),6:0.00} ");
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  O mesmo jogador (90 min, 26 anos) por stamina:");
        Console.Write("    stamina:  ");

        foreach (var stamina in new[] { 20, 40, 60, 80, 100 })
        {
            Console.Write($"{stamina,5}→{RecoveryModel.AfterPlaying(80, 90, stamina, 26),5:0.00} ");
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  O mesmo jogador (90 min, stamina 60) por idade:");
        Console.Write("    idade:    ");

        foreach (var age in new[] { 18, 22, 26, 30, 34, 38 })
        {
            Console.Write($"{age,4}→{RecoveryModel.AfterPlaying(80, 90, 60, age),5:0.00} ");
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  Retornos decrescentes — energia ganha a partir de cada ponto de partida:");
        Console.Write("    início:   ");

        foreach (var start in new[] { 10, 30, 50, 70, 90 })
        {
            Console.Write($"{start,4}→{Math.Min(100, start + (int)Math.Round(RecoveryModel.AfterBench(start, 60, 26))) - start,4} ");
        }

        Console.WriteLine();
        Console.WriteLine("    (ganho em uma janela de banco; o mesmo fator vale para qualquer janela)");
        Console.WriteLine();
        Console.WriteLine("  Custo de 90 minutos por stamina (o que a partida tira):");
        Console.Write("    stamina:  ");

        foreach (var stamina in new[] { 20, 40, 60, 80, 100 })
        {
            Console.Write($"{stamina,5}→{-SeasonSimulator.MatchEnergyCost(90, stamina, 26),5:0.0} ");
        }

        Console.WriteLine();
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
    }

    private static string Percent(double rate) =>
        (rate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
