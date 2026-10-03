using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

/// <summary>
/// An academy player as shown on the Base screen: his static identity, his current
/// attributes, his development room, and his availability for promotion.
/// </summary>
public class AcademyPlayer
{
    public Guid PlayerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public int Age { get; init; }
    public int OverallRating { get; init; }
    public double Stars { get; init; }

    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Stamina { get; init; }

    public int Potential { get; init; }
    public int DevelopmentRoom { get; init; }

    public int Energy { get; init; }

    /// <summary>
    /// Whether he can be named in an order today, and why not when he cannot.
    ///
    /// <para>
    /// The two counters travel together because <see cref="IsAvailable"/> alone cannot say
    /// which absence it is: a youth carrying a knock and a youth serving a ban are both simply
    /// "unavailable", and a screen that read the flag and guessed the cause was guessing.
    /// </para>
    /// </summary>
    public bool IsAvailable { get; init; }
    public string Injury { get; init; } = "None";
    public int InjuryMatchesRemaining { get; init; }
    public int SuspensionMatches { get; init; }
    public bool Retiring { get; init; }

    /// <summary>
    /// The youth's face as the raw JSON of a faces.js <c>FaceConfig</c>, null when he has
    /// none. It is carried out of the repository with the rest of the youth rather than
    /// fetched when a card is drawn, because twenty-odd separate face reads to paint one
    /// screen is a question asked per row — and a face that arrives late is a face that is
    /// painted late.
    /// </summary>
    public string? Face { get; init; }
}

/// <summary>
/// Result of promoting an academy player to the first team.
/// </summary>
public class PromoteAcademyResult
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public int? ShirtNumber { get; init; }
    public decimal Salary { get; init; }
    public int SeasonsLeft { get; init; }
}

/// <summary>
/// Result of an academy evolution round.
/// </summary>
public class AcademyEvolutionResult
{
    public int Evolved { get; init; }
    public int Total { get; init; }
}

/// <summary>
/// Result of listing or unlisting a player on the transfer market.
/// </summary>
public class TransferListResult
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public bool OnTransferList { get; init; }
}
