using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// How good a player is, and what playing him costs, in the terms the engine needs.
///
/// A single "overall rating" was the old answer and it was wrong in a way that showed on
/// the pitch: it is high for goalkeepers by design, so rating an eleven put two or three
/// of them on the field, and it knows nothing about what a player is for. A left back and
/// a centre forward are rated on the same sum of attributes, so a club was told its best
/// eleven was a set of footballers.
///
/// These are the ratings the model actually uses, and each one answers a different
/// question: <see cref="Metric"/> is who to pick, <see cref="KeeperAbility"/> is what is
/// in goal, <see cref="InjuryChance"/> is how likely he is to go off, and
/// <see cref="AgeCost"/> is what a match costs him.
/// </summary>
public static class PlayerMetric
{
    /// <summary>
    /// A goalkeeper's ability on the scale the outfield attributes use.
    ///
    /// A real goalkeeper is rated on his reflexes and his power, which is what the role is.
    /// A promoted outfield player is rated on the three attributes that stand in for a pair of
    /// gloves — and rated low, on purpose: a club that has run out of keepers is playing a
    /// different match, and the number says so.
    /// </summary>
    public static double KeeperAbility(MatchPlayerSnapshot player) =>
        AttributeWeights.KeeperAbility(player);

    /// <summary>
    /// What a player is worth to his unit, as a single number on the attribute scale. It is
    /// deliberately position-aware, because "the best eleven" is a claim about roles and not
    /// about a sum of attributes:
    ///
    /// - an attacker is judged on what he does with the ball, and carries the game;
    /// - a midfielder is judged on control and on the ball he can win;
    /// - a defender is judged on what he holds, covers and heads.
    ///
    /// Energy is multiplied in rather than added, so a tiring player drops in the order but
    /// never disappears from it: the ramp's floor (<see cref="EnergyCurve"/>) means a manager
    /// who has to choose still sees the better footballer, and sees how tired he is. An
    /// <i>additive</i> energy term was the shape this used to have, and on the 1..100
    /// attribute scale it was worth about three points against a tactical sum in the hundreds
    /// — which is a term that cannot change a single ordering, and so a term carrying a
    /// promise the code did not keep.
    /// </summary>
    public static double Metric(MatchPlayerSnapshot player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        if (player.KeepsGoal)
        {
            return KeeperAbility(player) * EnergyCurve.Factor(player);
        }

        return AttributeWeights.Of(player, AttributeWeights.For(player.Position))
            * EnergyCurve.Factor(player);
    }

    /// <summary>
    /// What a player is worth to a line that exists for a particular job.
    ///
    /// <see cref="Metric"/> is the right question for "who are the best eleven", and the
    /// wrong one for a holding midfielder: rated as an attacker on merit, the best passer in
    /// the squad is picked ahead of the man who stands in front of the back four, and the
    /// tactic the manager ordered is quietly not the one on the pitch.
    ///
    /// So a line that is there to hold is filled by men measured on holding, and a line
    /// that is there to create by men measured on creating — two different readings of the
    /// same body, which is why the profile has to pick the rating and not merely adjust the
    /// general one. A uniform adjustment would score a holding midfielder and a creative
    /// one identically, because both would get the same nudge in opposite directions, and
    /// the shape would be a label again.
    ///
    /// It is a blend rather than a filter. The general metric keeps
    /// <c>1 - <see cref="MatchRules.LineJobWeight"/></c> of the weight, so a man who is
    /// clearly the best footballer available still starts whichever line he is told to
    /// play in: a tactic is a preference about roles, not a list of men the manager is not
    /// allowed to pick.
    /// </summary>
    public static double TacticalMetric(MatchPlayerSnapshot player, TacticProfile profile)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        var baseMetric = Metric(player);

        if (profile == TacticProfile.Balanced)
        {
            return baseMetric;
        }

        var jobRating = profile == TacticProfile.Attacking
            ? CreatingRating(player)
            : HoldingRating(player);

