namespace NinjaEleven.Application.Models;

/// <summary>
/// One line of a division's final table, as a message reads it.
/// </summary>
public class SeasonSummaryLine
{
    public required Guid TeamId { get; init; }
    public required string ClubName { get; init; }
    public required int Position { get; init; }
    public required int Points { get; init; }
    public required int Played { get; init; }
    public required int Wins { get; init; }
    public required int Draws { get; init; }
    public required int Losses { get; init; }
    public required int GoalsFor { get; init; }
    public required int GoalsAgainst { get; init; }

    /// <summary>Where this club goes: up, down, or nowhere.</summary>
    public SeasonMovementKind Movement { get; init; } = SeasonMovementKind.Stays;
}

/// <summary>Which way a club moves between the divisions at the end of a season.</summary>
public enum SeasonMovementKind
{
    Stays,
    Promoted,
    Relegated,
}

/// <summary>
/// One division's table and what became of it, as a message reads it.
/// </summary>
public class SeasonSummaryDivision
{
    public required int Tier { get; init; }
    public required string DivisionName { get; init; }
    public required IReadOnlyList<SeasonSummaryLine> Lines { get; init; }
}

/// <summary>
/// The four divisions of a season that is over: every table, and who went up and who came
/// down.
///
/// <para>
/// It is one fact rather than four, and it is the same fact for every manager. A season ends
/// by moving sixteen clubs between four tables, and a manager who is not in the division that
/// lost a club still watched the country change shape: four messages saying so, each about
/// somebody else's table, would be a season that ended four times over and never all at once.
/// </para>
/// </summary>
public class SeasonSummaryFacts
{
    public required string SeasonName { get; init; }
    public required Guid SeasonId { get; init; }

    /// <summary>The four divisions, in the order they are read: first down to fourth.</summary>
    public required IReadOnlyList<SeasonSummaryDivision> Divisions { get; init; }

    /// <summary>
    /// What makes it once. A season is closed when its last window is played, and a window can
    /// be closed twice — by the run that played it and by a process that was down over the
    /// weekend — so the reference is the season rather than the close.
    /// </summary>
    public string Reference => $"season-summary:{SeasonId}";
}
