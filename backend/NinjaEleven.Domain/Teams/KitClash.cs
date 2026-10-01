namespace NinjaEleven.Domain.Teams;

/// <summary>
/// Whether two shirts can be told apart at forty metres, and which of a fixture's four shirts
/// is therefore the pair that will be worn.
/// </summary>
/// <remarks>
/// <para>
/// The game draws this rather than asking a manager, because a manager picks his own eleven and
/// not the referee's decision about who can see whom. But it is a draw among the answers that
/// actually work rather than among all four: a rule that changed a shirt at random would put two
/// dark blue shirts on the pitch as often as it fixed them.
/// </para>
///
/// <para>
/// The home club keeps its first shirt unless the two clash, which is the convention the whole
/// world plays by and the reason a manager can recognise his own players without looking at the
/// scoreboard.
/// </para>
/// </remarks>
public static class KitClash
{
    /// <summary>
    /// Two bodies this close in light are the same colour to a spectator.
    /// </summary>
    public const double BodyThreshold = 1.6;

    /// <summary>
    /// The same two bodies, both of them striped or halved, are told apart by their shape as
    /// much as by their colour: four white pinstripes on a dark shirt are four bands even when
    /// the dark is very dark.
    /// </summary>
    public const double PatternedThreshold = 1.25;

    /// <summary>
    /// Whether the two shirts cannot be told apart.
    ///
    /// <para>
    /// Only the bodies are looked at, and that is the whole rule. Two dark bodies are one dark
    /// mass however differently they are trimmed, which is why a dark blue club changes out of
    /// its shirt against a dark green one even when the green club's trim is a yellow and the
    /// blue club's is a white: from the other side of a stadium those are two dark shirts.
    /// </para>
    ///
    /// <para>
    /// The trims are not in it because a trim is not what tells two teams apart — it is what
    /// tells you whose shirt you are looking at when there is only one of them on the pitch. A
    /// rule that moved a club's shirt because its white trim was the colour of the other team's
    /// body would change shirts in fixtures nobody had any trouble watching.
    /// </para>
    /// </summary>
    public static bool AreIndistinguishable(KitDesign first, KitDesign second)
    {
        var threshold = Patterned(first) && Patterned(second)
            ? PatternedThreshold
            : BodyThreshold;

        return ClubColours.Contrast(first.PrimaryColor, second.PrimaryColor) < threshold;
    }

    /// <summary>
    /// Which shirt each of the two clubs is playing in, drawn from the match's own seed.
    /// </summary>
    /// <remarks>
    /// The seed is the match's so the draw is part of the match's identity: a fixture that is
    /// abandoned and replayed changes shirt in the same way both times.
    /// </remarks>
    public static (KitSide Home, KitSide Away) Decide(
        KitDesign homeHome,
        KitDesign? homeAway,
        KitDesign awayHome,
        KitDesign? awayAway,
        int seed)
    {
        if (!AreIndistinguishable(homeHome, awayHome))
        {
            return (KitSide.Home, KitSide.Home);
        }

        var options = new List<(KitSide Home, KitSide Away)>();

        if (awayAway is not null)
        {
            options.Add((KitSide.Home, KitSide.Away));
        }

        if (homeAway is not null)
        {
            options.Add((KitSide.Away, KitSide.Home));

            if (awayAway is not null)
            {
                options.Add((KitSide.Away, KitSide.Away));
            }
        }

        var workable = options
            .Where(option => !AreIndistinguishable(
                option.Home is KitSide.Away ? homeAway! : homeHome,
                option.Away is KitSide.Away ? awayAway! : awayHome))
            .ToList();

        // A club with two shirts that are all of each other's colour leaves nothing to choose,
        // and a coin between two unreadable shirts is a coin that decides nothing.
        if (workable.Count == 0)
        {
            return (KitSide.Home, KitSide.Home);
        }

        var index = (int)((uint)seed % (uint)workable.Count);

        return workable[index];
    }

    private static bool Patterned(KitDesign kit) =>
        kit.Pattern is not (KitPattern.Solid or KitPattern.SolidSeparateSleeves);
}
