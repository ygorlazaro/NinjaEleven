namespace NinjaEleven.Domain.Enums;

/// <summary>
/// What kind of competition a competition is, because the rules that follow from it are not
/// the same: a league is a table, a cup is a bracket, and a Supercup is one match between the
/// two teams that won the other two. Adding a kind here means saying what a table is for it,
/// not teaching the engine about it.
/// </summary>
public enum CompetitionType
{
    /// <summary>A division: everybody plays everybody, and the table decides.</summary>
    League,

    /// <summary>A knockout: two legs per tie, the aggregate decides, penalties break a tie.</summary>
    Cup,

    /// <summary>One match, between the champion of the league and the champion of the cup.</summary>
    SuperCup
}
