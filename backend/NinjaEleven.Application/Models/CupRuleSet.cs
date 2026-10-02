namespace NinjaEleven.Application.Models;

/// <summary>
/// The cup's rules, in a shape a screen can carry.
///
/// It is a reading of <c>CupRules</c> in the same way <see cref="PyramidRuleSet"/> is a reading of
/// <c>PyramidRules</c>: the shape of the competition is the domain's and this only says the same
/// facts in the flat rows a DTO can be built from. The sentences travel with the numbers because a
/// screen that printed its own version of any of them is a screen with a second copy of the cup,
/// and the second copy is the one nobody updates.
/// </summary>
/// <param name="Size">How many clubs the cup is drawn from.</param>
/// <param name="TieRounds">How many tie-rounds it is drawn over.</param>
/// <param name="LegsPerTie">Matches in a cup tie.</param>
/// <param name="AllowsExtraTime">
/// Whether a level tie can be played out in extra time. It is sent rather than omitted because its
/// absence is the rule: a cup tie goes to penalties.
/// </param>
/// <param name="AggregateRule">How the two legs of a tie are added up, in the game's own words.</param>
/// <param name="LevelTieRule">What decides a tie that is level on the aggregate.</param>
/// <param name="WinnerTakesRule">What the cup's winner goes on to play.</param>
/// <param name="Rounds">Every tie-round, with the clubs in it and the days it is played over.</param>
public record CupRuleSet(
    int Size,
    int TieRounds,
    int LegsPerTie,
    bool AllowsExtraTime,
    string AggregateRule,
    string LevelTieRule,
    string WinnerTakesRule,
    IReadOnlyList<CupRoundRuleLine> Rounds);

/// <summary>One tie-round of the cup, as a screen reads it.</summary>
/// <param name="TieRound">Which tie-round this is, counted from one at the 32-avos.</param>
/// <param name="Name">The round's name, in the game's own words.</param>
/// <param name="ClubsIn">How many clubs enter the round.</param>
/// <param name="Ties">How many ties the round is made of.</param>
/// <param name="FirstLegDay">The day of the season the first legs are played on.</param>
/// <param name="SecondLegDay">The day of the season the returns are played on.</param>
public record CupRoundRuleLine(
    int TieRound,
    string Name,
    int ClubsIn,
    int Ties,
    int FirstLegDay,
    int SecondLegDay);
