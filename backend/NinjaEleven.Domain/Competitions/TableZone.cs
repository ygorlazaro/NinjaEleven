namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// The band a club sits in across a divisions table: the top four is the promotion
/// race, the bottom four the relegation battle, and the rest are safe.
/// </summary>
public enum TableZone
{
    /// <summary>Not a divisions table (a cup or Supercup), so the bands do not apply.</summary>
    None = 0,

    /// <summary>The middle of the table: clear of both promotion and relegation.</summary>
    Safe = 1,

    /// <summary>The top band: four from the top, and the promotion race.</summary>
    Promotion = 2,

    /// <summary>The bottom band: four from the bottom, and the relegation battle.</summary>
    Relegation = 3
}
