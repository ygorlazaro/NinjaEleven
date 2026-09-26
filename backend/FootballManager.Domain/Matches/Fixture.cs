using FootballManager.Domain.Enums;

namespace FootballManager.Domain.Matches;

/// <summary>
/// A scheduled match. The result itself is not stored here: it belongs to the
/// <see cref="Match"/> session created when the fixture is played.
/// </summary>
public class Fixture
{
    public Guid Id { get; private set; }
    public Guid RoundId { get; private set; }
    public Guid HomeTeamId { get; private set; }
    public Guid AwayTeamId { get; private set; }
    public FixtureStatus Status { get; private set; }

    private Fixture() { }

    public static Fixture Create(Guid roundId, Guid homeTeamId, Guid awayTeamId)
    {
        if (homeTeamId == awayTeamId)
        {
            throw new ArgumentException("A team cannot play against itself.", nameof(awayTeamId));
        }

        return new Fixture
        {
            Id = Guid.NewGuid(),
            RoundId = roundId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Status = FixtureStatus.Scheduled
        };
    }

    public void MarkInProgress() => Status = FixtureStatus.InProgress;

    public void MarkFinished() => Status = FixtureStatus.Finished;

    public void Postpone() => Status = FixtureStatus.Postponed;
}
