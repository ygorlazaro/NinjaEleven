namespace FootballManager.Domain.Enums;

/// <summary>
/// Kind of competition. The domain is intentionally not tied to "League" so
/// state leagues, cups and international tournaments can be added later.
/// </summary>
public enum CompetitionType
{
    League,
    StateLeague,
    Cup,
    International
}
