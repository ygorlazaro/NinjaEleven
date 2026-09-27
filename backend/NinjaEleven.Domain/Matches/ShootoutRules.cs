namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Who may walk to the spot, and in which order, when a cup tie has gone to penalties.
///
/// This is the part of a shootout the Laws say very little about in words and a great deal
/// about in consequences, and it is decided here rather than on the screen for two reasons:
/// a manager choosing from a list of eleven may only choose from men who are allowed, and
/// the side nobody is watching has to arrive at the same list on its own.
///
/// The Laws, as they are implemented:
///
///   **Only the men who were on the pitch may take.** A man who came on, a man who was
///   substituted off, a man who is there because a keeper went down — all of them played
///   the match, and all of them may take. A man who never set foot on it may not, however
///   good he is, because the shootout is the match's and not the squad's.
///
///   **A man sent off may not take.** He is not an outfield player any more and the Laws
///   name him by exclusion. A player who is hurt may: he left the pitch for a moment, and
///   the Laws are explicit that a man who was temporarily off counts as one who was on.
///
///   **A side with more men than its opponent sends fewer.** A club that has a player the
///   other has not — a sending-off, an injury that took somebody off the pitch — has to
///   leave the surplus man at the centre circle, name him to the referee, and he may not
///   take. The surplus is taken off the *weakest* men, because a club that is a man down
///   does not choose to lose its best taker: it loses the man it can least afford to be
///   without, and the manager picks the order of what is left.
///
///   **A goalkeeper who cannot carry on is replaced by a man who can.** That is the same
///   rule the match itself follows, and it is not repeated here: the engine has already
///   put a man in goal before the whistle goes for the shootout.
/// </summary>
public static class ShootoutRules
{
    /// <summary>
    /// The men of one side who may take, best first — the order a manager would read them
    /// in, and the order the other club's five are drawn from.
    /// </summary>
    /// <param name="squad">The eleven and the bench of the side, as the match ended.</param>
    public static IReadOnlyList<MatchPlayerSnapshot> EligibleTakers(
        IEnumerable<MatchPlayerSnapshot> squad)
    {
        ArgumentNullException.ThrowIfNull(squad);

        return squad
            .Where(player => player.PlayedInMatch)
            .Where(player => !player.RedCard)
            .Where(player => !player.EmergencyGK)
            .OrderByDescending(player => MatchEngine.PenaltyConversion(player, null))
            .ThenBy(player => player.PlayerId)
            .ToList();
    }

    /// <summary>
    /// The order of one side's five, drawn from its eligible men.
    ///
    /// This is the order the engine picks for the club no manager is watching, and it is the
    /// same order the manager's own screen shows him: best taker first, and the man with
    /// the worst chance left off when there are more men than kicks. A shootout taken by a
    /// club that nobody chose is still a shootout, and it is decided by the same players a
    /// manager would have sent up.
    /// </summary>
    /// <param name="eligible">The side's eligible men, best first.</param>
    /// <param name="allowed">How many of them may take, after the reduction.</param>
    public static IReadOnlyList<Guid> DrawOrder(IReadOnlyList<MatchPlayerSnapshot> eligible, int allowed) =>
        eligible.Take(allowed).Select(player => player.PlayerId).ToList();
}
