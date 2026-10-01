namespace NinjaEleven.Domain.Teams;

/// <summary>
/// What number a player wears, and who may have it.
///
/// <para>
/// A shirt number is not on the player. It belongs to the contract: a striker who is sold
/// leaves his number behind with the club he leaves, and the man who arrives in his place
/// wears whatever is free on the day he signs. That is why this rule lives beside the
/// membership rather than on <see cref="Players.Player"/>, and why the identity the player
/// keeps for the whole world — the face, the age, the potential — does not include it.
/// </para>
///
/// <para>
/// <b>The range is football's.</b> One to ninety-nine, because that is the range the sport
/// settled on and a squad numbered one to thirty is a squad that has run out of numbers long
/// before it has run out of men. Zero is not a number a player wears: it is the value a
/// membership carries before anybody has dealt it one, and a club whose third-choice
/// goalkeeper turned out in a zero would be a club that forgot a step rather than one that
/// made a decision.
/// </para>
///
/// <para>
/// <b>Two men in one club may not wear the same number, and two men in two clubs may.</b>
/// The uniqueness is scoped to the club on purpose. A number is a name inside one dressing
/// room, the same way a squad number is: the goalkeeper of one club and the goalkeeper of
/// another are both the number one of their own teams, and a rule that made the second
/// signing illegal would be a rule about the world rather than about a shirt.
/// </para>
/// </summary>
public static class ShirtNumberRules
{
    /// <summary>The smallest number a player wears.</summary>
    public const int Lowest = 1;

    /// <summary>The largest number a player wears.</summary>
    public const int Highest = 99;

    /// <summary>
    /// The lowest number an outfield player wears, which is two.
    ///
    /// <para>
    /// It is a rule of its own rather than a consequence of the keepers being dealt first,
    /// because a keeper who leaves takes one with him: a club whose number one walks out
    /// must not hand one to a striker on the next signing, or the shirt that says "keeper"
    /// ends up on a man who has never kept goal. A number one belongs to a keeper whenever
    /// there is a keeper to give it to, and it waits for one when there is not.
    /// </para>
    /// </summary>
    public const int LowestOutfield = 2;

    /// <summary>
    /// The numbers a club's first three goalkeepers are given, in the order they join.
    ///
    /// They are not the lowest free numbers, which is the whole point of spelling them out:
    /// every outfield player on a full squad takes 2 and 3 and 4, and a keeper dealt the
    /// lowest free number would be given one of those and then spend the season as a
    /// goalkeeper wearing a striker's shirt. The three are far enough apart that a fourth
    /// keeper has to take what is left, which is what happens in the real game too.
    /// </summary>
    public static readonly int[] GoalkeeperNumbers = [1, 12, 23];

    /// <summary>Whether a number is one a player may wear.</summary>
    public static bool IsValid(int number) => number >= Lowest && number <= Highest;

    /// <summary>
    /// The lowest number nobody on this list is wearing, counted from <paramref name="from"/>.
    ///
    /// It is what a signing is given and what a squad is built with, and it is "lowest free"
    /// rather than "next in sequence" so that a club which has deliberately moved a player out
    /// of the way does not hand that same number to the next man through the door.
    /// </summary>
    public static int LowestFree(IEnumerable<int> taken, int from = Lowest)
    {
        var worn = new HashSet<int>(taken);
        var first = Math.Max(Lowest, from);

        for (var number = first; number <= Highest; number++)
        {
            if (!worn.Contains(number))
            {
                return number;
            }
        }

        throw new InvalidOperationException(
            $"Every shirt number from {first} to {Highest} is taken; a squad of more than {Highest} men cannot be numbered.");
    }

    /// <summary>
    /// The number a goalkeeper joining this club is given.
    ///
    /// The three keeper numbers first, and the lowest free number after them, so a club with
    /// one keeper has a one and a club with four has a one, a twelve, a twenty-three and
    /// whatever is left over.
    /// </summary>
    public static int ForGoalkeeper(IEnumerable<int> taken)
    {
        var worn = new HashSet<int>(taken);

        var reserved = GoalkeeperNumbers.FirstOrDefault(number => !worn.Contains(number));
        return reserved != 0 ? reserved : LowestFree(worn);
    }

    /// <summary>
    /// The number a player joining this club is given, asked of the position he plays.
    ///
    /// A keeper is offered a keeper's number and an outfield player is not, and the two rules
    /// are the same rule seen from either side of it: the shirt says the job, so the number
    /// that goes on it has to be one the job wears.
    /// </summary>
    public static int For(IEnumerable<int> taken, bool isGoalkeeper) =>
        isGoalkeeper ? ForGoalkeeper(taken) : LowestFree(taken, LowestOutfield);

    /// <summary>
    /// The numbers a whole squad is dealt, in the order the players are given.
    ///
    /// Order is the only argument: the caller hands over the squad in the order it wants them
    /// numbered and the keepers are recognised by what they are, so the result is the same
    /// squad numbered the same way whichever order the club happens to list its players in.
    /// That is what makes a seeded world and a rebuilt world agree.
    /// </summary>
    public static IReadOnlyList<int> Deal(IEnumerable<bool> goalkeepersInOrder)
    {
        var worn = new HashSet<int>();
        var dealt = new List<int>();

        foreach (var isGoalkeeper in goalkeepersInOrder)
        {
            var number = For(worn, isGoalkeeper);
            worn.Add(number);
            dealt.Add(number);
        }

        return dealt;
    }

    /// <summary>The word a number outside the range is refused by.</summary>
    public const string OutOfRangeCode = "ShirtNumberOutOfRange";

    /// <summary>The word a clash is refused by, so a manager is told it was taken rather than that it failed.</summary>
    public const string AlreadyTakenCode = "ShirtNumberAlreadyTaken";
}
