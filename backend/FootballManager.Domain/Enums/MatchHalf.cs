namespace FootballManager.Domain.Enums;

/// <summary>
/// Which half of the match is currently being played. Stored as an enum instead
/// of a loose integer so the value is meaningful everywhere it is read.
/// </summary>
public enum MatchHalf
{
    First,
    Second,
    ExtraTime,
    PenaltyShootout
}
