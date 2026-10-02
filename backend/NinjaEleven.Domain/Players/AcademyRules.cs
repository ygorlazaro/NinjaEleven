namespace NinjaEleven.Domain.Players;

/// <summary>
/// The rules for the youth academy: how many young players a club has, how old they are,
/// and how they develop while in the academy.
///
/// Youth players are generated once per season, before the Supercup, and assigned to
/// clubs rather than released as free agents. They cannot be bought by other clubs until
/// they are promoted to the first team. The manager may promote any academy player at any
/// time — even an injured one — and the academy evolves its players each round, giving
/// a tenth of them an attribute improvement.
/// </summary>
public static class AcademyRules
{
    /// <summary>The oldest age a youth player may be when generated.</summary>
    public const int MaxAcademyAge = 21;

    /// <summary>The youngest age a youth player may be when generated.</summary>
    public const int MinAcademyAge = 16;

    /// <summary>The maximum number of academy players a single club may have.</summary>
    public const int MaxAcademyPlayersPerClub = 11;

    /// <summary>Range of goalkeepers in the youth intake (0-1).</summary>
    public static readonly (int Min, int Max) GoalkeeperRange = (0, 1);

    /// <summary>Range of defenders in the youth intake (0-4).</summary>
    public static readonly (int Min, int Max) DefenderRange = (0, 4);

    /// <summary>Range of midfielders in the youth intake (0-6).</summary>
    public static readonly (int Min, int Max) MidfielderRange = (0, 6);

    /// <summary>Range of attackers in the youth intake (0-5).</summary>
    public static readonly (int Min, int Max) AttackerRange = (0, 5);

    /// <summary>
    /// The share of the youth intake that is goalkeepers, matching the proportion a
    /// regular squad is built with.
    /// </summary>
    public const double GoalkeeperShare = 0.15;

    /// <summary>
    /// The fraction of academy players who receive an attribute improvement each round.
    /// Ten percent of the academy evolves — a slow but steady development that keeps the
    /// manager checking back.
    /// </summary>
    public const double EvolutionRate = 0.10;

    /// <summary>
    /// The maximum amount a single attribute may grow in one evolution tick.
    /// </summary>
    public const int MaxEvolutionPerAttribute = 2;

    /// <summary>
    /// Whether a player is within the age range for academy generation.
    /// </summary>
    public static bool IsValidAcademyAge(int age) =>
        age >= MinAcademyAge && age <= MaxAcademyAge;
}
