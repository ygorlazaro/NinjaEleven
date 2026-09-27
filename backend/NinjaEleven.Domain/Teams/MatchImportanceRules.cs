using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Teams;

/// <summary>
/// How much a match matters to a crowd, worked out from the facts of the competition rather
/// than guessed at the call site.
///
/// This is a rule and not a default because the difference between a packed ground and an
/// empty one is a rule of the game, and a rule that is worked out by whichever caller
/// remembered to pass a bigger number is not a rule. Everything here is read off things a
/// crowd can see before kick-off: which competition it is, how late in the season it is, and
/// where the two clubs stand.
/// </summary>
public static class MatchImportanceRules
{
    /// <summary>
    /// How many matchdays from the end of the season a race stops being a race. A title
    /// decided four games out is settled; a title decided in the last two is not.
    /// </summary>
    public const int LivelyMatchdays = 3;

    /// <summary>
    /// How many clubs at the top of the division are in the title race. Four, because the
    /// top four is the usual way a league decides to describe it, and a bottom club eleven
    /// points clear is not in a title race however late the season is.
    /// </summary>
    public const int TitleRacePlaces = 4;

    /// <summary>How many clubs at the bottom of the division are in the relegation fight.</summary>
    public const int RelegationRacePlaces = 4;

    /// <summary>
    /// What the competition decides by itself, before anybody's position is looked at.
    ///
    /// A cup tie is a tie: win and you are through, lose and you are out, and a ground knows
    /// that from the moment the draw is made. A Supercup is a single match, so it is the one
    /// thing in the game that literally cannot be played again.
    /// </summary>
    public static MatchImportance ByCompetition(CompetitionType type) => type switch
    {
        CompetitionType.Cup => MatchImportance.VeryRelevant,
        CompetitionType.SuperCup => MatchImportance.Decisive,
        _ => MatchImportance.Normal
    };

    /// <summary>
    /// How much a league match matters, given where the two clubs are and how much season is
    /// left.
    /// </summary>
    /// <param name="homePosition">Where the home club stands, counted from one.</param>
    /// <param name="awayPosition">Where the away club stands, counted from one.</param>
    /// <param name="clubsInDivision">How many clubs are in the division.</param>
    /// <param name="matchdaysRemaining">How many matchdays of the season are still to be played after this one.</param>
    public static MatchImportance ForLeagueMatch(
        int homePosition,
        int awayPosition,
        int clubsInDivision,
        int matchdaysRemaining)
    {
        if (clubsInDivision < 2) return MatchImportance.Normal;
        if (matchdaysRemaining < 0) matchdaysRemaining = 0;

        var lively = matchdaysRemaining <= LivelyMatchdays;
        var inTitleRace = InRace(homePosition, TitleRacePlaces, clubsInDivision)
            || InRace(awayPosition, TitleRacePlaces, clubsInDivision);
        var inRelegationFight = InRelegationFight(homePosition, clubsInDivision)
            || InRelegationFight(awayPosition, clubsInDivision);

        // The last matchday of a season is decided all at once, so a club that can still go up
        // or still go down is playing for something however far the season has run.
        if (matchdaysRemaining == 0 && (inTitleRace || inRelegationFight))
        {
            return MatchImportance.Decisive;
        }

        if (lively && (inTitleRace || inRelegationFight)) return MatchImportance.VeryRelevant;

        return MatchImportance.Normal;
    }

    /// <summary>
    /// Whether a place is inside the race at the top. A division with fewer clubs than the
    /// race is wide has every club in it, and a division with more clubs than the race is
    /// wide has only the ones inside it.
    /// </summary>
    private static bool InRace(int position, int places, int clubsInDivision) =>
        position >= 1 && position <= Math.Min(places, clubsInDivision);

    private static bool InRelegationFight(int position, int clubsInDivision) =>
        position >= Math.Max(1, clubsInDivision - RelegationRacePlaces + 1) && position <= clubsInDivision;
}
