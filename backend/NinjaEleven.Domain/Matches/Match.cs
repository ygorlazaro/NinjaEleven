using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Domain.Matches;

/// <summary>
    /// The playable session of a fixture: logical clock, score, half and the event
    /// sequence. The server owns this clock; clients only send commands. Because the
    /// whole state is persisted, a disconnected client can be re-synchronised with a
    /// REST snapshot and then resume following the SignalR event stream.
    /// </summary>
    public class Match
    {
        public Guid Id { get; private set; }
        public Guid FixtureId { get; private set; }
        public Guid HomeTeamId { get; private set; }
        public Guid AwayTeamId { get; private set; }

        public MatchStatus Status { get; private set; }
        public MatchHalf Half { get; private set; }
        public int CurrentMinute { get; private set; }
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }

        /// <summary>
        /// Monotonic sequence of every event published for this match. Clients detect
        /// gaps after a reconnect by comparing the last received sequence with the next one.
        /// </summary>
        public int Sequence { get; private set; }

        /// <summary>
        /// Seed handed to the match engine. Replaying a match with the same seed and the
        /// same configuration must produce the same events and the same result.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>
        /// When this match row was created. A fixture can be replayed after an abandoned
        /// match, so several rows can point at the same fixture; the newest one is the
        /// match of the fixture.
        /// </summary>
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>
        /// Which competition this match belongs to. A match is a match in a competition and
        /// never just a match: the table counts a league game and nothing else, the aggregate
        /// counts a cup leg and nothing else, and a fixture row cannot be read without it.
        /// </summary>
        public CompetitionType CompetitionType { get; private set; }

        /// <summary>Which window of the matchday it was played in.</summary>
        public int Window { get; private set; }

        /// <summary>
        /// Which of the home club's two shirts was on the pitch.
        ///
        /// <para>
        /// It is stamped on the match rather than worked out whenever a screen asks, because
        /// two dark shirts are a decision somebody has to make and the answer has to be the same
        /// one at the whistle and at the final whistle. A matchscreen that redrew the draw on
        /// every reload would change a visitor's shirt under the manager halfway through the
        /// second half.
        /// </para>
        /// </summary>
        public KitSide HomeKitSide { get; private set; }

        /// <summary>Which of the visiting club's two shirts was on the pitch.</summary>
        public KitSide AwayKitSide { get; private set; }

        /// <summary>How many people turned up, worked out from the ground and the match.</summary>
        public int Attendance { get; private set; }

        /// <summary>What a seat cost on the day. Kept, so a price change never rewrites history.</summary>
        public decimal TicketPrice { get; private set; }

        /// <summary>Everything the gate took, in limos.</summary>
        public decimal GrossRevenue { get; private set; }

        /// <summary>
        /// The share the club that hosted the match took: two thirds of a division game, half
        /// of a cup tie. It was stamped on the row rather than recomputed, so a ledger written
        /// in one season still adds up after the rule that produced it has been read again.
        /// </summary>
        public decimal HomeRevenue { get; private set; }

        /// <summary>What the club that travelled took, which is the rest of the gate.</summary>
        public decimal AwayRevenue { get; private set; }

        /// <summary>
        /// The process whose memory holds this match's working state.
        ///
        /// The engine's state lives in RAM while a match is being played, and it is no longer
        /// necessarily the API's: the Scheduler plays the matches nobody is watching and the
        /// API plays the one somebody is. Two processes, one world, so a row has to say whose
        /// working memory it belongs to. Without it a process that restarts abandons every
        /// open match it can find, including the one the other process is in the middle of.
        /// </summary>
        public string SessionHost { get; private set; } = string.Empty;

    /// <summary>
    /// The shape this match was played in, from the <see cref="Tactics"/> catalogue, or empty
    /// for a match that was played in whatever the club is made of.
    ///
    /// <para>
    /// It is recorded because "the last tactic this club used" is a question a manager asks
    /// before the next match and cannot be answered from a plan that was never made: with no
    /// plan there is nothing to repeat, and a club that changed nothing is a club that should
    /// come out the same way as last time. So the match carries the fact rather than the plan
    /// carrying a copy of it, and the two can never disagree about what was played.
    /// </para>
    /// </summary>
    public string TacticCode { get; private set; } = string.Empty;

    /// <summary>
    /// The shape the visiting club went out in, for the same reason as the home club's.
    ///
    /// It is a second column rather than a row per club because a match is one row and the
    /// question it is asked — "what did these two clubs do" — is about the match, not about a
    /// side of it.
    /// </summary>
    public string AwayTacticCode { get; private set; } = string.Empty;

        /// <summary>
        /// The last moment this match was ticked. It is the lease that lets another process
        /// take a match over once its owner has gone quiet: a process that dies mid-match does
        /// not get to leave its games locked for ever.
        /// </summary>
        public DateTimeOffset? SessionHeartbeatAt { get; private set; }

        /// <summary>The gate as one value, for a caller that wants all of it at once.</summary>
        public GateReceipt Gate => GateReceipt.For(Attendance, TicketPrice, GateSplit.For(CompetitionType));

        private Match() { }

    public static Match Create(
        Guid fixtureId,
        Guid homeTeamId,
        Guid awayTeamId,
        CompetitionType competitionType = CompetitionType.League,
        int window = Competitions.CompetitionRules.ChampionshipWindow)
    {
        return new Match
        {
            Id = Guid.NewGuid(),
            FixtureId = fixtureId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Status = MatchStatus.Scheduled,
            Half = MatchHalf.First,
            CurrentMinute = 0,
            HomeScore = 0,
            AwayScore = 0,
            Sequence = 0,
            Seed = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CompetitionType = competitionType,
            Window = window,
            HomeKitSide = KitSide.Home,
            AwayKitSide = KitSide.Home
        };
    }

    /// <summary>
    /// Records the two shirts this match was played in. Written once, at the kick-off that
    /// drew them, and never drawn again.
    /// </summary>
    public void RecordKits(KitSide home, KitSide away)
    {
        HomeKitSide = home;
        AwayKitSide = away;
    }

    public bool IsFinished => Status is MatchStatus.Finished or MatchStatus.Abandoned;

    public bool IsInProgress => Status is MatchStatus.InProgress or MatchStatus.SecondHalf;

    /// <summary>
    /// Whether the match is somewhere between kick-off and the final whistle, which is the
    /// only stretch in which two processes could both believe they own it.
    /// </summary>
    public bool IsLive => Status is MatchStatus.KickOff or MatchStatus.InProgress
        or MatchStatus.HalfTime or MatchStatus.SecondHalf;

    /// <summary>
    /// Takes the match's working memory on behalf of a process, and starts its lease.
    /// It is written at kick-off and refreshed on every tick.
    /// </summary>
    public void ClaimSession(string host, DateTimeOffset now)
    {
        SessionHost = host ?? string.Empty;
        SessionHeartbeatAt = now;
    }

    /// <summary>
    /// Records the shape the match kicked off in. Written once, at the kick-off that decided
    /// it, and read by every match after this one when a manager has no plan of his own.
    /// </summary>
    public void RecordTactic(string tacticCode) => TacticCode = tacticCode ?? string.Empty;

    /// <summary>Records the shape the visiting club kicked off in.</summary>
    public void RecordAwayTactic(string tacticCode) => AwayTacticCode = tacticCode ?? string.Empty;

    /// <summary>Renews the lease on a match that has just been advanced.</summary>
    public void TouchSession(DateTimeOffset now) => SessionHeartbeatAt = now;

    /// <summary>Whether this process is the one holding the match's working memory.</summary>
    public bool IsOwnedBy(string host) =>
        !string.IsNullOrEmpty(SessionHost)
        && string.Equals(SessionHost, host, StringComparison.Ordinal);

    /// <summary>
    /// Whether a process may take this match over: it is this process's own match, whose
    /// memory died with it, or it is a match whose owner has stopped renewing its lease.
    /// Anything else belongs to somebody who is still playing it.
    /// </summary>
    public bool CanBeReclaimedBy(string host, DateTimeOffset now, TimeSpan lease) =>
        IsOwnedBy(host) || SessionHeartbeatAt is null || SessionHeartbeatAt.Value + lease <= now;

    /// <summary>
    /// Works out the crowd and the gate, and stamps them onto the match.
    ///
    /// It happens at kick-off rather than at the end because a manager watching a match should
    /// see who is in the stand from the first whistle, and because the crowd is a fact about
    /// the fixture rather than about how the game turned out: a 5-0 does not empty a stand
    /// that had already filled it.
    /// </summary>
    public void KickOff(int seed, Stadium homeStadium, AttendanceContext attendance)
    {
        EnsureNotFinished();

        Seed = seed;
        Status = MatchStatus.KickOff;
        Half = MatchHalf.First;
        CurrentMinute = 0;

        if (homeStadium is null) return;

        // The crowd is drawn from the match's own seed, so a replayed match fills the same
        // seats. A ground that draws its crowd from a shared source is a ground whose crowd
        // can differ between two runs of the same fixture, and a manager who sees 4,100 the
        // first time and 1,900 the second has been given two different facts.
        var random = new DeterministicRandomSource(seed);
        var noise = AttendanceCalculator.RandomFloor + random.NextDouble()
            * (AttendanceCalculator.RandomCeiling - AttendanceCalculator.RandomFloor);

        Attendance = AttendanceCalculator.Calculate(homeStadium, attendance, noise);

        var gate = GateReceipt.For(Attendance, homeStadium.TicketPrice, GateSplit.For(CompetitionType));
        TicketPrice = gate.TicketPrice;
        GrossRevenue = gate.GrossRevenue;
        HomeRevenue = gate.HomeRevenue;
        AwayRevenue = gate.AwayRevenue;
    }

    public void AdvanceClockTo(int minute)
    {
        if (minute < CurrentMinute)
        {
            throw new InvalidOperationException("The match clock can never move backwards.");
        }

        CurrentMinute = minute;
    }

    /// <summary>
    /// Mirrors the engine's working memory onto the persisted row. This is how the
    /// clock, the score and the event sequence reach the database; the engine itself
    /// never writes.
    ///
    /// The half comes with it, because a leg that ends on penalties is not a match that
    /// ended in the second half: a results screen that says otherwise is telling the
    /// manager the ninety minutes were the whole of it. It is optional so a caller that is
    /// only moving the clock on does not have to answer a question it was not asked.
    /// </summary>
    public void ApplyEngineState(
        int minute,
        int homeScore,
        int awayScore,
        int sequence,
        MatchHalf? half = null)
    {
        if (minute < CurrentMinute)
        {
            throw new InvalidOperationException("The match clock can never move backwards.");
        }

        CurrentMinute = minute;
        HomeScore = homeScore;
        AwayScore = awayScore;
        Sequence = sequence;

        if (half is { } value)
        {
            Half = value;
        }
    }

    public void StartFirstHalf() => Status = MatchStatus.InProgress;

    public void ReachHalfTime()
    {
        EnsureNotFinished();
        Status = MatchStatus.HalfTime;
    }

    public void StartSecondHalf()
    {
        EnsureNotFinished();
        Half = MatchHalf.Second;
        Status = MatchStatus.SecondHalf;
    }

    public void RegisterGoal(bool isHome)
    {
        if (isHome)
        {
            HomeScore++;
        }
        else
        {
            AwayScore++;
        }
    }

    public void RegisterOwnGoal(bool isHomeTeamScored)
    {
        RegisterGoal(isHomeTeamScored);
    }

    public void Finish()
    {
        Status = MatchStatus.Finished;
        SessionHeartbeatAt = null;
    }

    public void Abandon()
    {
        Status = MatchStatus.Abandoned;
        SessionHeartbeatAt = null;
    }

    /// <summary>
    /// Reserves the next event sequence number. Only the match simulation is allowed
    /// to call it, which guarantees a single increasing stream per match.
    /// </summary>
    public int NextSequence() => ++Sequence;

    private void EnsureNotFinished()
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The match has already ended.");
        }
    }
}
