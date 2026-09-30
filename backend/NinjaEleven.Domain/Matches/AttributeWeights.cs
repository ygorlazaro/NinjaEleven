using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// What each attribute is worth, in each of the places the engine reads one.
///
/// <para>
/// The engine used to carry its weights where it happened to need them: a sum of three
/// attributes written out in <c>TeamStrength</c>, a different three multiplied by 1.25 in
/// <c>PlayerMetric</c>, a third set in the shot resolution, a fourth in the penalty, and a
/// fifth in the two tactical ratings. Six readings of the same five numbers, and no way to
/// ask whether a striker who is good at finishing is a striker the model agrees with — the
/// only way to find out was to read five methods and compare them by eye.
/// </para>
///
/// <para>
/// Everything is here now, as a weight over the attributes a role is made of, normalised to
/// sum to one. Two consequences worth keeping: a role is one reading of a player rather than
/// five, and the weights can be reasoned about together — which is what makes "a midfielder's
/// job is control" a claim about a number rather than a claim about a line of code.
/// </para>
///
/// <para>
/// The weight sets sum to one because they are <em>readings</em>, not sums. A raw sum of five
/// attributes on the 1..100 scale would make a keeper with high reflexes look like a great
/// footballer to every formula that reads him, which is exactly the bug an overall rating
/// was replaced for; a normalised reading of his own two goalkeeper attributes does not.
/// </para>
/// </summary>
public static class AttributeWeights
{
    /// <summary>
    /// A weight over the outfield attributes. The five weights sum to one, so
    /// <see cref="Of(MatchPlayerSnapshot, AttributeWeights)"/> is a weighted average of the
    /// player's own attributes and lands on the attribute scale.
    /// </summary>
    public readonly record struct Weights(
        double Speed,
        double Accuracy,
        double Dribbling,
        double Heading,
        double Strength)
    {
        /// <summary>The same reading over a keeper, whose goalkeeping is his reflexes and power.</summary>
        public static Weights Keeper(double reflexShare = 0.55)
        {
            var outfield = (1.0 - reflexShare) / 4.0;

            return new Weights(
                Speed: outfield,
                Accuracy: outfield,
                Dribbling: outfield * 0.5,
                Heading: outfield * 0.5,
                Strength: reflexShare);
        }

        /// <summary>Scales every weight, for the places that want a sharper or a flatter reading.</summary>
        public Weights Sharpened(double factor) => new(
            Speed * factor,
            Accuracy * factor,
            Dribbling * factor,
            Heading * factor,
            Strength * factor);

        /// <summary>Rotates the reading between two attribute pairs, for the same reason twice.</summary>
        public Weights Blended(Weights other, double share) => new(
            Speed + (other.Speed - Speed) * share,
            Accuracy + (other.Accuracy - Accuracy) * share,
            Dribbling + (other.Dribbling - Dribbling) * share,
            Heading + (other.Heading - Heading) * share,
            Strength + (other.Strength - Strength) * share);
    }

    // --- The units ---------------------------------------------------------------

    /// <summary>
    /// What an attacker is judged on: what he does with the ball, and the pace to get to it.
    /// Finishing first and dribbling almost as much, because a forward who cannot carry the
    /// ball past a man is a forward who receives it facing his own goal.
    /// </summary>
    public static readonly Weights Attack = new(
        Speed: 0.22,
        Accuracy: 0.36,
        Dribbling: 0.32,
        Heading: 0.05,
        Strength: 0.05);

    /// <summary>
    /// What a midfielder is judged on: control of the ball, the pass, and the body that wins
    /// the second ball. It is the most even of the three readings, which is what a midfielder
    /// is — no single attribute makes him and none of them is optional.
    /// </summary>
    public static readonly Weights Midfield = new(
        Speed: 0.20,
        Accuracy: 0.28,
        Dribbling: 0.24,
        Heading: 0.10,
        Strength: 0.18);

    /// <summary>
    /// What a defender is judged on: what he holds, what he covers and what he heads away.
    /// Accuracy is last and barely there, because a defender's job is the ball not reaching
    /// the target rather than being on it when it does.
    /// </summary>
    public static readonly Weights Defense = new(
        Speed: 0.24,
        Accuracy: 0.06,
        Dribbling: 0.04,
        Heading: 0.34,
        Strength: 0.32);

