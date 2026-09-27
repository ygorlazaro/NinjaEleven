namespace NinjaEleven.Application.Models;

/// <summary>
/// How long a scorers table is allowed to be.
///
/// The cap is generous on purpose. A club's list of scorers is a page about its history and
/// not a leaderboard for this season: a club that has played ten seasons has thirty men who
/// have scored for it, and a screen that answered with fifteen of them is a screen that
/// decides which men a club is allowed to remember. The cap exists to stop a caller asking
/// for the whole table of every club in the country, not to keep the page short.
/// </summary>
public static class ScorerRules
{
    public const int DefaultScorers = 30;
    public const int MaxScorers = 500;

    public static int Clamp(int requested) =>
        requested < 1 ? DefaultScorers : Math.Min(requested, MaxScorers);
}
