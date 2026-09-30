using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Domain.Players;

/// <summary>
/// What a body's stamina is worth, in both of the two directions it acts in.
///
/// <para>
/// Stamina is the only attribute that is not about how well a man plays. It is about how
/// much football is left in him at minute eighty-five and about how quickly he puts back
/// what a match took, and those are the two ends of one fact rather than two attributes: a
/// body that empties slowly is a body that fills quickly, and an attribute that bought only
/// one of them would be half an attribute — a player who is hard to tire and slow to mend is
/// a worse player than either a tireless one or a durable one.
/// </para>
///
/// <para>
/// Every number here is a -1..1 reading, which is the only way the two can be compared and
/// combined with the age and the fatigue. A raw 1..100 attribute used as a multiplier is a
/// number that never leaves its floor for half the world, which is the failure the whole
/// scale repair was about.
/// </para>
/// </summary>
public static class StaminaRules
{
    /// <summary>
    /// How far below the reference a man of no stamina sits, and above it a man of perfect
    /// stamina. The span is the whole scale rather than half of it, so a man of 0 and a man of
    /// 100 reach the two ends — a stamina that could not exhaust itself would not be an
    /// attribute.
    /// </summary>
    public const double Span = 50.0;

    /// <summary>
    /// How much of the ninety minutes a man at the reference has left at the end of it,
    /// against a man of no stamina at all.
    /// </summary>
    /// <remarks>
    /// This is the tank. It is a modest multiplier rather than a dramatic one because the
    /// energy ramp (<see cref="EnergyCurve"/>) is already doing the visible work of making
    /// a tired side measurably weaker; stamina is what decides how tired, and a man at the
    /// bottom of the scale should finish a match on the floor rather than unable to finish
    /// it at all — a player who cannot complete a match is not a tired player, he is an
    /// unavailable one, and that is a different design.
    ///
    /// The two ends straddle one exactly, which is what makes the reference body cost
    /// nothing: a band from 0.60 to 1.25 has a midpoint of 0.925, and a stamina rule that
    /// quietly charged the average player a quarter of a tick would be taxing half the world
    /// to make the ends of the scale look busy.
    /// </remarks>
    public const double MinTank = 0.75;
    public const double MaxTank = 1.25;

    /// <summary>
    /// How much of a full window's rest a man at the reference gets, against a man of no
    /// stamina at all. It is a gentler multiplier than the tank on purpose: recovery is
    /// already banded by what he did, and a stamina that dominated it would mean a low-stamina
    /// player could never come back from a bad week no matter how long he rested.
    /// </summary>
    public const double MinRefill = 0.75;
    public const double MaxRefill = 1.25;

    /// <summary>The reading of a stamina, on the -1..1 scale the other rules multiply by.</summary>
    public static double Factor(double stamina) =>
        Math.Clamp((stamina - Player.StaminaReference) / Span, -1.0, 1.0);

    /// <summary>Overload for the engine, which reads stamina off a player.</summary>
    public static double Factor(int stamina) => Factor((double)stamina);

    /// <summary>Overload for a snapshot, which is where the match reads it from.</summary>
    public static double Factor(MatchPlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return Factor(player.Stamina);
    }

    /// <summary>
    /// How expensive a stretch of football is for a man with this much stamina — the tank.
    /// It multiplies the drain, so a bigger number is a more expensive man.
    /// </summary>
    public static double TankCost(double stamina) =>
        TankAt(Factor(stamina));

    /// <summary>The same, for a player the match is holding.</summary>
    public static double TankCost(MatchPlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return TankAt(Factor(player.Stamina));
    }

    /// <summary>
    /// How much of a window's rest this much stamina earns — the refill. It multiplies the
    /// recovery, so a bigger number is a quicker man to put back together.
    /// </summary>
    public static double Refill(double stamina) =>
        RefillAt(Factor(stamina));

    /// <summary>The same, for a player the caller is recovering.</summary>
    public static double Refill(MatchPlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return RefillAt(Factor(player.Stamina));
    }

    /// <summary>
    /// The tank, read off the -1..1 factor. It runs the other way from the refill, and the
    /// two running in the same direction would be the bug rather than the design: more in
    /// the tank has to mean a cheaper man to run.
    /// </summary>
    private static double TankAt(double factor) => MaxTank + (MinTank - MaxTank) * (factor + 1.0) / 2.0;

    /// <summary>The refill, which runs with the factor: more in the tank, a quicker mend.</summary>
    private static double RefillAt(double factor) => MinRefill + (MaxRefill - MinRefill) * (factor + 1.0) / 2.0;
}
