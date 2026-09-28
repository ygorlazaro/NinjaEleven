using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One player's line in a top scorers table: what he scored, and everything the order of the
/// table is allowed to look at besides that.
/// </summary>
/// <remarks>
/// The goals are what the table is about. The three other numbers are there because a top
/// scorers table is also a prize list, and a prize cannot be handed to the second of two
/// players level on goals by whatever the database happened to return first.
/// </remarks>
public class ScorerStanding
{
    /// <summary>
    /// What a yellow card counts for in the order, and what a red counts for.
    /// </summary>
    /// <remarks>
    /// A red is three yellows' worth of it, which is the same arithmetic the rest of football
    /// uses to weigh a booking: it is not that a red is three times worse in a table, it is that
    /// a red is a yellow that stopped the man playing, and the count is a way of saying so.
    /// They are one point and three so that a player with two reds is placed after a player with
    /// two yellows, and not level with a player on six.
    /// </remarks>
    public const int YellowCardPoints = 1;

    /// <inheritdoc cref="YellowCardPoints"/>
    public const int RedCardPoints = 3;

    public required Guid PlayerId { get; init; }

    /// <summary>Goals scored in the competition this table is of.</summary>
    public required int Goals { get; init; }

    /// <summary>
    /// Games he played: started plus came off the bench. A man who was on the bench for the
    /// whole match has no appearance, because he did not play.
    /// </summary>
    public required int Appearances { get; init; }

    /// <summary>
    /// The cards, weighted: <see cref="YellowCardPoints"/> a yellow and
    /// <see cref="RedCardPoints"/> a red.
    /// </summary>
    public required int CardPoints { get; init; }

    /// <summary>
    /// When he was born, or null when the game does not know. It is the last thing the order
    /// looks at, and a player with no birth date is not treated as the youngest man in the
    /// country: he is treated as a man it knows nothing about, which is to say he stays level
    /// with another such man and is decided by the line below.
    /// </summary>
    public DateOnly? BornOn { get; init; }

    /// <summary>Where the player stands, counted from one and decided by the chain.</summary>
    public int Position { get; internal set; }

    /// <summary>
    /// Which prize the line is paid, counted from one, and zero when the line is not paid one.
    /// </summary>
    /// <remarks>
    /// It is not the same number as <see cref="Position"/> when players are level: two men tied
    /// for second are both paid the second prize, and the third prize is then paid to nobody.
    /// A prize list that gave one of them the second and the other the third would be splitting
    /// one place's money in two, and the pair of them would have earned less between them than
    /// the runner-up of any other season.
    /// </remarks>
    public int PrizeSlot { get; internal set; }

    /// <summary>
    /// How many other lines share this position, and zero when nobody does.
    /// </summary>
    public int TiedWith { get; internal set; }

    /// <summary>Builds a line from raw numbers, weighting the cards as the rules weight them.</summary>
    public static ScorerStanding From(
        Guid playerId,
        int goals,
        int appearances,
        int yellowCards,
        int redCards,
        DateOnly? bornOn) => new()
        {
            PlayerId = playerId,
            Goals = goals,
            Appearances = appearances,
            CardPoints = yellowCards * YellowCardPoints + redCards * RedCardPoints,
            BornOn = bornOn
        };
}

/// <summary>
/// The order of a top scorers table, in one place, and the prizes that go with it.
///
/// The chain is: most goals, then fewest games, then fewest cards by weight, then oldest. It
/// is the same chain for a club's own list of scorers, for a division's artilharia and for the
/// cup's, because a striker who is level on goals with another is level with him for the
/// same reasons in all three, and a table that settles it one way in one place and another
/// way in the next is a table whose prize is a coin toss.
///
/// **A tie that survives the whole chain is a tie.** The lines level on all four take the same
/// position, both are paid, and the prize of the place below is not awarded. There is no fifth
/// rule to break a tie nobody can break, and a rule invented to break one — a coin, a name, the
/// order the rows came out of the database — would be a rule that pays two equally good
/// strikers different amounts of money for saying so.
///
/// This lives in the domain for the same reason <see cref="StandingTable"/> does: a top scorers
/// table sorted in two places, with two different chains, is a table that shows one order to
/// the manager and another to the money.
/// </summary>
public static class TopScorerTable
{
    /// <summary>
    /// Ranks the lines, stamps each one's position, and says which prize each is paid.
    /// </summary>
    /// <remarks>
    /// Lines level on the whole chain come back in the order they went in, so a caller that
    /// wants a level pair printed alphabetically asks for that itself and the domain does not
    /// have to know what a name means. What decides a prize is the chain and nothing else.
    /// </remarks>
    /// <param name="lines">Every player who scored in the competition.</param>
    /// <param name="prizePlaces">How many prizes are being handed out, first place included.</param>
    public static IReadOnlyList<ScorerStanding> Rank(
        IReadOnlyCollection<ScorerStanding> lines,
        int prizePlaces = PrizeRules.TopScorerPlaces)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var ordered = lines
            .OrderByDescending(line => line.Goals)
            .ThenBy(line => line.Appearances)
            .ThenBy(line => line.CardPoints)
            .ThenBy(line => line.BornOn ?? DateOnly.MaxValue)
            .ToList();

        var nextSlot = 1;
        var index = 0;

        while (index < ordered.Count)
        {
            var end = index + 1;
            while (end < ordered.Count && LevelOnEverything(ordered[index], ordered[end])) end++;

            var group = ordered.GetRange(index, end - index);

            foreach (var line in group)
            {
                line.Position = index + 1;
                line.TiedWith = group.Count - 1;
            }

            // The place a group of level men shares is the highest place any of them holds, and
            // the group takes that place's prize whole: two men tied for second are both paid the
            // second prize, and there is no third prize left for anybody.
            if (index + 1 <= prizePlaces && nextSlot <= prizePlaces)
            {
                foreach (var line in group)
                {
                    line.PrizeSlot = nextSlot;
                }
            }

            nextSlot += group.Count;
            index = end;
        }

        return ordered;
    }

    /// <summary>
    /// Whether two lines are level on everything the order looks at. A player with no birth
    /// date is level with another who has none, rather than being ordered as a year younger
    /// than the rest of the table.
    /// </summary>
    private static bool LevelOnEverything(ScorerStanding left, ScorerStanding right) =>
        left.Goals == right.Goals
        && left.Appearances == right.Appearances
        && left.CardPoints == right.CardPoints
        && left.BornOn == right.BornOn;
}
