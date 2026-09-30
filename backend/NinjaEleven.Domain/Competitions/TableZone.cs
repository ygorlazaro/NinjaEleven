namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// The band a club sits in across a division's table.
///
/// The bands are not the same in every division, because the pyramid is not symmetric: the
/// top division has a champion above it and nothing to promote into, and the bottom one has
/// nothing below it to be sent down to. So the zones are named for what they *are* — the title,
/// the race up and the fight to stay — rather than for a pair of numbers that only holds in the
/// middle of the pyramid.
/// </summary>
public enum TableZone
{
    /// <summary>Not a divisions table (a cup or Supercup), so the bands do not apply.</summary>
    None = 0,

    /// <summary>The middle of the table: clear of everything that decides a club's season.</summary>
    Safe = 1,

    /// <summary>The top band of a division that has a division above it: the promotion race.</summary>
    Promotion = 2,

    /// <summary>The bottom band of a division that has a division below it: the relegation battle.</summary>
    Relegation = 3,

    /// <summary>The champion of the top division: first place, and the only place that is a title.</summary>
    Champion = 4
}
