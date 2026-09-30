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

    /// <summary>What the session cost the club, being a share of the man's season wage.</summary>
    public decimal Fee { get; init; }

    /// <summary>How many sessions the club has left on the day after this one.</summary>
    public int SessionsLeft { get; init; }
}
