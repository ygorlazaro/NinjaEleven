using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// One attribute on a training sheet, and what a session on it would cost. The cost is null
/// when a session is impossible, which is what lets a screen offer nothing rather than
/// offering a button the backend will refuse.
/// </summary>
public class TrainingAttributeQuoteDto
{
    public PlayerAttribute Attribute { get; init; }
    public int Value { get; init; }
    public int? Cost { get; init; }
}

/// <summary>One player's training sheet: who he is, what he has, and the price of each of the eight.</summary>
public class TrainingQuoteDto
{
    public Guid PlayerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public int Age { get; init; }
    public int Potential { get; init; }
    public int Stamina { get; init; }
    public int Energy { get; init; }
    public bool IsAvailable { get; init; }
    public string Injury { get; init; } = string.Empty;

    /// <summary>Whether this player is an academy player, who trains for free and ages through the intake.</summary>
    public bool IsAcademyPlayer { get; init; }

    /// <summary>What one session on this man costs the club, being half his season wage.</summary>
    public decimal SessionFee { get; init; }

    public List<TrainingAttributeQuoteDto> Attributes { get; init; } = new();
}

/// <summary>The club's whole sheet, and what the day is worth to each man on it.</summary>
public class SquadTrainingQuotesDto
{
    public Guid TeamId { get; init; }
    public Guid SeasonId { get; init; }
    public int SquadEnergy { get; init; }

    /// <summary>The calendar day the sheet is for.</summary>
    public DateOnly Day { get; init; }

    /// <summary>Whether the club has a fixture on that day.</summary>
    public bool PlaysToday { get; init; }

    public List<TrainingQuoteDto> Players { get; init; } = new();
}
