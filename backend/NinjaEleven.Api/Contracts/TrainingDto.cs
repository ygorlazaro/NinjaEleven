using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A request for one session on one attribute.
///
/// <para>
/// The attribute is the enum itself rather than a string, so that a name the game does not
/// have is refused by the same machinery that refuses a malformed id: the request never
/// reaches the service, and the answer is the <c>ValidationFailed</c> contract every other bad
/// request in the game answers with. A string parsed by hand would have produced a second
/// error shape for a second kind of bad input, and a client would have had to know which of
/// the two it was looking at.
/// </para>
/// </summary>
public class TrainPlayerRequest
{
    /// <summary>Which of the eight to work on.</summary>
    public PlayerAttribute Attribute { get; init; }

    /// <summary>
    /// The season to spend the energy in, or the current one. A manager trains the man he is
    /// looking at rather than a man in a season he has to name, so it is optional.
    /// </summary>
    public Guid? SeasonId { get; init; }
}

/// <summary>
/// A request to work a whole selection of men in one press.
/// </summary>
/// <remarks>
/// The selection is a list of pairs rather than a player id and a single attribute because the
/// manager's decision is one decision about several men, and a screen that made him press once
/// per man would be asking him to change his mind eleven times. A man can appear once here, and
/// the second time he would be refused by the day's own allowance rather than by a rule invented
/// for this endpoint.
/// </remarks>
public class TrainSelectionRequest
{
    /// <summary>Who to work and what to work on, in the order the manager picked them.</summary>
    public IReadOnlyList<TrainingSelectionItem> Selection { get; init; } = [];

    /// <summary>The season to spend the energy in, or the current one.</summary>
    public Guid? SeasonId { get; init; }
}

/// <summary>One man on a manager's sheet: this one, this attribute.</summary>
public class TrainingSelectionItem
{
    public Guid PlayerId { get; init; }
    public PlayerAttribute Attribute { get; init; }
}

/// <summary>What one session did: the cost, what is left, and the attribute either side.</summary>
public class TrainingResultDto
{
    public Guid PlayerId { get; init; }
    public Guid SeasonId { get; init; }
    public PlayerAttribute Attribute { get; init; }
    public int EnergySpent { get; init; }
    public int EnergyLeft { get; init; }
    public int AttributeBefore { get; init; }
    public int AttributeAfter { get; init; }

    /// <summary>What the session cost the club, being half the man's season wage.</summary>
    public decimal Fee { get; init; }
}

/// <summary>
/// What became of one man in a selection: worked, or refused with the reason.
/// </summary>
/// <remarks>
/// Every man comes back. A selection of twenty-three that answered with the nineteen that
/// worked would leave a manager unable to tell whether the other four were skipped, refused or
/// lost, and a squad that is quietly four men short of the one on the screen is a squad nobody
/// is managing.
/// </remarks>
public class TrainingOutcomeDto
{
    public Guid PlayerId { get; init; }
    public PlayerAttribute Attribute { get; init; }
    public bool Worked { get; init; }
    public TrainingResultDto? Result { get; init; }

    /// <summary>The rule that refused him, when it did — the same code the single route raises.</summary>
    public string? RefusalCode { get; init; }

    /// <summary>The sentence the domain wrote, in the manager's own language.</summary>
    public string? Refusal { get; init; }
}
