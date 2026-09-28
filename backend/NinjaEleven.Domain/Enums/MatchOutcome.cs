namespace NinjaEleven.Domain.Enums;

/// <summary>
/// How a finished match went for one of the two clubs in it.
///
/// It is a fact about the scoreline and about which side was which, so it is worked out once
/// from the result and then read by everything that wants to say "V", "E" or "D" about a game:
/// a club's recent form, a run of results, a table's column of the last five. A screen that
/// compared two scores itself would be reaching the same answer by a second route, and the two
/// routes are the kind that agree right up until a manager looks at the one matchday they do not.
/// </summary>
public enum MatchOutcome
{
    Win,
    Draw,
    Loss
}