    /// <summary>The reading of a whole unit, decided by the position the player is playing in.</summary>
    public static Weights For(Position position) => position switch
    {
        Position.ATT => Attack,
        Position.MID => Midfield,
        Position.DEF => Defense,
        _ => Weights.Keeper()
    };

    // --- The actions -------------------------------------------------------------

    /// <summary>
    /// A shot. Finishing dominates, the ball is carried in at a quarter of the weight, and
    /// pace is a garnish — which is the reason a fast striker who cannot finish is not a
    /// striker the engine rewards, and the reason the pace is in there at all.
    /// </summary>
    public static readonly Weights Shot = new(
        Speed: 0.15,
        Accuracy: 0.65,
        Dribbling: 0.20,
        Heading: 0.0,
        Strength: 0.0);

    /// <summary>
    /// Carrying the ball at a man. Almost all of it is the ball under the foot and the
    /// strength to hold off the man behind it, and none of it is heading.
    /// </summary>
    public static readonly Weights Dribble = new(
        Speed: 0.20,
        Accuracy: 0.15,
        Dribbling: 0.50,
        Heading: 0.0,
        Strength: 0.15);

    /// <summary>
    /// The man who wins a midfield duel against a marker. It is the mirror of
    /// <see cref="Dribble"/> with the ball taken off him, which is the whole difference
    /// between the two ends of the same duel.
    /// </summary>
    public static readonly Weights Duel = new(
        Speed: 0.25,
        Accuracy: 0.15,
        Dribbling: 0.25,
        Heading: 0.10,
        Strength: 0.25);

    /// <summary>
    /// A man who stands in front of the back four, whatever line he is nominally in. Body and
    /// head for the ball he is going to be under, and half a player's pace to get to it.
    /// </summary>
    public static readonly Weights Holding = new(
        Speed: 0.15,
        Accuracy: 0.10,
        Dribbling: 0.05,
        Heading: 0.35,
        Strength: 0.35);

    /// <summary>
    /// A man who makes the thing happen, whatever line he is nominally in. The mirror of
    /// <see cref="Holding"/>: a full back told to get forward is still rated on what he can
    /// do with the ball, and what he can hold moves out of the way.
    /// </summary>
    public static readonly Weights Creating = new(
        Speed: 0.25,
        Accuracy: 0.40,
        Dribbling: 0.35,
        Heading: 0.0,
        Strength: 0.0);

    /// <summary>A goalkeeper's own two attributes, and nothing else.</summary>
    public static readonly Weights Keeping = Weights.Keeper();

    // --- Reading -----------------------------------------------------------------

    /// <summary>
    /// The player's own weighted average of these attributes, on the attribute scale and
    /// before energy. It is the reading every action formula starts from.
    /// </summary>
    public static double Of(MatchPlayerSnapshot player, Weights weights)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        return weights.Speed * player.Speed
            + weights.Accuracy * player.Accuracy
            + weights.Dribbling * player.Dribbling
            + weights.Heading * player.Heading
            + weights.Strength * player.Strength;
    }

    /// <summary>
    /// What a goalkeeper is worth in goal, on the attribute scale.
    ///
    /// <para>
    /// A real keeper is read on his reflexes and his power, which is what the role is. A
    /// promoted outfield player is read on the three attributes that stand in for a pair of
    /// gloves and rated far below one, on purpose: a club that has run out of keepers is
    /// playing a different match, and the number says so.
    /// </para>
    /// </summary>
    public static double KeeperAbility(MatchPlayerSnapshot player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        if (!player.EmergencyGK)
        {
            return player.Reflexes * MatchRules.KeeperReflexShare
                + player.GoalkeeperPower * (1.0 - MatchRules.KeeperReflexShare);
        }

        var improvised = (player.Speed + player.Strength + player.Accuracy) / 3.0
            * MatchRules.EmergencyKeeperScale;

        return Math.Max(MatchRules.EmergencyKeeperFloor, improvised);
    }
}
