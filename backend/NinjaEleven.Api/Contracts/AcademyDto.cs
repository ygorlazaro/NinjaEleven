namespace NinjaEleven.Api.Contracts;

/// <summary>
/// An academy player as shown on the Base screen.
/// </summary>
public class AcademyPlayerDto
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
    public bool IsAvailable { get; init; }
    public string Injury { get; init; } = "None";
    public int InjuryMatchesRemaining { get; init; }

    /// <summary>
    /// How many matches of his club the suspension keeps him out of, beside the injury's own
    /// counter. <see cref="IsAvailable"/> says he cannot play; these two say which of the two
    /// it is.
    /// </summary>
    public int SuspensionMatches { get; init; }
    public bool Retiring { get; init; }

    /// <summary>
    /// The youth's face as the raw JSON of a faces.js <c>FaceConfig</c>, null when he has
    /// none. The same string the profile carries, for the same reason: a face belongs to the
    /// man rather than to the screen that shows him, so a youth drawn in the base is the same
    /// face he has after he is promoted.
    /// </summary>
    public string? Face { get; init; }
}

/// <summary>
/// Result of promoting an academy player to the first team.
/// </summary>
public class PromoteAcademyResponseDto
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
/// Result of listing or unlisting a player on the transfer market.
/// </summary>
public class TransferListResultDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public bool OnTransferList { get; init; }
}
