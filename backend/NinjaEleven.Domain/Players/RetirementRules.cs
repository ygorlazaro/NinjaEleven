namespace NinjaEleven.Domain.Players;

/// <summary>
/// The rule that decides who stops playing, and when he says so.
///
/// A man in this band announces at the opening of the season that this is his last one, to his
/// club, and the season's last day is the day he stops. There is nothing to ask and nobody to
/// ask it of: the club he spent his career at is told the same week as every other club is
/// told about its own, and the manager's eleven is one season shorter because a rule said so
/// rather than because a man was asked to press something.
///
/// Two things follow from the announcement being a rule and not a question. It cannot be
/// refused by the man it concerns — a player of thirty-eight does not get to decide he is
/// young for another season, and a game where the oldest men on a roster get to keep playing
/// is a game whose retirements are a setting — and it cannot be made early or late, because
/// the season it is about is the season it is announced in.
/// </summary>
public static class RetirementRules
{
    /// <summary>
    /// The youngest age at which a man announces his last season. Below this a player is still
    /// somebody a club is building around, and the world stops asking.
    /// </summary>
    public const int MinRetirementAge = 37;

    /// <summary>
    /// The oldest age at which a man announces his last season.
    ///
    /// Past this the game stops announcing anything: a man of forty-three is finished by the
    /// arithmetic of the game rather than by an announcement, and the announcement is what a
    /// club is given to plan around, not something to keep sending a club that has already
    /// moved on.
    /// </summary>
    public const int MaxRetirementAge = 42;

    /// <summary>
    /// Whether a man of this age announces his retirement at the opening of the season.
    ///
    /// The answer is read off the age the world aged him to, so the man who turns thirty-seven
    /// this year is announced in this year and the same man was not announced in any year
    /// before it.
    /// </summary>
    public static bool CanRetireAt(int age) => age >= MinRetirementAge && age <= MaxRetirementAge;
}
