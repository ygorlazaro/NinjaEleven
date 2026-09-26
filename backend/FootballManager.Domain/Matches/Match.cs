using FootballManager.Domain.Enums;

namespace FootballManager.Domain.Matches;

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

    private Match() { }

    public static Match Create(Guid fixtureId, Guid homeTeamId, Guid awayTeamId)
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
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public bool IsFinished => Status is MatchStatus.Finished or MatchStatus.Abandoned;

    public bool IsInProgress => Status is MatchStatus.InProgress or MatchStatus.SecondHalf;

    public void KickOff(int seed)
    {
        EnsureNotFinished();

        Seed = seed;
        Status = MatchStatus.KickOff;
        Half = MatchHalf.First;
        CurrentMinute = 0;
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
    /// </summary>
    public void ApplyEngineState(int minute, int homeScore, int awayScore, int sequence)
    {
        if (minute < CurrentMinute)
        {
            throw new InvalidOperationException("The match clock can never move backwards.");
        }

        CurrentMinute = minute;
        HomeScore = homeScore;
        AwayScore = awayScore;
        Sequence = sequence;
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
    }

    public void Abandon()
    {
        Status = MatchStatus.Abandoned;
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
