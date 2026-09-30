using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// What a side is worth on the pitch right now, as three numbers instead of one.
///
/// A single overall rating answers "how good is this club" and nothing else. A match needs
/// to answer three separate questions — who gets the ball, who gets behind the ball, and
/// who is waiting for it — and a team that is strong in one and weak in another plays a
/// different match from a team that is merely good.
///
/// So the eleven is read unit by unit. Each player's contribution is his attributes for
/// his role, weighted by the energy he has left, and the three totals are then shaped by
/// the formation: three attackers make an attack, four defenders make a defence, and the
/// shape is the reason 4-4-2 and 4-3-3 are not the same eleven rearranged.
///
/// A missing player is not only one less body, it is a hole in one of the three numbers,
/// which is why a team that loses its striker and a team that loses its centre back do not
/// limp in the same way.
/// </summary>
public sealed class TeamStrength
{
    /// <summary>
    /// The attacking value of the eleven: every forward on the pitch, weighted by the
    /// energy he has to run on.
    /// </summary>
    public double Attack { get; }

    /// <summary>
    /// The midfield value: the unit that decides who plays and for how long.
    /// </summary>
    public double Midfield { get; }

    /// <summary>
    /// The defensive value: the back four and the keeper's share of it.
    /// </summary>
    public double Defense { get; }

    /// <summary>
    /// How many of the eleven are actually on the pitch. It is here because a red card
    /// and a knock are not the same absence: a club short of men plays a different match,
    /// and the number of them is part of what it is worth.
    /// </summary>
    public int Players { get; }

    /// <summary>
    /// The one number that decides who starts the next action. Midfield weighted heavily
    /// and attack on top of it, because the side that controls the game plays more of it
    /// and the side with the better forwards converts more of what it does.
    /// </summary>
    public double Initiative => Midfield + Attack * MatchRules.InitiativeAttackWeight;

    private TeamStrength(double attack, double midfield, double defense, int players)
    {
        Attack = attack;
        Midfield = midfield;
        Defense = defense;
        Players = players;
    }

    /// <summary>
    /// Measures the eleven that is on the pitch, and only the eleven that is on the
    /// pitch: a sent off or a substituted player contributes nothing to what is happening
    /// now, and counting him would make a team stronger by being reduced.
    /// </summary>
    public static TeamStrength Of(IEnumerable<MatchPlayerSnapshot> eleven)
    {
        if (eleven is null) throw new ArgumentNullException(nameof(eleven));

        var onPitch = eleven.Where(player => player.IsOnPitch).ToList();
        var formation = Formation.FromComposition(onPitch);

        var attack = 0.0;
        var midfield = 0.0;
        var defense = 0.0;

        foreach (var player in onPitch)
        {
            // One ramp, one place. The three unit readings live in
            // <see cref="AttributeWeights"/> and the ramp in <see cref="EnergyCurve"/>, so
            // there is no fourth answer to "how tired is he" left in this method.
            var effective = EnergyCurve.Factor(player.Energy)
                * AttributeWeights.Of(player, AttributeWeights.For(player.Position));

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

        // A club with nobody in goal still defends: whatever outfield player is standing
        // in front of an empty net is the last line, and he is worth a fraction of a
        // keeper rather than nothing. The keeper is on the same ramp as everybody else, which
        // is the point: a tired keeper is a worse keeper by the same rule that a tired
        // centre back is a worse centre back.
        var keeper = onPitch.FirstOrDefault(player => player.KeepsGoal);
        var keeperValue = keeper is null
            ? 0.0
            : AttributeWeights.KeeperAbility(keeper) * EnergyCurve.Factor(keeper.Energy);

        // The shape of the eleven scales each unit. More forwards means a heavier attack
        // and a lighter defence, and that trade is the whole point of choosing a
        // formation rather than picking eleven good men and leaving it there.
        var attackFactor = 0.72 + formation.Attackers * 0.095 + formation.Midfielders * 0.018 - formation.Defenders * 0.018;
        var midfieldFactor = 0.72 + formation.Midfielders * 0.075 + formation.Defenders * 0.018 - formation.Attackers * 0.012;
        var defenseFactor = 0.72 + formation.Defenders * 0.09 + formation.Midfielders * 0.018 - formation.Attackers * 0.022;

        // Every man missing is a hole, and the unit he belonged to feels it most.
        var absence = Math.Max(0, 11 - onPitch.Count);

        return new TeamStrength(
            attack * attackFactor * (1 - absence * MatchRules.AbsenceAttackPenalty),
            midfield * midfieldFactor * (1 - absence * MatchRules.AbsenceMidfieldPenalty),
            (defense + keeperValue * MatchRules.KeeperShareOfDefense) * defenseFactor * (1 - absence * MatchRules.AbsenceDefensePenalty),
            onPitch.Count);
    }
}
