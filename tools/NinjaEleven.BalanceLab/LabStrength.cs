using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// The three numbers a side is worth, measured with a curve the laboratory chooses.
///
/// <para>
/// This is <c>TeamStrength</c> with one thing changed: the ramp is a parameter instead of a
/// hardcoded one. Everything else — the three unit readings, the formation scaling, the
/// goalkeeper's share of the defence, the penalty for a missing man — is copied from
/// <c>AttributeWeights</c> and <c>MatchRules</c> rather than written out again here, because a
/// laboratory that copies the model by hand is a laboratory that reports on a model nobody
/// plays.
/// </para>
///
/// <para>
/// <see cref="BalanceInvariantTests.TheLaboratoryStillMeasuresTheEngine"/> is the assertion
/// that holds it there, and it is worth noting what it now asserts: with
/// <see cref="Fidelity.Production"/> and attributes on the 1..100 scale, this class reproduces
/// <c>TeamStrength.Of</c> exactly.
/// </para>
/// </summary>
public static class LabStrength
{
    /// <summary>How the attack, midfield and defence of a set of cards come out, for one curve.</summary>
    public readonly record struct Numbers(double Attack, double Midfield, double Defense, int Players)
    {
        /// <summary>Mirrors <c>TeamStrength.Initiative</c>: midfield decides who plays, attack decides what happens.</summary>
        public double Initiative => Midfield + Attack * MatchRules.InitiativeAttackWeight;
    }

    public static Numbers Of(IEnumerable<PlayerCard> eleven, IEnergyCurve curve)
    {
        ArgumentNullException.ThrowIfNull(eleven);
        ArgumentNullException.ThrowIfNull(curve);

        var onPitch = eleven.ToList();

        var attack = 0.0;
        var midfield = 0.0;
        var defense = 0.0;

        foreach (var player in onPitch)
        {
            var effective = curve.Factor(player.Energy) * Reading(player);

            switch (player.Position)
            {
                case Position.ATT:
                    attack += effective;
                    break;
                case Position.MID:
                    midfield += effective;
                    break;
                case Position.DEF:
                    defense += effective;
                    break;
            }
        }

        // A keeper's share of the defence, on the same ramp as everybody else: the keeper is
        // read on his reflexes and his power and is not the one place where the curve is
        // quietly different from everybody else's.
        var keeper = onPitch.FirstOrDefault(player => player.Position == Position.GK);
        var keeperValue = keeper is null
            ? 0.0
            : KeeperReading(keeper) * curve.Factor(keeper.Energy);

        var defenders = onPitch.Count(player => player.Position == Position.DEF);
        var midfielders = onPitch.Count(player => player.Position == Position.MID);
        var attackers = onPitch.Count(player => player.Position == Position.ATT);

        var attackFactor = 0.72 + attackers * 0.095 + midfielders * 0.018 - defenders * 0.018;
        var midfieldFactor = 0.72 + midfielders * 0.075 + defenders * 0.018 - attackers * 0.012;
        var defenseFactor = 0.72 + defenders * 0.09 + midfielders * 0.018 - attackers * 0.022;

        var absence = Math.Max(0, 11 - onPitch.Count);

        return new Numbers(
            attack * attackFactor * (1 - absence * MatchRules.AbsenceAttackPenalty),
            midfield * midfieldFactor * (1 - absence * MatchRules.AbsenceMidfieldPenalty),
            (defense + keeperValue * MatchRules.KeeperShareOfDefense) * defenseFactor * (1 - absence * MatchRules.AbsenceDefensePenalty),
            onPitch.Count);
    }

    /// <summary>
    /// The unit reading of a card, from the same weights production uses. The card carries
    /// the five attributes rather than a snapshot, so the weight set is applied here directly
    /// — it is the same set of five numbers, read the same way.
    /// </summary>
    private static double Reading(PlayerCard player)
    {
        var weights = AttributeWeights.For(player.Position);

        return weights.Speed * player.Speed
            + weights.Accuracy * player.Accuracy
            + weights.Dribbling * player.Dribbling
            + weights.Heading * player.Heading
            + weights.Strength * player.Strength;
    }

    /// <summary>A keeper's own two attributes, on the same share production uses.</summary>
    private static double KeeperReading(PlayerCard keeper) =>
        keeper.Reflexes * MatchRules.KeeperReflexShare
        + keeper.GoalkeeperPower * (1 - MatchRules.KeeperReflexShare);

    /// <summary>
    /// The inverse question: given two sides and a curve, how much of the difference between
    /// them survives as a difference in what they can do.
    ///
    /// <para>
    /// This is the number the whole exercise turns on, and it is deliberately a ratio of
    /// <em>effective</em> numbers rather than a probability. A probability answers "who wins";
    /// this answers "how much of the quality gap is still there once energy has had its say",
    /// which is the question of whether energy is a tax or a veto.
    /// </para>
    /// </summary>
    public static double QualityGapSurvives(
        IReadOnlyList<PlayerCard> stronger,
        IReadOnlyList<PlayerCard> weaker,
        IEnergyCurve curve)
    {
        var a = Of(stronger, curve).Initiative;
        var b = Of(weaker, curve).Initiative;

        return b <= 0 ? double.PositiveInfinity : a / b;
    }
}
