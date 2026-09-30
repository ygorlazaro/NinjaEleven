namespace NinjaEleven.BalanceLab;

/// <summary>
/// A season of football, run end to end, so the recovery formula can be judged by what it
/// does to a squad rather than by what it returns for one player on one evening.
///
/// <para>
/// A recovery rule that looks right in isolation and produces a world where nobody is ever
/// tired is a rule that is wrong. The laboratory runs a whole season — thirty-four windows,
/// a squad rotating eleven men — and reports the distribution of energy at the end of it,
/// which is the only number that says whether the fatigue the whole model is built on
/// actually exists.
/// </para>
///
/// <para>
/// Deterministic throughout. Every energy in the table below is a consequence of the window
/// before it, which is the property the brief asks for and the reason the season can be
/// checked against a hand-worked example.
/// </para>
/// </summary>
public static class SeasonSimulator
{
    /// <summary>One window of a matchday: who played, for how long, and how fit they started it.</summary>
    public readonly record struct Window(int Minutes, int[] Squad);

    /// <summary>
    /// Runs a squad through a season, rotating so that nobody plays every match, and returns
    /// what the rotation looked like from the manager's side.
    /// </summary>
    public static SeasonReport Run(int squadSize, int matchdays, int rotation) =>
        Run(squadSize, matchdays, rotation, rotateSquad: true);

    /// <summary>
    /// The same season with the rotation switched off, which is the case the recovery rule
    /// has to be judged on: a club that puts the same eleven out every day of a thirty-four
    /// day season is a club that will find out what its recovery is worth.
    /// </summary>
    public static SeasonReport RunWithoutRotation(int squadSize, int matchdays) =>
        Run(squadSize, matchdays, rotation: 0, rotateSquad: false);

    private static SeasonReport Run(int squadSize, int matchdays, int rotation, bool rotateSquad)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(squadSize, 11);
        ArgumentOutOfRangeException.ThrowIfLessThan(matchdays, 1);

        if (rotateSquad)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(rotation, 1);
        }

        var energy = new int[squadSize];
        var stamina = new int[squadSize];
        var ages = new int[squadSize];

        for (var index = 0; index < squadSize; index++)
        {
            energy[index] = 100;
            stamina[index] = 30 + index * 3;
            ages[index] = 20 + (index % 20);
        }

        // Stamina 30..99 across the squad, so a season is measured over men who are not
        // clones of each other. A model tuned on one body is a model tuned on one man.

        var samples = new List<int>();
        var playedCount = new int[squadSize];
        var lowEnergyWindows = 0;

        for (var day = 0; day < matchdays; day++)
        {
            // The eleven of the day: a window on the squad that rotates, so each man plays
            // roughly eleven matches out of every `squadSize / 11` the club plays. Two of the
            // eleven come off the bench on some days, which is what puts cameos in the sample
            // and what makes the minutes factor observable rather than theoretical.
            var starters = new int[11];

            for (var slot = 0; slot < 11; slot++)
            {
                starters[slot] = rotateSquad
                    ? (day * rotation + slot) % squadSize
                    : slot % squadSize;
            }

            for (var index = 0; index < squadSize; index++)
            {
                var isStarter = Array.IndexOf(starters, index) >= 0;

                if (!isStarter)
                {
                    energy[index] = Clamp((int)Math.Round(
                        energy[index] + RecoveryModel.AfterBench(energy[index], stamina[index], ages[index])));

                    continue;
                }

                // The two who come off the bench get a cameo, the other nine play the match.
                var minutes = day % 3 == 0 && index % 11 >= 9 ? 25 : 90;

                var cost = MatchEnergyCost(minutes, stamina[index], ages[index]);
                var recovered = RecoveryModel.AfterPlaying(energy[index], minutes, stamina[index], ages[index]);

                energy[index] = Clamp((int)Math.Round(energy[index] - cost + recovered));
                playedCount[index]++;
            }

            lowEnergyWindows += energy.Count(value => value < MatchRulesRestThreshold);

            samples.AddRange(energy);
        }

        samples.Sort();

        return new SeasonReport(
            squadSize,
            matchdays,
            samples,
            playedCount,
            lowEnergyWindows,
            squadSize * matchdays);
    }

    /// <summary>The energy below which a squad is running on fumes and the rotation is failing.</summary>
    public const int MatchRulesRestThreshold = 50;

    /// <summary>
    /// What ninety minutes cost a man, which is where stamina enters production for the first
    /// time: a man with a large tank drains slower.
    ///
    /// <para>
    /// Production's cost is flat — 0.035 a tick whatever the player — and the whole of its
    /// age and injury variation sits in <c>AgeCost</c>. The proposal puts stamina in the
    /// multiplier so the man who can keep going is the man who costs less, which is the
    /// second place stamina earns its column: the first is what he delivers, and this is
    /// what it costs to deliver it.
    /// </para>
    /// </summary>
    public static double MatchEnergyCost(int minutes, int stamina, int age)
    {
        const double baseCostPerMinute = 0.075;
        const double referenceAgeCost = 1.0;

        var ageCost = age switch
        {
            >= 34 => 1.18,
            <= 21 => 0.78,
            <= 28 => 0.90,
            _ => referenceAgeCost
        };

        var staminaCost = 1.45 - 0.45 * (Math.Clamp(stamina, 1, 100) / 100.0);

        return minutes * baseCostPerMinute * ageCost * staminaCost;
    }

    private static int Clamp(int value) => Math.Max(1, Math.Min(100, value));

    /// <summary>What a season of the above looked like, in the numbers a manager would care about.</summary>
    public sealed record SeasonReport(
        int SquadSize,
        int Matchdays,
        List<int> EnergySamples,
        int[] MatchesPlayed,
        int LowEnergyWindows,
        int WindowsObserved)
    {
        public double Minimum => EnergySamples.Count == 0 ? 0 : EnergySamples[0];

        public double P10 => Percentile(0.10);

        public double Median => Percentile(0.50);

        public double P90 => Percentile(0.90);

        public double Maximum => EnergySamples.Count == 0 ? 0 : EnergySamples[^1];

        public double Mean => EnergySamples.Count == 0 ? 0 : EnergySamples.Average();

        public double LowEnergyShare => (double)LowEnergyWindows / Math.Max(1, WindowsObserved);

        public int MinMatchesPlayed => MatchesPlayed.Length == 0 ? 0 : MatchesPlayed.Min();

        public int MaxMatchesPlayed => MatchesPlayed.Length == 0 ? 0 : MatchesPlayed.Max();

        private double Percentile(double share)
        {
            if (EnergySamples.Count == 0)
            {
                return 0;
            }

            var index = (int)Math.Round((EnergySamples.Count - 1) * share);

            return EnergySamples[Math.Clamp(index, 0, EnergySamples.Count - 1)];
        }
    }

    /// <summary>Prints a season report as a table.</summary>
    public static void Print(SeasonReport report, string title)
    {
        Console.WriteLine($"  {title}");
        Console.WriteLine($"    energia: mín {report.Minimum:0} | P10 {report.P10:0} | mediana {report.Median:0} | P90 {report.P90:0} | máx {report.Maximum:0} | média {report.Mean:0.0}");
        Console.WriteLine($"    jogos por homem: {report.MinMatchesPlayed}..{report.MaxMatchesPlayed} de {report.Matchdays}");
        Console.WriteLine($"    janelas abaixo de {MatchRulesRestThreshold} de energia: {report.LowEnergyShare:P1} das observações");
        Console.WriteLine();
    }
}
