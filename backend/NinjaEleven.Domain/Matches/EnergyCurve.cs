namespace NinjaEleven.Domain.Matches;

/// <summary>
/// How much of a player's quality a given amount of energy lets him deliver.
///
/// <para>
/// This is the one ramp in the game, and it used to be three of them. <see cref="TeamStrength"/>
/// multiplied a player's attributes by <c>Energy / 100</c>, <see cref="PlayerMetric.Metric"/>
/// multiplied by <c>0.85 + 0.15 × Energy / 100</c>, and the keeper's share of a defence by
/// <c>0.9 + Energy / 1000</c>. Three ramps, three different answers to one question, and the
/// first of them was the reason a tired great could be beaten by a fresh squad player: a
/// linear ramp runs to <b>zero</b>, so a fully drained man delivers nothing at all and no
/// amount of quality survives him.
/// </para>
///
/// <para>
/// The curve here is <c>floor + (1 − floor) × (E / 100)^γ</c>, with the floor and the exponent
/// in <see cref="MatchRules"/>. Two numbers, because they are two questions:
/// </para>
///
/// <list type="bullet">
/// <item>the <b>floor</b> is how much of a man is a spent man — a player on an empty tank is
/// a worse version of himself, never thirty per cent of a footballer;</item>
/// <item>the <b>exponent</b> is how the last stretch of the scale is paid for — a tired body
/// loses its first ten points far more cheaply than its last thirty.</item>
/// </list>
///
/// <para>
/// The floor is what makes this a tax rather than a veto. The property the design is judged
/// by is a ratio: <i>energy may not cancel a quality gap</i>, which
/// <c>InitiativeRatio(90, tired, 45, fresh) &gt; 1</c> states and
/// <c>BalanceInvariantTests.ATiredGreatStillWinsAgainstAFreshMediocreOne</c> holds. A linear
/// ramp fails it and any floor of zero fails it, because the extreme of the curve is then the
/// extreme of the hierarchy.
/// </para>
/// </summary>
public static class EnergyCurve
{
    /// <summary>
    /// The fraction of a player's own attributes that reaches the action, from 1 to 100 energy.
    /// Never above a full tank, never below <see cref="MatchRules.EnergyFactorFloor"/>, and
    /// never going up as the energy goes down.
    /// </summary>
    public static double Factor(double energy)
    {
        var filled = Math.Clamp(energy, 0.0, 100.0) / 100.0;

        return MatchRules.EnergyFactorFloor
            + (1.0 - MatchRules.EnergyFactorFloor)
            * Math.Pow(filled, MatchRules.EnergyFactorGamma);
    }

    /// <summary>Overload for the engine, which reads energy off a player rather than a number.</summary>
    public static double Factor(MatchPlayerSnapshot player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        return Factor(player.Energy);
    }
}
