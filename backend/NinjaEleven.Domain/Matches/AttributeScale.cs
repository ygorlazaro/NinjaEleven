namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Where an attribute sits on the 1..100 scale the game actually uses, and what that means
/// for a formula that reads one.
///
/// <para>
/// The attributes were written for a 1..20 scale and <see cref="Player.Create"/> clamps them
/// to 1..100. Every formula that measured a player as a distance from an average and then
/// clamped the result was therefore bolted at its ceiling for every player in the world: a
/// man of 45 and a man of 95 both read as "as good as the formula can express". That is not a
/// balance opinion, it is arithmetic, and it is why energy dominated the engine — energy was
/// the only thing multiplying a raw attribute anywhere that mattered, so it was the only live
/// path for a difference in quality.
/// </para>
///
/// <para>
/// The reference and the span are therefore properties of the scale and live here, in one
/// place, rather than as a pair of constants in whichever formula happened to need them.
/// <c>AttributeSpan</c> is the distance from the reference to the top of the scale, which is
/// what makes the mapping reach its ends on the whole of 1..100 instead of a fifth of it.
/// </para>
/// </summary>
public static class AttributeScale
{
    /// <summary>The lowest attribute the world can hold.</summary>
    public const int Min = 1;

    /// <summary>The highest attribute the world can hold.</summary>
    public const int Max = 100;

    /// <summary>
    /// A player of average quality. The seeded distribution puts thirty per cent of its mass
    /// in 41..60 and its mean at about 50, so this is the middle of the world rather than an
    /// aspiration.
    /// </summary>
    public const double Reference = 50.0;

    /// <summary>
    /// How far from <see cref="Reference"/> the scale reaches at either end. It is half the
    /// span of the scale, so the mapping is symmetric and saturates at both ends instead of
    /// at one.
    /// </summary>
    public const double HalfSpan = 45.0;

    /// <summary>
    /// Places an attribute on a -1..1 scale: zero for an average player, positive above him,
    /// negative below, and never past the ends of the scale.
    ///
    /// <para>
    /// This is the one conversion every action formula is built on, and it is worth stating
    /// what it buys: two players twenty points apart are the same distance apart here whether
    /// they are at 20 and 40 or at 80 and 100, which is the property the old span of seven
    /// could not have — a twenty-point gap was not merely beyond its reach, it was three times
    /// beyond it.
    /// </para>
    /// </summary>
    public static double Factor(double attribute) =>
        Math.Clamp((attribute - Reference) / HalfSpan, -1.0, 1.0);

    /// <summary>
    /// Places the average of two attributes on the same scale, for the readings that need a
    /// pair: a penalty is taken on finishing <i>and</i> control, and a keeper saves on
    /// reflexes <i>and</i> power.
    /// </summary>
    public static double Factor(double first, double second) => Factor((first + second) / 2.0);

    /// <summary>
    /// Turns a -1..1 factor back into an attribute, which is what the ranges of the team
    /// strengths are expressed in. Every number a manager reads off a strength is on the
    /// attribute scale, because that is the scale the only thing he can act on is on.
    /// </summary>
    public static double ToAttribute(double factor) =>
        Reference + Math.Clamp(factor, -1.0, 1.0) * HalfSpan;
}
