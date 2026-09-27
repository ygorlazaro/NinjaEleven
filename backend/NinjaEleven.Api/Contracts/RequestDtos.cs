using System.ComponentModel.DataAnnotations;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A season to be created.
///
/// There is no name in it, and that is the point. A season is identified by its number and
/// shown as a roman numeral, both derived from the same count, so there is nothing for a
/// client to disagree with and nothing to keep in step. A request that carried a name would
/// be a request that could ask for "Temporada IX" in a world that is on season two.
/// </summary>
public class CreateSeasonRequestDto
{
    /// <summary>The number to create, when the caller insists on one. Normally the next one.</summary>
    public int? Number { get; init; }

    [Required]
    public DateOnly? StartDate { get; init; }

    [Required]
    public DateOnly? EndDate { get; init; }
}

public class CreateCompetitionRequestDto
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [EnumDataType(typeof(CompetitionType))]
    public CompetitionType Type { get; init; } = CompetitionType.League;
}

public class SetupLeagueRequestDto
{
    public Guid CompetitionId { get; init; }

    public Guid SeasonId { get; init; }

    public IReadOnlyList<Guid> TeamIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
/// Optional seed for a new match, plus the eleven and bench chosen by the manager. Omitting the
/// eleven lets the backend pick the strongest available players, which is what the
/// opponent always does.
/// </summary>
public class StartMatchRequestDto
{
    public int? Seed { get; init; }

    public Guid? UserTeamId { get; init; }

    public IReadOnlyList<Guid> StarterIds { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<Guid> BenchIds { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// The shape the manager ordered his club to be built in, by code ("4231"). Omitted
    /// means the club plays the shape it is made of, which is what a manager who did not
    /// think about it gets.
    /// </summary>
    public string? TacticCode { get; init; }
}

/// <summary>
/// Replaces a player on the pitch with one from the bench.
/// </summary>
public class SubstitutionRequestDto
{
    public Guid PlayerOutId { get; init; }

    public Guid PlayerInId { get; init; }
}

/// <summary>
/// Chooses the player who takes a penalty the engine awarded.
/// </summary>
public class PenaltyTakerRequestDto
{
    public Guid PlayerId { get; init; }
}

/// <summary>
/// Names the order a club will take a shootout in, over the hub.
///
/// The order is a list rather than a single player because a shootout is not one kick: it is
/// five of them, in an order the manager picks, and the men are the ones the engine says may
/// take. The ids are the men themselves, in the order they will walk to the spot.
/// </summary>
public class NameShootoutOrderDto
{
    public Guid MatchId { get; init; }

    public Guid TeamId { get; init; }

    public List<Guid> TakerIds { get; init; } = new();
}

/// <summary>
/// Chooses the order a club takes a shootout in, over REST.
/// </summary>
public class ShootoutOrderRequestDto
{
    public List<Guid> TakerIds { get; init; } = new();
}

/// <summary>
/// Identifies the match a hub command applies to. Used by the commands that take
/// nothing else: pause, resume and the second half.
/// </summary>
public class MatchCommandDto
{
    public Guid MatchId { get; init; }
}

/// <summary>
/// Session preference for how often the server advances the simulation. It is never a
/// football rule and never lets a client move the logical clock.
/// </summary>
public class MatchSpeedDto
{
    public Guid MatchId { get; init; }

    public int Speed { get; init; }
}

/// <summary>
/// Replaces a player on the pitch with one from the bench, over the hub.
/// </summary>
public class MakeSubstitutionDto
{
    public Guid MatchId { get; init; }

    public Guid TeamId { get; init; }

    public Guid PlayerOutId { get; init; }

    public Guid PlayerInId { get; init; }
}

/// <summary>
/// Chooses the penalty taker, over the hub.
/// </summary>
public class SelectPenaltyTakerDto
{
    public Guid MatchId { get; init; }

    public Guid TeamId { get; init; }

    public Guid PlayerId { get; init; }
}
