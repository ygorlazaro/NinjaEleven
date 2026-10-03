namespace NinjaEleven.Application.Models;

/// <summary>
/// One club's turnstiles over a season: what came through them and what wanted to.
/// </summary>
/// <remarks>
/// <see cref="Demand"/> is null for matches played before the number was kept, and that is a
/// different fact from a demand of zero. A club whose history reaches back that far has a
/// measured gate and an unmeasured pressure, and a screen that drew the second as the first
/// would be drawing a number nobody has.
/// </remarks>
public class HomeGateReading
{
    public int HomeMatches { get; init; }

    public double AverageAttendance { get; init; }

    /// <summary>Null when none of the club's matches in the season recorded what it wanted.</summary>
    public double? AverageDemand { get; init; }

    public int BestGate { get; init; }
}
