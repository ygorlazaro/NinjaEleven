using NinjaEleven.Domain.Matches;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// How much of a player's quality a given amount of energy lets him deliver.
///
/// <para>
/// This is the seam the whole laboratory hangs on. Production multiplies a player's
/// attributes by <c>Energy / 100</c> (<see cref="TeamStrength"/>), so the curve is not a
/// parameter today — it is a hardcoded linear ramp, and it is the reason a tired great can
/// lose to a fresh mediocre. The laboratory takes the ramp out of the wall so a candidate can
/// be measured before anything in the engine is touched.
/// </para>
///
/// <para>
/// A curve answers one question: given an energy between 1 and 100, what fraction of the
/// player's own attributes reaches the action? <c>1.00</c> means a man on 100 energy delivers
/// everything he has; <c>0.65</c> means he delivers two thirds of it and the missing third is
/// what tiredness took off him tonight.
/// </para>
///
/// <para>
/// Two properties are being balanced against each other and cannot both be maximised:
///
/// <list type="bullet">
/// <item>the <b>span</b> — how much of a player's quality a fully drained man loses
/// (<c>Factor(1)</c>), which is what makes rotation worth doing;</item>
/// <item>the <b>floor of the hierarchy</b> — how close two curves are to parallel, which is
/// what decides whether energy can ever invert a quality gap.</item>
/// </list>
///
/// <para>
/// The span is the easy half. The hierarchy is the hard half: <i>the ratio between two
/// energies must never exceed the ratio between two qualities that a manager would call
/// different players</i>. A linear ramp has span 1.0, which means energy alone can cancel a
/// player who is twice as good as his opponent — and twice as good is not an exotic pairing,
/// it is a striker and a squad player.
/// </para>
/// </summary>
public interface IEnergyCurve
{
    /// <summary>Name used in reports, so a table of numbers says which curve produced it.</summary>
    string Name { get; }

    /// <summary>
    /// The fraction of the player's attributes that reaches the action, from 1 to 100 energy.
    /// Must be in (0, 1] and must be non-decreasing.
    /// </summary>
    double Factor(double energy);
}

/// <summary>
/// What production does today: <c>Energy / 100</c>.
/// <para>
/// It is here as a curve rather than as arithmetic scattered through the engine so the
/// laboratory can hold every other candidate against the number the game shipped for years:
/// <i>a fully drained player delivers nothing, so no amount of quality survives it.</i> The
/// game no longer ramps this way — <see cref="Fidelity.Production"/> is the curve the engine
/// reads — and it is kept because it is the baseline every candidate is measured against.
/// </para>
/// </summary>
public sealed class LinearEnergyCurve : IEnergyCurve
{
    public static readonly LinearEnergyCurve Instance = new();

    public string Name => "Linear (o que o motor usava: Energy/100)";

    public double Factor(double energy) => Math.Clamp(energy / 100.0, 0.0, 1.0);
}

/// <summary>
/// The curve proposed in the design brief, read as the table it was given as.
///
/// <para>
/// Piecewise-linear between the ten anchor points, so it is exactly the numbers that were
/// proposed rather than a fit through them. The table is a hypothesis and the laboratory
/// exists to say whether it is a good one.
/// </para>
/// </summary>
public sealed class TableEnergyCurve : IEnergyCurve
{
    private static readonly (int Energy, double Factor)[] Anchors =
    {
        (10, 0.55), (20, 0.65), (30, 0.72), (40, 0.78), (50, 0.83),
        (60, 0.87), (70, 0.91), (80, 0.94), (90, 0.97), (100, 1.00)
    };

    public static readonly TableEnergyCurve Instance = new();

    public string Name => "Tabela proposta (10→0.55 … 100→1.00)";

    public double Factor(double energy)
    {
        // Above the last anchor the table is flat at 1.00 — nobody gains from a hundred and
        // one — and below the first it keeps falling on the same slope, so a man on 1 is
        // worse off than the table says rather than better off than it says.
        if (energy >= Anchors[^1].Energy)
        {
            return Anchors[^1].Factor;
        }

        if (energy <= Anchors[0].Energy)
        {
            var (energy0, factor0) = Anchors[0];
            var slope = SlopeAt(energy0);

            return Math.Clamp(factor0 + (energy - energy0) * slope, 0.05, 1.0);
        }

        for (var index = 1; index < Anchors.Length; index++)
        {
            var (upperEnergy, upperFactor) = Anchors[index];

            if (energy > upperEnergy)
            {
                continue;
            }

            var (lowerEnergy, lowerFactor) = Anchors[index - 1];
            var share = (energy - lowerEnergy) / (double)(upperEnergy - lowerEnergy);

            return lowerFactor + share * (upperFactor - lowerFactor);
        }

        return 1.0;
    }

