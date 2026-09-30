namespace NinjaEleven.BalanceLab;

/// <summary>
/// What a window of football gives a player back, worked out from what he did rather than
/// drawn from a band.
///
/// <para>
/// Production's <c>EnergyRecoveryRules.Recovery</c> opens with <c>random.Next(min, max + 1)</c>
/// and then multiplies, so the randomness sits on the base term rather than on the result: a
/// man who rested a whole window and a man who came on for five minutes are both drawn from
/// the same uniform, and the seed decides which of them got the top of the range. The brief
/// asks for the opposite shape — a deterministic base with the factors that actually differ
/// between two players doing the same work — and the shape below is it.
/// </para>
///
/// <para>
/// <b>Recovery = Base × Minutes × Stamina × Age × Fatigue</b>, in that order, and the order
/// is the argument:
///
/// <list type="number">
/// <item><b>Minutes</b> — how much of a match's work he did, on a curve rather than a
/// fraction. A man on at the eighty-fifth has done real work and a real warm-up, and paying
/// him a fifth of a full match would make the player who did least end the day in the best
/// shape — the exact opposite of what a rotation is for. The exponent below one says the
/// last thirty minutes are worth more than a third of the first sixty, which is true of the
/// legs and true of the decision-making.</item>
/// <item><b>Stamina</b> — how much of that work his body can put back. A man with a large
/// tank empties slowly and refills quickly, and those are the same fact read from opposite
/// ends, which is why one attribute serves both.</item>
/// <item><b>Age</b> — how fast he repairs at all. Production's <c>AgeRecovery</c> bands,
/// carried over unchanged so the laboratory measures a change of shape rather than a change
/// of the age curve as well.</item>
/// <item><b>Fatigue</b> — how far below the top of the scale he started. This is the
/// diminishing return the brief asks for, and it is the factor that answers "a man on 10
/// must not recover like a man on 90".</item>
/// </list>
///
/// <para>
/// The two windows a player does not play in are shaped differently on purpose. Both scale
/// with the <b>deficit</b> rather than with a flat amount, which is the whole of the
/// diminishing return: a man on 95 has almost nothing to get back and is not paid for the
/// privilege, and a man on 30 is rebuilt in proportion to how much of a man he is not. An
/// absolute band cannot say both — the flat band hands the same five points to a man on 95
/// and a man on 30, and only one of those two is finishing the job.
/// </para>
///
/// <para>
/// Every number is deterministic, so the same window recovers the same way on every replay
/// and every figure in it is a consequence of something about the player rather than of the
/// seed. The <c>RandomFactor</c> the brief allows for is present at 1.0, so a 0.97..1.03 band
/// can be switched on later without the shape changing underneath it.
/// </para>
/// </summary>
public static class RecoveryModel
{
    /// <summary>
    /// What a full match rebuilds, as a share of what it cost. Below one, deliberately: the
    /// gap between the two is what a rotation exists to close, and a model where playing
    /// costs nothing has no reason to rest anybody.
    /// </summary>
    public const double RebuildShare = 0.78;

    /// <summary>The floor for anybody who set foot on the pitch, however briefly.</summary>
    public const double CameoFloor = 2.0;

    /// <summary>Share of the deficit a window on the bench puts back.</summary>
    public const double BenchDeficitShare = 0.32;

    /// <summary>Share of the deficit a window his club did not play in puts back.</summary>
    public const double RestDeficitShare = 0.45;

    /// <summary>
    /// The exponent on the deficit. Below one, so a man who is destroyed rebuilds a larger
    /// share of what he is missing than a man who is merely tired — which is both true of
    /// bodies and necessary: with a straight share, a squad ground down to 20 climbs back to
    /// 100 in a fortnight and the fatigue the season is about stops existing.
    /// </summary>
    public const double DeficitExponent = 0.85;

    /// <summary>
    /// How much of a match's work ninety minutes is worth. The engine's own answer is
    /// <c>EnergyCostPerTick × 180 ticks</c> with the age multiplier on top, which lands near
    /// six; the figure here is larger because stamina is now inside the cost and the
    /// labour in the balance of a matchday has moved.
    /// </summary>
    public const double FullMatchCost = 12.0;

