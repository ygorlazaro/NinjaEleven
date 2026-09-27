namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One window of football: the fixtures of one competition that are played on one matchday.
///
/// A round is no longer a unit of time on its own. Two competitions run at the same time, so
/// a matchday holds a window for the championship and a window for the cup, and a round is
/// the set of fixtures in one of those windows. The window is what energy is recovered
/// against and what a matchday scoreboard shows, so it is the unit the calendar is built on.
/// </summary>
public class Round
{
    public Guid Id { get; private set; }
    public Guid CompetitionSeasonId { get; private set; }

    /// <summary>Counted from one inside its own competition: matchday 4, window 2.</summary>
    public int Number { get; private set; }

    /// <summary>The matchday this window belongs to, and null only while it is being built.</summary>
    public Guid? MatchDayId { get; private set; }

    /// <summary>
    /// Which window of the matchday this is. The championship is the first and the cup the
    /// second, so the final of a season is the last football played without anything having to
    /// be sorted afterwards.
    /// </summary>
    public int Window { get; private set; }

    /// <summary>
    /// When the last fixture of this window was finished, and null while any of them are
    /// still to be played. It is the guard that stops the window's recovery from being applied
    /// twice, and it is why a window can be closed exactly once.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private Round() { }

    public static Round Create(Guid competitionSeasonId, int number, int window = CompetitionRules.ChampionshipWindow)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "The round number must be greater than zero.");
        }

        if (window <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "A window is counted from one.");
        }

        return new Round
        {
            Id = Guid.NewGuid(),
            CompetitionSeasonId = competitionSeasonId,
            Number = number,
            Window = window
        };
    }

    public void ScheduleOn(Guid matchDayId) => MatchDayId = matchDayId;

    public void Unschedule() => MatchDayId = null;

    public void Complete() => CompletedAt = DateTimeOffset.UtcNow;

    /// <summary>Whether every fixture of this window has been played.</summary>
    public bool IsCompleted => CompletedAt is not null;
}
