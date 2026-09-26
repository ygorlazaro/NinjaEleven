namespace FootballManager.Domain.Enums;

/// <summary>
/// Lifecycle of a season. Only the backend ever changes this state.
/// </summary>
public enum SeasonStatus
{
    NotStarted,
    InProgress,
    Finished
}
