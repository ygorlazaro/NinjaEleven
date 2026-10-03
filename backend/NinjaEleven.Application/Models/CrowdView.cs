using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

/// <summary>
/// How much of a crowd the ground can hold, and how much of it the ground cannot.
/// </summary>
/// <remarks>
/// The two numbers are kept apart because either one alone is a lie. Attendance is people in
/// seats, so it sits at or below capacity and a full ground looks exactly like a ground that is
/// comfortably big enough. Demand has no ceiling and is the only one of the two that can say a
/// club is being turned away.
/// </remarks>
public class CrowdPressure
{
    public int Capacity { get; init; }

    /// <summary>How many people came, on a season average.</summary>
    public double AverageAttendance { get; init; }

    /// <summary>
    /// How many wanted to come, on the same average, with nothing capping it. Null when the
    /// club's matches in the season were played before this number was kept — a club with no
    /// measurement is not a club nobody wants to see.
    /// </summary>
    public double? AverageDemand { get; init; }

    /// <summary>Whether the club's matches actually recorded what the crowd wanted.</summary>
    public bool DemandIsMeasured { get; init; }

    /// <summary>How full the ground is. Bounded by one, because seats are seats.</summary>
    public double Occupancy => Capacity <= 0 ? 0 : AverageAttendance / Capacity;

    /// <summary>
    /// The share of the crowd the ground turns away, and null when the pressure has never been
    /// measured. A screen that printed zero here would be telling a manager a ground is exactly
    /// the right size on the strength of a number nobody took.
    /// </summary>
    public double? TurnedAwayShare =>
        AverageDemand is not { } demand || demand <= 0
            ? null
            : (demand - AverageAttendance) / demand;

    /// <summary>Whether anybody is being turned away at all.</summary>
    public bool Oversubscribed => AverageDemand is { } demand && demand > AverageAttendance;

    /// <summary>
    /// The next project up from this ground, and none when the ground is already as big as the
    /// catalogue builds.
    /// </summary>
    public StadiumProject? NextProject { get; init; }
}

/// <summary>A club's crowd, as a screen reads it.</summary>
public class CrowdView
{
    public Guid TeamId { get; init; }
    public int Supporters { get; init; }
    public int OpeningSupporters { get; init; }
    public int PeakSupporters { get; init; }
    public bool Grew { get; init; }
    public int Change { get; init; }

    /// <summary>
    /// Null when the club has not been given a crowd for the season in question, which is a
    /// different fact from a club whose crowd is zero.
    /// </summary>
    public Guid? SeasonId { get; init; }

    public string? SeasonName { get; init; }

    /// <summary>
    /// Null when the club has not played a home match this season. A club that has not opened
    /// its ground has no average crowd and a screen that printed zero would be printing an
    /// answer to a question nobody asked.
    /// </summary>
    public double? AverageAttendance { get; init; }

    public CrowdPressure? Pressure { get; init; }
}

/// <summary>Building work on a ground, and where it has got to.</summary>
public class StadiumWorkView
{
    public Guid ConstructionId { get; init; }
    public int Seats { get; init; }
    public decimal Cost { get; init; }
    public int Rounds { get; init; }
    public int StartedAfterRound { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}

/// <summary>A ground, and whatever is being done to it.</summary>
public class StadiumView
{
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public decimal TicketPrice { get; init; }

    /// <summary>Null when the ground is not being worked on, which is most grounds most of the year.</summary>
    public StadiumWorkView? Work { get; init; }
}
