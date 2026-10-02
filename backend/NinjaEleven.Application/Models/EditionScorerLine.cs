using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One player's goals in one edition of one competition, as the match lines add them up.
/// </summary>
/// <remarks>
/// This is <see cref="ClubScorerLine"/> with the edition carried on it, and the edition is the
/// whole point of the type: a club's history asks "did one of my men top this season's chart"
/// once for every competition the club was in, which is four divisions' worth of editions plus
/// the cup, and answering that one edition at a time is a question per row. Read them as a
/// set and the answer is worked out over all of them at once.
/// </remarks>
public class EditionScorerLine : ClubScorerLine
{
    /// <summary>The edition these goals were scored in.</summary>
    public required Guid CompetitionSeasonId { get; init; }
}

/// <summary>
/// The division a club was in, for one season of its career.
/// </summary>
/// <remarks>
/// There is no record anywhere of a club having been promoted or relegated: the world rebuilds
/// the pyramid from the final tables and the only trace a move leaves is that the club's
/// division this season is not the division it was in last season. So this row is what a move
/// is *derived* from — the same reason a club's page reads two of them side by side rather than
/// asking a movements table that does not exist.
/// </remarks>
public class ClubDivisionSeason
{
    public required Guid SeasonId { get; init; }

    /// <summary>The edition of the championship the club was enrolled in that season.</summary>
    public required Guid CompetitionSeasonId { get; init; }

    public required Guid DivisionId { get; init; }

    /// <summary>1 is the top of the pyramid. A higher number is a lower division.</summary>
    public required int Tier { get; init; }
}