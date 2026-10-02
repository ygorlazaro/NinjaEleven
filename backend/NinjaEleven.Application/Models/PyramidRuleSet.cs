namespace NinjaEleven.Application.Models;

/// <summary>
/// The pyramid's rules, in a shape a screen can carry.
///
/// It is a reading of <c>PyramidRules</c> in the same way <see cref="DivisionPurse"/> is a
/// reading of <c>PrizeRules</c>: the bands and the chain are the domain's, and this only says
/// the same facts in the flat rows a DTO can be built from. The words travel with the numbers
/// because a screen that printed its own version of either is a screen with a second copy of
/// the season's rules, and the second copy is the one nobody updates.
/// </summary>
/// <param name="DivisionCount">How many divisions the pyramid has, tier 1 at the top.</param>
/// <param name="ClubsPerDivision">Clubs in every division's table.</param>
/// <param name="TieBreakers">The order a table is settled in, first criterion first.</param>
/// <param name="Divisions">Every division, from the top, with the bands its table ends in.</param>
public record PyramidRuleSet(
    int DivisionCount,
    int ClubsPerDivision,
    IReadOnlyList<StandingCriterionRule> TieBreakers,
    IReadOnlyList<DivisionRuleLine> Divisions);

/// <summary>
/// One step of the order a table is settled in.
/// </summary>
/// <param name="Order">Which place in the chain this is, counted from one.</param>
/// <param name="WholeTable">
/// Whether the criterion is asked of every club in the division, rather than only of the clubs
/// the criteria before it left level. A screen that drew the two ends as one list would be
/// telling a manager that a head-to-head decides a table it has nothing to do with.
/// </param>
/// <param name="Label">The criterion's name, in the game's own words.</param>
/// <param name="Detail">What it counts, and why it sits where it sits.</param>
public record StandingCriterionRule(int Order, bool WholeTable, string Label, string Detail);

/// <summary>One division's rules, as a screen reads them.</summary>
/// <param name="Tier">Which division, counted from one at the top.</param>
/// <param name="Name">The division's own name.</param>
/// <param name="Clubs">How many clubs share its table.</param>
/// <param name="Purse">What the whole table is paid out of.</param>
/// <param name="Bands">The bands, in the order a manager reads them down the table.</param>
public record DivisionRuleLine(
    int Tier,
    string Name,
    int Clubs,
    decimal Purse,
    IReadOnlyList<DivisionBandRule> Bands);

/// <summary>One band of a division's table, and what the season's end does to it.</summary>
/// <param name="Kind">Title, Promotion, Relegation or Safe, as the domain's own name for it.</param>
/// <param name="FromPosition">The first position of the band, counted from one.</param>
/// <param name="ToPosition">The last position of the band.</param>
/// <param name="ToTier">The division the clubs in the band start the next season in.</param>
/// <param name="ToDivisionName">That division's own name.</param>
/// <param name="Label">The band's name, as a manager would say it.</param>
/// <param name="Meaning">What finishing there is worth.</param>
public record DivisionBandRule(
    string Kind,
    int FromPosition,
    int ToPosition,
    int ToTier,
    string ToDivisionName,
    string Label,
    string Meaning);
