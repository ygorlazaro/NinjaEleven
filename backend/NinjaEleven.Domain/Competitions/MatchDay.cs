namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// A day of the season's calendar: a number, a date, and the windows of football in it.
/// </summary>
public class MatchDay
{
    public Guid Id { get; private set; }
    public Guid SeasonId { get; private set; }

    /// <summary>Counted from one. Matchday 1 is the first day of the season.</summary>
    public int Number { get; private set; }

    /// <summary>
    /// The date this matchday falls on. It is the season's start date plus its own offset,
    /// so a matchday is a day of the calendar rather than a day of the process.
    /// </summary>
    public DateOnly Date { get; private set; }

    private MatchDay() { }

    public static MatchDay Create(Guid seasonId, int number, DateOnly date)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number), number, "A matchday is counted from one.");
        }

        return new MatchDay
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Number = number,
            Date = date
        };
    }
}
