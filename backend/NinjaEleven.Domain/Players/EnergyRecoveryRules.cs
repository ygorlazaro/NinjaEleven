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
    /// <remarks>
    /// Three answers, as before, and then two multipliers over the top of whichever one it is:
    ///
    ///   **the minutes.** A recovery band is what a full match earns, so a man who was on the
    ///   pitch for a fifth of it earns a fifth of it. Without this a substitute who came on
    ///   at the eighty-fifth is paid the same as a man who played ninety, and he is paid it
    ///   from a higher number, because the bench cost him nothing — so the player who did
    ///   the least work ends the day in the best shape, which is the exact opposite of what a
    ///   rotation is for. The band is the ceiling of the recovery, not the recovery itself.
    ///
    ///   **the age.** A window of rest is worth more to a young body than to an old one, and
    ///   the same is true of the match that cost it, which is <see cref="NinjaEleven.Domain.Matches.PlayerMetric.AgeCost"/>
    ///   said from the other side. It scales the bench band as well as the played one: a man
    ///   who sat out a match at thirty-four is not as rested as a man who sat it out at
    ///   twenty-one.
    ///
    /// A man who was never on the pitch is not scaled by the minutes — he has no minutes —
    /// and is scaled by the age only when the caller knows it. A whole window off is the one
    /// recovery in here that is not a consequence of the match, and it is left alone on
    /// purpose: reading an age for a man the engine holds no snapshot of would cost a query
    /// per player at the end of each of the thirty-four matches of a matchday, to move a
    /// number nobody is reading.
    /// </remarks>
    /// <param name="effort">What he did in the window.</param>
    /// <param name="minutesPlayed">How many minutes of the match he was on the pitch for.</param>
    /// <param name="age">His age, when the caller has it.</param>
    /// <param name="random">The window's own source, so a replayed match recovers the same way.</param>
    public static int Recovery(
        WindowEffort effort,
        int minutesPlayed,
        int? age,
        Common.IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var (min, max) = BandOf(effort);
        var recovery = random.Next(min, max + 1);

        if (effort.PlayedInMatch)
        {
            var share = Math.Clamp(
                (double)minutesPlayed / NinjaEleven.Domain.Matches.MatchRules.MinutesInAMatch,
                0.0,
                1.0);

            recovery = (int)Math.Round(
                recovery * share * (age is null ? 1.0 : NinjaEleven.Domain.Matches.PlayerMetric.AgeRecovery(age.Value)),
                MidpointRounding.AwayFromZero);

            // A cameo is not nothing. He ran out onto a cold pitch, he warmed up in front of
            // thirty thousand people, and he is a man who played football today.
            return Math.Max(NinjaEleven.Domain.Matches.MatchRules.MinRecoveryForACameo, recovery);
        }

        if (age is null)
        {
            return recovery;
        }

        return Math.Max(
            0,
            (int)Math.Round(
                recovery * NinjaEleven.Domain.Matches.PlayerMetric.AgeRecovery(age.Value),
                MidpointRounding.AwayFromZero));
    }

    /// <summary>The two ends of the band a window pays out of.</summary>
    private static (int Min, int Max) BandOf(WindowEffort effort) => effort switch
    {
        // A man who played has already been billed for the match inside it, and the bill is
        // the drain the engine took off him tick by tick. He is given the small band here and
        // the big one is never also given, or a squad would recover a season's worth of rest
        // in a fortnight.
        { PlayedInMatch: true } => (MinAfterPlaying, MaxAfterPlaying),

        { ClubPlayedInWindow: true } => (MinOnTheBench, MaxOnTheBench),

        _ => (MinFullRest, MaxFullRest)
    };
}
