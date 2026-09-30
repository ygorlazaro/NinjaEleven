namespace NinjaEleven.Domain.Enums;

/// <summary>
/// One of the eight things about a player the world keeps on him.
///
/// <para>
/// It exists because "the attribute" was being named in eight different ways across the
/// codebase: a private record struct in the match engine for the engine's own five, a
/// positional index in a tuple in the development code, a string on a training request, and
/// a switch in a mapper. None of them agreed with each other, and a rule that grew or
/// declined an attribute had to be written once per naming — which is a rule that was three
/// rules, and three rules about the same man drift apart within a season.
/// </para>
///
/// <para>
/// It is a closed set on purpose. A player has exactly these eight, seven of them about how
/// well he plays and one of them about how much football is left in him, and anything a
/// future change wants to add has to be added here rather than smuggled in beside it.
/// </para>
/// </summary>
public enum PlayerAttribute
{
    Speed,
    Accuracy,
    Dribbling,
    Heading,
    Strength,
    GoalkeeperPower,
    Reflexes,

    /// <summary>
    /// The only one of the eight that is not about how well he plays. It is here rather
    /// than apart because it grows, peaks and declines on exactly the same clock as the
    /// other seven, and a rule that treated the tank as a different kind of thing would be
    /// the first place the two drifted apart.
    /// </summary>
    Stamina
}