    /// <summary>How much of a match's work a man played a share of it has done.</summary>
    public static double MinutesFactor(int minutesPlayed)
    {
        if (minutesPlayed <= 0)
        {
            return 0.0;
        }

        return Math.Pow(Math.Clamp(minutesPlayed / 90.0, 0.0, 1.0), 0.75);
    }

    /// <summary>
    /// How much of a window a man gets back, as a share of the reference.
    ///
    /// <para>
    /// A stamina of 100 is the reference and a stamina of 1 costs a quarter of the recovery.
    /// The exponent is above one, so the last twenty points of stamina are worth more than
    /// the first twenty: a man nearly out of engine recovers badly, which is what makes an
    /// old squad player a different thing from an old one who was always the fittest in the
    /// dressing room.
    /// </para>
    /// </summary>
    public static double StaminaFactor(int stamina) =>
        0.75 + 0.25 * Math.Pow(Math.Clamp(stamina / 100.0, 0.0, 1.0), 1.35);

    /// <summary>How fast a body repairs, by age.</summary>
    public static double AgeFactor(int age) => age switch
    {
        >= 34 => 0.80,
        <= 21 => 1.15,
        <= 28 => 1.05,
        _ => 0.95
    };

    /// <summary>
    /// The fatigue factor of a window that contained football: how much of what he did the
    /// match rebuilds.
    /// </summary>
    /// <remarks>
    /// Between 0.55 and 1.00 rather than the full range, and that restraint is the whole
    /// difference between the two shapes. A man who has been ground down to 30 does not
    /// rebuild <i>nothing</i> after a match — he played the match, and what he played is
    /// rebuilt in proportion to what it rebuilt for everybody else. The deficit is allowed
    /// to modulate the repair; it is not allowed to switch it off. A window with no football
    /// in it is where the deficit takes over completely, because there the only thing that
    /// is being rebuilt is the deficit.
    /// </remarks>
    public static double FatigueFactor(int currentEnergy) =>
        0.55 + 0.45 * Math.Pow(Math.Clamp((100 - currentEnergy) / 100.0, 0.0, 1.0), DeficitExponent);

    /// <summary>
    /// The fatigue factor of a window with no football in it: recovery in proportion to how
    /// much is missing, on a curve that makes the last stretch of the deficit cheap in
    /// relative terms.
    /// </summary>
    public static double DeficitFactor(int currentEnergy) =>
        Math.Pow(Math.Clamp((100 - currentEnergy) / 100.0, 0.0, 1.0), DeficitExponent);

    /// <summary>
    /// What the same man is worth coming off a full ninety minutes, which is what a played
    /// window rebuilds. It is the match's own cost, not a flat band, so a cameo rebuilds a
    /// cameo.
    /// </summary>
    public static double AfterPlaying(int currentEnergy, int minutesPlayed, int stamina, int age) =>
        Math.Max(
            CameoFloor,
            FullMatchCost
            * MinutesFactor(minutesPlayed)
            * StaminaFactor(stamina)
            * AgeFactor(age)
            * RebuildShare
            * FatigueFactor(currentEnergy));

    /// <summary>
    /// What a window that contained no football rebuilds: a share of the deficit, on the
    /// diminishing curve, scaled by the man.
    /// </summary>
    private static double FromDeficit(int currentEnergy, double share, int stamina, int age) =>
        100.0 * share
        * DeficitFactor(currentEnergy)
        * StaminaFactor(stamina)
        * AgeFactor(age);

    /// <summary>The same man, having travelled, warmed up and not played.</summary>
    public static double AfterBench(int currentEnergy, int stamina, int age) =>
        FromDeficit(currentEnergy, BenchDeficitShare, stamina, age);

    /// <summary>The same man, whose club did not play at all. A day off is not a bench.</summary>
    public static double AfterFullRest(int currentEnergy, int stamina, int age) =>
        FromDeficit(currentEnergy, RestDeficitShare, stamina, age);
}