        // Energy is already factored into baseMetric and jobRating alike, so the blend
        // compares two readings of the same man at the same tiredness rather than letting
        // one of them carry the fatigue and the other not.
        return baseMetric * (1.0 - MatchRules.LineJobWeight) + jobRating * MatchRules.LineJobWeight;
    }

    /// <summary>
    /// A player read as the man who stands in front of the back four, whatever line he is
    /// nominally in. A forward told to hold does not become a midfielder, and is rated
    /// accordingly: the holding reading of a striker leans on the attributes he has least of,
    /// which is what being told to hold costs him.
    /// </summary>
    private static double HoldingRating(MatchPlayerSnapshot player) =>
        AttributeWeights.Of(player, AttributeWeights.Holding) * EnergyCurve.Factor(player);

    /// <summary>
    /// A player read as the man who makes the thing happen, whatever line he is nominally
    /// in. A full back told to get forward is still a full back, rated on what he can do
    /// with the ball rather than on what he can hold.
    /// </summary>
    private static double CreatingRating(MatchPlayerSnapshot player) =>
        AttributeWeights.Of(player, AttributeWeights.Creating) * EnergyCurve.Factor(player);

    /// <summary>
    /// What a match costs this player, relative to one of his age playing at full tilt.
    /// The young are cheap because they recover, the old are expensive because they do
    /// not, and the middle is the baseline.
    /// </summary>
    public static double AgeCost(MatchPlayerSnapshot player) => player.Age switch
    {
        >= 34 => 1.18,
        <= 21 => 0.78,
        <= 28 => 0.90,
        _ => 1.0
    };

    /// <summary>
    /// What a knock costs this player on top of the match itself. An injury is not only
    /// the absence: a player playing through one is carrying it into every sprint.
    /// </summary>
    public static double InjuryCost(MatchPlayerSnapshot player) => player.Injury switch
    {
        Injury.Grave => 1.35,
        Injury.Light => 1.22,
        _ => 1.0
    };

    /// <summary>
    /// How tired this player is, on a scale from minus one to one where zero is
    /// <see cref="MatchRules.ReferenceEnergy"/>.
    /// </summary>
    /// <remarks>
    /// It is its own number and not a reading of the energy, because a tired man is worse at
    /// what he does and not merely less energetic: the same eighty points means something
    /// different at the twelfth penalty of a match than it did at the first, and the engine
    /// needs to be able to say so without inventing a second scale for every place it reads
    /// energy. Positive is tired, negative is fresh, and the two ends are the ends of the
    /// scale rather than numbers that grow.
    /// </remarks>
    public static double Fatigue(MatchPlayerSnapshot player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        return Math.Clamp(
            (MatchRules.ReferenceEnergy - player.Energy) / (double)MatchRules.FatigueSpan,
            -1.0,
            1.0);
    }

    /// <summary>
    /// How much of what a window of rest is worth this player actually gets back, by age.
    /// </summary>
    /// <remarks>
    /// It is the other half of <see cref="AgeCost"/> and it says the opposite thing on
    /// purpose: a young man is cheap to run and quick to put back together, an old one is
    /// dearer to run and slower to mend, and a model that only had one of the two would say
    /// that old players are cheap in every way or expensive in every way. Neither is true,
    /// and the difference between them is the whole reason a club rotates its striker and
    /// keeps its goalkeeper.
    /// </remarks>
    public static double AgeRecovery(int age) => age switch
    {
        >= 34 => 0.80,
        <= 21 => 1.15,
        <= 28 => 1.05,
        _ => 0.95
    };

    /// <summary>How much of a window of rest this player gets back, by his age.</summary>
    public static double AgeRecovery(MatchPlayerSnapshot player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        return AgeRecovery(player.Age);
    }

    /// <summary>
    /// How likely this player is to pick up a knock during an action. Age and tiredness
    /// are the two things that decide it, and a player who has already been kicked this
    /// match is markedly more likely to be kicked again.
    /// </summary>
    public static double InjuryChance(MatchPlayerSnapshot player)
    {
        var age = (player.Age - MatchRules.InjuryAgeReference) / MatchRules.InjuryAgeSpan;
        var fatigue = (100 - player.Energy) / 100.0;

        var chance = Math.Min(
            MatchRules.MaxInjuryChance,
            MatchRules.InjuryBaseChance + age * MatchRules.InjuryAgeWeight + fatigue * MatchRules.InjuryFatigueWeight);

        return chance * (player.Injury == Injury.Light ? MatchRules.ReinjuryMultiplier : 1.0);
    }

    /// <summary>
    /// Whether a knock is going to end this player's match. A player who has already been
    /// hurt, or who is out of energy, is far likelier to be hurt badly than to get away
    /// with it.
    /// </summary>
    public static bool IsSevereInjury(MatchPlayerSnapshot player, double roll)
    {
        var baseChance = player.Injury == Injury.Light
            ? MatchRules.SevereInjuryAfterKnockChance
            : MatchRules.SevereInjuryBaseChance;

        var chance = Math.Min(
            MatchRules.MaxSevereInjuryChance,
            baseChance + (100 - player.Energy) / 220.0 + (player.Age >= MatchRules.SevereInjuryAge ? MatchRules.OldPlayerSevereBonus : 0.0));

        return roll < chance;
    }
}
