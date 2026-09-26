using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Seasons;

/// <summary>
/// A season of the game. Competitions, season states and fixtures are all scoped
/// by season, so a season is a top-level temporal boundary of the domain.
/// </summary>
public class Season
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public SeasonStatus Status { get; private set; }

    private Season() { }

    public static Season Create(string name, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The season cannot end before it starts.", nameof(endDate));
        }

        return new Season
        {
            Id = Guid.NewGuid(),
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            StartDate = startDate,
            EndDate = endDate,
            Status = SeasonStatus.NotStarted
        };
    }

    public bool ContainsDate(DateOnly date) => date >= StartDate && date <= EndDate;

    public void Start() => Status = SeasonStatus.InProgress;

    public void Finish() => Status = SeasonStatus.Finished;
}
