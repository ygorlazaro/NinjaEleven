namespace NinjaEleven.Domain.Enums;

/// <summary>
/// Player position. GK = goalkeeper, DEF = defender, MID = midfielder, ATT = attacker.
/// Any combination of DEF/MID/ATT is permitted; only exactly one effective GK is required.
/// </summary>
public enum Position
{
    GK,
    DEF,
    MID,
    ATT
}