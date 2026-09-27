using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Seasons;

/// <summary>
/// One edition of the world.
///
/// A season is identified by its number and shown as a roman numeral, never by the calendar
/// year it happens to fall in: the world outlives the calendar, and two seasons in the same
/// year are two seasons. The name is derived from the number so the two can never disagree.
/// </summary>
public class Season
{
    public Guid Id { get; private set; }

    /// <summary>The season's number, counted from one. Its identity.</summary>
    public int Number { get; private set; }

    /// <summary>"Temporada III". Derived from <see cref="Number"/> and never set by hand.</summary>
    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public SeasonStatus Status { get; private set; }

    private Season() { }

    public static Season Create(int number, DateOnly startDate, DateOnly endDate)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                number,
                "A season is counted from one; there is no season before the first.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("A season cannot end before it starts.", nameof(endDate));
        }

        return new Season
        {
            Id = Guid.NewGuid(),
            Number = number,
            Name = RomanNumeral.SeasonName(number),
            StartDate = startDate,
            EndDate = endDate,
            Status = SeasonStatus.NotStarted
        };
    }

    /// <summary>The season that follows this one, on the same length of calendar.</summary>
    public Season Next() => Create(
        Number + 1,
        StartDate.AddDays((EndDate.DayNumber - StartDate.DayNumber) + 1),
        EndDate.AddDays((EndDate.DayNumber - StartDate.DayNumber) + 1));

    public bool ContainsDate(DateOnly date) => date >= StartDate && date <= EndDate;

    public void Start()
    {
        if (Status == SeasonStatus.Finished)
        {
            throw new InvalidOperationException("A finished season cannot start again.");
        }

        Status = SeasonStatus.InProgress;
    }

    public void Finish()
    {
        if (Status == SeasonStatus.NotStarted)
        {
            throw new InvalidOperationException("A season that never started cannot be finished.");
        }

        Status = SeasonStatus.Finished;
    }
}
