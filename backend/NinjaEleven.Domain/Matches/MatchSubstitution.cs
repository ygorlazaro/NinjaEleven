using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// The rules of a swap, in one place.
///
/// Substitution is the one place where the engine and the manager's command meet, and the
/// two had learned different rules: the engine would happily put two goalkeepers on the
/// pitch, and the service refused to explain why. Both now go through here, so what a
/// manager is allowed to do by hand and what the engine does on its own cannot drift apart.
/// </summary>
public static class MatchSubstitution
{
    /// <summary>
    /// Whether the swap is a legal one. It is not about the eleven being any good, it is
    /// about the club still having a goalkeeper afterwards.
    /// </summary>
    public static bool CanSwap(
        IReadOnlyCollection<MatchPlayerSnapshot> lineup,
        MatchPlayerSnapshot outgoing,
        MatchPlayerSnapshot incoming)
    {
        if (lineup is null) throw new ArgumentNullException(nameof(lineup));
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (incoming is null) throw new ArgumentNullException(nameof(incoming));

        if (outgoing.PlayerId == incoming.PlayerId)
        {
            return false;
        }

        // A substitute is spent. A manager who has taken a man off cannot name him again
        // five minutes later, and the engine is held to the same rule when it substitutes
        // on its own: the bench is not a revolving door.
        if (incoming.SubbedOff)
        {
            return false;
        }

        // The last goalkeeper of a club can only be replaced by another goalkeeper. An
        // outfielder taking his place is what the engine does in an emergency, not
        // something anybody gets to choose.
        if (outgoing.KeepsGoal && !incoming.KeepsGoal && lineup.Count(player => player.KeepsGoal) == 1)
        {
            return false;
        }

        // A real goalkeeper arriving while an improvised one is in goal is a rescue, not a
        // swap, and the improvised keeper gives the gloves back rather than playing on.
        if (incoming.Position == Position.GK && !incoming.EmergencyGK && incoming.KeepsGoal)
        {
            var improvised = lineup.FirstOrDefault(player =>
                player.PlayerId != outgoing.PlayerId &&
                player.PlayerId != incoming.PlayerId &&
                player.EmergencyGK &&
                player.KeepsGoal);

            if (improvised is not null && improvised.PlayerId != outgoing.PlayerId)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Performs the swap. The outgoing player takes the incoming one's place and vice
    /// versa, the substitution is counted against the club, and the shape of the side is
    /// recalculated — replacing a striker for a defender does not only change two names.
    /// </summary>
    public static void Swap(
        MatchState state,
        bool home,
        MatchPlayerSnapshot outgoing,
        MatchPlayerSnapshot incoming)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (incoming is null) throw new ArgumentNullException(nameof(incoming));

        var lineup = home ? state.HomeLineup : state.AwayLineup;
        var bench = home ? state.HomeBench : state.AwayBench;

        var outIndex = lineup.IndexOf(outgoing);
        var inIndex = bench.IndexOf(incoming);

        if (outIndex < 0 || inIndex < 0)
        {
            return;
        }

        lineup[outIndex] = incoming;
        bench[inIndex] = outgoing;
        incoming.SubbedIn = true;

        // Both men have now played the match: the one who came on, and the one who was
        // there when the decision was made to take him off. And the outgoing one is spent:
        // he is on the bench, but he is not in the match any more.
        incoming.PlayedInMatch = true;
        outgoing.PlayedInMatch = true;
        outgoing.SubbedOff = true;

        // The minutes each of them played, because the recovery at the end of the match is
        // measured against them. A man who came on at eighty-five has not played a match,
        // and a snapshot that could not say so would pay him as if he had.
        incoming.EnteredAtMinute = state.Minute;
        outgoing.LeaveThePitchAt(state.Minute);

        if (home)
        {
            state.SubstitutionsHome++;
            state.HomeFormation = Formation.FromComposition(lineup);
        }
        else
        {
            state.SubstitutionsAway++;
            state.AwayFormation = Formation.FromComposition(lineup);
        }

        // The gloves go back to the rack as soon as there is a keeper who wears them for a
        // living, and a player who was promoted only because nobody else was left no longer
        // defends the goal once a real one is standing there.
        if (incoming.Position == Position.GK && !incoming.EmergencyGK)
        {
            foreach (var player in lineup.Where(player => player.EmergencyGK && player.KeepsGoal))
            {
                player.ClearGoalkeeperDuty();
            }

            outgoing.ClearGoalkeeperDuty();
        }
        else if (!HasGoalkeeper(lineup))
        {
            // An outfielder answering the call: nobody in goal is a hole in a side, and the
            // man who fills it is the one who gets the gloves.
            incoming.PromoteToGoalkeeper();
        }
    }

    /// <summary>
    /// Whether the club still has a real goalkeeper, an improvised one, or nothing at all.
    /// A club with nothing is playing with an empty net, and that is a different match.
    /// </summary>
    public static bool HasGoalkeeper(IEnumerable<MatchPlayerSnapshot> lineup) =>
        lineup.Any(player => player.KeepsGoal);
}
