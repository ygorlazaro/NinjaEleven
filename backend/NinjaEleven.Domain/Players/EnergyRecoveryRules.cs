using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Domain.Players;

/// <summary>
/// What happened to a player's body in one window of football, and therefore what he gets
/// back at the end of it.
/// </summary>
/// <param name="PlayedInMatch">He was on the pitch, so the match itself cost him.</param>
/// <param name="ClubPlayedInWindow">
/// His club had a match in this window. False means his club sat the window out entirely,
/// which is a different kind of rest from being on the bench.
/// </param>
public readonly record struct WindowEffort(bool PlayedInMatch, bool ClubPlayedInWindow)
{
    /// <summary>He was in the eleven, or came off the bench.</summary>
    public static WindowEffort Played(bool clubPlayed) => new(true, clubPlayed);

    /// <summary>He was in the squad and did not play.</summary>
    public static WindowEffort SatOut(bool clubPlayed) => new(false, clubPlayed);

    /// <summary>His club had no match in this window at all.</summary>
    public static WindowEffort NoMatch => new(false, false);
}

/// <summary>
/// What a window of football gives back to a player, in energy.
///
/// This is the rule that has to change when a season stops being one competition. There used
/// to be a single league, a round passed, and every player in the two squads either played
/// and got a little back or sat on the bench and got a lot back. Now a club can play in the
/// championship window and sit the cup window out, so "a round passed" is not a thing that
/// happens to anybody, and a player who played on Monday and whose club has no cup tie on
/// Tuesday has had two separate windows that deserve two separate answers.
///
/// The bands do not overlap, and that is the whole reason a squad is rotated:
///
///   played in the window .................. 3..7
///   did not play, the club did ............ 11..18
///   the club did not play at all .......... 18..26
///
/// The last band is a different rule rather than a bigger number: a club that does not play
/// at all in a window gives its men more back than a club that played and left them on the
/// bench, because a bench in a cold evening costs something and a day off does not. It
/// starts where the bench band ends so the two can never be confused for one another.
/// </summary>
public static class EnergyRecoveryRules
{
    public const int MinAfterPlaying = MatchRules.MinRecoveryAfterPlaying;
    public const int MaxAfterPlaying = MatchRules.MaxRecoveryAfterPlaying;

    public const int MinOnTheBench = MatchRules.MinRecoveryAfterResting;
    public const int MaxOnTheBench = MatchRules.MaxRecoveryAfterResting;

    /// <summary>A window his club did not play at all is worth more than a window he sat out.</summary>
    public const int MinFullRest = MatchRules.MaxRecoveryAfterResting;

    public const int MaxFullRest = 26;

    /// <summary>How much energy a window gives a player back.</summary>
    public static int Recovery(WindowEffort effort, Common.IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return effort switch
        {
            // A man who played has already been billed for the match inside it, and the bill
            // is the drain the engine took off him tick by tick. He is given the small band
            // here and the big one is never also given, or a squad would recover a season's
            // worth of rest in a fortnight.
            { PlayedInMatch: true } => random.Next(MinAfterPlaying, MaxAfterPlaying + 1),

            { ClubPlayedInWindow: true } => random.Next(MinOnTheBench, MaxOnTheBench + 1),

            _ => random.Next(MinFullRest, MaxFullRest + 1)
        };
    }
}
