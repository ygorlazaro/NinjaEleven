using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// One confrontation, resolved without a match around it: an attacker, a defender, a keeper
/// and a single chance.
///
/// <para>
/// A full ninety minutes is the wrong instrument for the question being asked. The question
/// is "given these two men and these two amounts of energy, who takes this chance", and a
/// match answers it two hundred times over with injuries, cards, substitutions and the clock
/// in the way. A confrontation is the chance itself, rolled a hundred thousand times, which
/// makes it both faster and <em>more</em> precise: a hundred thousand matches measure the
/// match model, and a hundred thousand chances measure the thing the curve touches.
/// </para>
///
/// <para>
/// The resolution is the engine's own — the same shot power, the same save power, the same
/// clamped band — so the numbers this class prints are numbers the engine would produce for
/// the same chance. What is parameterised is the ramp: the shooter's energy enters through
/// the curve rather than as a straight <c>/100</c>.
/// </para>
/// </summary>
public sealed class DuelSimulator
{
    private readonly IEnergyCurve _curve;

    public DuelSimulator(IEnergyCurve curve)
    {
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));
    }

    /// <summary>
    /// What the shooter is worth on this chance, on the -1..1 scale the engine compares on.
    ///
    /// <para>
    /// Production reads finishing, control and pace off
    /// <c>AttributeWeights.Shot</c> and then places the reading on the attribute scale. The
    /// laboratory applies the energy curve to the reading first, because those are two
    /// different claims: scaling the reading says a tired man shoots worse and controls worse
    /// and runs worse, which is what the curve is for; scaling the result says the three
    /// degrade together, which is the same claim with the arithmetic hidden.
    /// </para>
    /// </summary>
    public double ShooterPower(PlayerCard shooter) =>
        AttributeScale.Factor(
            Reading(shooter, AttributeWeights.Shot) * _curve.Factor(shooter.Energy));

    /// <summary>
    /// What the keeper is worth on this chance, on the same scale as the shooter.
    /// </summary>
    public double KeeperPower(PlayerCard keeper) =>
        AttributeScale.Factor(
            (keeper.Reflexes * MatchRules.KeeperReflexShare
             + keeper.GoalkeeperPower * (1 - MatchRules.KeeperReflexShare))
            * _curve.Factor(keeper.Energy));

    /// <summary>
    /// The chance the ball goes in, which is the engine's own band: the base, the swing of
    /// the gap between the two men, and the clamps at 0.12 and 0.50.
    /// </summary>
    /// <remarks>
    /// The old form was <c>(ShooterPower − KeeperPower + 14) / 54</c> with both powers as raw
    /// sums on 1..100, and the constants were the 1..20 scale the attributes were written
    /// on. The offset was a third of a shooter's power and the divisor was barely half of the
    /// range the attributes actually span, so the band saturated at both ends and the quality
    /// difference between two ordinary strikers stopped being visible. Comparing two -1..1
    /// readings is the same comparison the engine makes.
    /// </remarks>
    public double GoalChance(PlayerCard shooter, PlayerCard keeper) =>
        Math.Clamp(
            MatchRules.BaseGoalChance
                + MatchRules.GoalChanceSwing * (ShooterPower(shooter) - KeeperPower(keeper)),
            MatchRules.MinGoalChance,
            MatchRules.MaxGoalChance);

    /// <summary>
    /// How likely the attacker is to get the chance at all, which is where the defender and
    /// the dribble duel live in the engine's attack sequence.
    /// </summary>
    public double ChanceCreation(PlayerCard attacker, PlayerCard defender) =>
        Math.Clamp(
            MatchRules.DuelBaseChance
                + MatchRules.DuelSwing * (
                    Skill(attacker, AttributeWeights.Dribble) - Skill(defender, AttributeWeights.Dribble)),
            MatchRules.MinDuelChance,
            MatchRules.MaxDuelChance);

    private double Skill(PlayerCard player, AttributeWeights.Weights weights) =>
        AttributeScale.Factor(Reading(player, weights) * _curve.Factor(player.Energy));

    private static double Reading(PlayerCard player, AttributeWeights.Weights weights) =>
        weights.Speed * player.Speed
        + weights.Accuracy * player.Accuracy
        + weights.Dribbling * player.Dribbling
        + weights.Heading * player.Heading
        + weights.Strength * player.Strength;

    /// <summary>
    /// Whether the attacker beats the defender to make the chance at all. A separate roll
    /// because a team that cannot create anything is a different failure from a team that
    /// creates and cannot finish, and folding them together hides which one is happening.
    /// </summary>
    public bool CreatesChance(PlayerCard attacker, PlayerCard defender, LabRandom random) =>
        random.NextDouble() < ChanceCreation(attacker, defender);

    /// <summary>Whether the chance goes in.</summary>
    public bool Scores(PlayerCard shooter, PlayerCard keeper, LabRandom random) =>
        random.NextDouble() < GoalChance(shooter, keeper);

    /// <summary>
    /// A whole confrontation: the attacker has to win the ball, get past his man and finish.
    /// </summary>
    public bool Wins(PlayerCard attacker, PlayerCard defender, PlayerCard keeper, LabRandom random) =>
        CreatesChance(attacker, defender, random) && Scores(attacker, keeper, random);
}
