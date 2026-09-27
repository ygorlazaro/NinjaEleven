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

        public int Goals { get; private set; }
        public int OwnGoals { get; private set; }
        public int Assists { get; private set; }

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
    ///
    /// There is deliberately no "minutes played" column. The engine keeps a clock, not a
    /// stopwatch on each man, and a number this table showed would be a guess wearing the
    /// costume of a measurement.
    /// </summary>
    public void ApplyFrom(MatchPlayerSnapshot player, bool started, bool wasOnBenchUnused = false)
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
    }
}
