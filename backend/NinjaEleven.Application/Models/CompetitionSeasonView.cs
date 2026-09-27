using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One edition of a competition, with the two things a service always has to know about it
/// and can never work out from the row on its own: what kind of competition it is, and which
/// tier it is the table of.
///
/// It exists because a service is handed repository interfaces and not a database context, so
/// joining the competition and the division is something the repository has to do on the
/// service's behalf. Returning a row per edition rather than a bare entity is also what lets
/// a season's competitions be listed as the calendar needs them, ordered by tier, with the
/// cup last.
/// </summary>
public class CompetitionSeasonView
{
    public required Guid Id { get; init; }
    public required Guid CompetitionId { get; init; }
    public required Guid SeasonId { get; init; }
    public Guid? DivisionId { get; init; }

    /// <summary>1 is the top of the pyramid. Null for a cup and a Supercup.</summary>
    public int? Tier { get; init; }

    public required string CompetitionName { get; init; }

    public required CompetitionType Type { get; init; }

    /// <summary>"1ª Divisão", or the cup's own name when it is not a division.</summary>
    public string Name => Tier is { } tier ? CompetitionRules.DivisionName(tier) : CompetitionName;

    public bool IsDivision => DivisionId is not null;
}
