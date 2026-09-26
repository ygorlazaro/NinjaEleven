namespace NinjaEleven.Domain.Enums;

/// <summary>
/// Scheduling state of a fixture. The result itself lives in the Match aggregate.
/// </summary>
public enum FixtureStatus
{
    Scheduled,
    InProgress,
    Finished,
    Postponed
}