    private static double SlopeAt(double energy)
    {
        for (var index = 1; index < Anchors.Length; index++)
        {
            var (upperEnergy, upperFactor) = Anchors[index];

            if (energy > upperEnergy)
            {
                continue;
            }

            var (lowerEnergy, lowerFactor) = Anchors[index - 1];

            return (upperFactor - lowerFactor) / (upperEnergy - lowerEnergy);
        }

        return 0.0;
    }
}

/// <summary>
/// A ramp with a floor: <c>floor + (1 - floor) × energy/100</c>.
///
/// <para>
/// The shape the engine already uses for a player's rating
/// (<see cref="PlayerMetric.Metric"/>, floor 0.85) and the shape every football game uses for
/// condition: a man who is dead on his feet is not a man who is thirty per cent of a player,
/// he is a man who is a worse version of himself. The floor is the number that decides
/// whether the curve can ever invert a quality gap — at 0.55 the worst tired player still
/// delivers more than half of what he is, so an attribute ratio below 1.82 is untouchable by
/// energy.
/// </para>
/// </summary>
public sealed class FlooredEnergyCurve : IEnergyCurve
{
    private readonly double _floor;

    public FlooredEnergyCurve(double floor)
    {
        if (floor <= 0 || floor >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(floor), floor, "The floor must be in (0, 1).");
        }

        _floor = floor;
    }

    public string Name => $"Rampa com piso ({_floor:0.00} + {(1 - _floor):0.00}×E/100)";

    public double Factor(double energy) =>
        _floor + (1 - _floor) * Math.Clamp(energy / 100.0, 0.0, 1.0);
}

/// <summary>
/// A curve whose slope grows as energy falls: <c>(E/100)^γ</c>.
///
/// <para>
/// The only shape of the three that is <i>steeper at the bottom</i>. A ramp with a floor
/// treats a man on 60 and a man on 40 as differing by a fixed fraction, which is not what a
/// tired body is like: the first ten points below the reference cost about as much as the
/// last thirty do. Raising γ pushes the whole cost of exhaustion into the last third of the
/// scale, where the matches that are actually in danger are decided.
/// </para>
/// </summary>
public sealed class PowerEnergyCurve : IEnergyCurve
{
    private readonly double _gamma;

    public PowerEnergyCurve(double gamma)
    {
        if (gamma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), gamma, "Gamma must be positive.");
        }

        _gamma = gamma;
    }

    public string Name => $"Potência ((E/100)^{_gamma:0.00})";

    public double Factor(double energy) => Math.Pow(Math.Clamp(energy / 100.0, 0.0, 1.0), _gamma);
}

/// <summary>
/// Floor, and a slope that steepens towards exhaustion: the composition of the two above.
///
/// <para>
/// <c>floor + (1 - floor) × (E/100)^γ</c>. The floor answers "how much of a man is a spent
/// man", the exponent answers "how the last twenty points of energy are paid for", and the
/// two are independent questions — which is the reason they are two parameters rather than
/// one.
/// </para>
/// </summary>
public sealed class ComposedEnergyCurve : IEnergyCurve
{
    private readonly double _floor;
    private readonly double _gamma;

    public ComposedEnergyCurve(double floor, double gamma)
    {
        if (floor <= 0 || floor >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(floor), floor, "The floor must be in (0, 1).");
        }

        if (gamma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), gamma, "Gamma must be positive.");
        }

        _floor = floor;
        _gamma = gamma;
    }

    public string Name => $"Composta (piso {_floor:0.00}, γ {_gamma:0.00})";

    public double Factor(double energy) =>
        _floor + (1 - _floor) * Math.Pow(Math.Clamp(energy / 100.0, 0.0, 1.0), _gamma);
}

/// <summary>The curves the laboratory knows how to measure, in the order they are reported.</summary>
public static class EnergyCurves
{
    public static IReadOnlyList<IEnergyCurve> All { get; } =
    [
        LinearEnergyCurve.Instance,
        new TableEnergyCurve(),
        new FlooredEnergyCurve(0.55),
        new FlooredEnergyCurve(0.65),
        new ComposedEnergyCurve(0.55, 1.5),
        new ComposedEnergyCurve(0.65, 1.5)
    ];
}
