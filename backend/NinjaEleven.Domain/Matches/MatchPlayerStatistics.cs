using NinjaEleven.Domain.Common;
namespace NinjaEleven.Domain.Matches;

/// <summary>
    /// What one player did in one match, kept after the match is gone.
    ///
    /// The live session holds every player's line for the ninety minutes it is running, and
    /// then it is discarded. That is fine for a results screen, which reads the match, and it
    /// is not fine for a career: a manager who wants to know what a striker has done this
    /// season, or whether a defender was ever taken off, has nowhere to ask. Everything here
    /// is a fact about a match that has already been played, written once when the whistle
    /// went and read many times after.
    ///
    /// Appearances are counted three ways on purpose. <see cref="Started"/>, <see cref="CameOn"/>
    /// and <see cref="WasOnBenchUnused"/> are not three ways of saying one thing: a manager reads
    /// "14 (3) [2]" as fourteen games started, three off the bench, and two matches on the bench
    /// without playing, and a table that only says "14" cannot tell a regular from a man the staff
    /// stopped believing in.
    /// </summary>
    public class MatchPlayerStatistics
    {
        public Guid Id { get; private set; }
        public Guid MatchId { get; private set; }
        public Guid PlayerId { get; private set; }
        public Guid TeamId { get; private set; }

        /// <summary>The season the match belonged to, so a history can be read per season.</summary>
        public Guid? SeasonId { get; private set; }

        /// <summary>True when the player was one of the eleven who kicked off.</summary>
        public bool Started { get; private set; }

        /// <summary>True when he came on during the match.</summary>
        public bool CameOn { get; private set; }

        /// <summary>True when he finished the match on the bench, having played.</summary>
        public bool SubbedOff { get; private set; }

        /// <summary>True when he was on the bench for the whole match and never played.</summary>
        public bool WasOnBenchUnused { get; private set; }

        /// <summary>
        /// How many minutes of the match he was actually on the pitch.
        ///
        /// <para>
        /// This used to not exist, and the reason it did not is the reason it now does: the
        /// engine keeps a clock, not a stopwatch on each man, so a number this table showed
        /// would have been a guess wearing the costume of a measurement. It is no longer a
        /// guess — the engine stamps the minute a man leaves the pitch for every way of
        /// leaving it, and a substitute's <c>EnteredAtMinute</c> is the minute he came on.
        /// The measurement exists; the column was what was missing.
        /// </para>
        ///
        /// <para>
        /// It is worth persisting for three reasons, and the third is the one that settles it.
        /// A recovery is measured against the minutes actually played, so without the column a
        /// season's worth of fatigue could not be re-derived from the record and the number
        /// that decided a rotation could not be checked afterwards. A player who came on at
        /// the eighty-fifth and one who started and was taken off at the same minute have the
        /// same two appearance flags in some order — <see cref="Started"/> and
        /// <see cref="CameOn"/> — and only the minutes tell them apart.
        /// </para>
        /// </summary>
        public int MinutesPlayed { get; private set; }

        public int Goals { get; private set; }
        public int OwnGoals { get; private set; }

        /// <summary>
        /// Passes that set a goal up, and only those.
        /// </summary>
        /// <para>
        /// The column existed and nothing ever wrote to it, which is the worst kind of missing
        /// value: not a null that said "nobody knows", but a zero that said this striker has
        /// never created a goal in his life, read by a scorers table and a profile as though
        /// somebody had checked. It is the pass that a goal came from directly, so a goal off
        /// a rebound has no assist — the ball was already loose, and crediting the man whose
        /// shot it was would be an assist for a shot that had already missed.
        /// </para>
        /// </summary>
        public int Assists { get; private set; }

        // --- The tally -----------------------------------------------------------
        //
        // What he did, as opposed to what it was worth. These are kept so that the rating
        // below can be read rather than believed: a card saying 8.1 is a claim, and a card
        // saying 8.1 next to two goals and four shots on target is evidence.

        public int ShotsOnTarget { get; private set; }
        public int ShotsOffTarget { get; private set; }
        public int DuelsWon { get; private set; }
        public int DuelsLost { get; private set; }
        public int FoulsCommitted { get; private set; }
        public int CornersWon { get; private set; }

        /// <summary>
        /// What the match was worth to him, out of ten, or null when he did not play enough
        /// of it to have one.
        /// </summary>
        /// <para>
        /// Stored rather than recomputed on demand, and that is a deliberate choice against
        /// the alternative. A rating is a reading of a tally, and the tally is a fact about a
        /// match that has been played; recomputing it later would mean recomputing it from a
        /// column that a later balance change had moved, so the same match would carry two
        /// different numbers depending on when it was asked for. A manager's history is not
        /// allowed to change because the engine was tuned.
        /// </para>
        /// </summary>
        public double? Rating { get; private set; }

        /// <summary>Goals kept out, which is what a goalkeeper's match is made of.</summary>
        public int Saves { get; private set; }

        public int YellowCards { get; private set; }
        public int RedCards { get; private set; }

        /// <summary>
        /// Whether he left the match hurt. A knock he played through is not the same absence
        /// as a knock that took him off, so the duration is stored as well.
        /// </summary>
        public bool WasInjured { get; private set; }

        public bool InjuredOff { get; private set; }

        public int InjuryMatchesOut { get; private set; }

        private MatchPlayerStatistics() { }

    public static MatchPlayerStatistics Create(
        Guid matchId,
        Guid playerId,
        Guid teamId,
        Guid? seasonId)
    {
        return new MatchPlayerStatistics
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            PlayerId = playerId,
            TeamId = teamId,
            SeasonId = seasonId
        };
    }

    /// <summary>
    /// Fills the line from a player as the match ended. The live snapshot is the only
    /// place this is known, so it is copied out here and the snapshot goes away.
    /// </summary>
    /// <param name="player">The man as the whistle went.</param>
    /// <param name="started">Whether he was one of the eleven who kicked off.</param>
    /// <param name="wasOnBenchUnused">Whether he never left the bench.</param>
    /// <param name="finalMinute">
    /// The last minute of the match, which is what a man who never came off is measured to.
    /// The snapshot knows every stamp except this one — a starter who played out the ninety
    /// has no <c>LeftAtMinute</c> — so the minute is passed in rather than guessed at, and a
    /// mismatch between this column and the recovery the match applied is then impossible.
    /// </param>
    public void ApplyFrom(
        MatchPlayerSnapshot player,
        bool started,
        bool wasOnBenchUnused = false,
        int finalMinute = 90)
    {
        Started = started;
        Goals = player.MatchGoals;
        OwnGoals = player.MatchOwnGoals;
        Saves = player.MatchSaves;
        YellowCards = player.MatchYellowCards;
        RedCards = player.RedCard ? 1 : 0;
        CameOn = player.SubbedIn;
        SubbedOff = player.SubbedOff;
        WasOnBenchUnused = wasOnBenchUnused;
        WasInjured = player.Injury != Injury.None;
        InjuredOff = player.InjuredOff;
        InjuryMatchesOut = player.InjuryMatchesOut;
        MinutesPlayed = player.MinutesPlayed(finalMinute);
        Assists = player.Performance.Assists;
        ShotsOnTarget = player.Performance.ShotsOnTarget;
        ShotsOffTarget = player.Performance.ShotsOffTarget;
        DuelsWon = player.Performance.DuelsWon;
        DuelsLost = player.Performance.DuelsLost;
        FoulsCommitted = player.Performance.FoulsCommitted;
        CornersWon = player.Performance.CornersWon;

        // Read at the whistle, once, from the tally as it stands. The match is over, so this
        // is the only moment at which the whole of it exists.
        Rating = MatchRating.Of(player, MinutesPlayed, finalMinute);
    }
}
