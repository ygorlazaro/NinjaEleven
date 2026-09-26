namespace NinjaEleven.Domain.Enums;

/// <summary>
/// Execution state of a match. The server owns the logical clock and only the
/// MatchEngine (driven by the Application layer) is allowed to move a match forward.
/// </summary>
public enum MatchStatus
{
    Scheduled,
    KickOff,
    InProgress,
    HalfTime,
    SecondHalf,
    Finished,
    Abandoned
}
