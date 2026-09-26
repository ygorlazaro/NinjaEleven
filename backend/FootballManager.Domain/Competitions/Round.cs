namespace FootballManager.Domain.Competitions;

/// <summary>
/// A round (rodada) inside one edition of a competition. All fixtures of a round
/// belong to the same competition season.
/// </summary>
public class Round
{
    public Guid Id { get; private set; }
    public Guid CompetitionSeasonId { get; private set; }
    public int Number { get; private set; }

    private Round() { }

    public static Round Create(Guid competitionSeasonId, int number)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "The round number must be greater than zero.");
        }

        return new Round
        {
            Id = Guid.NewGuid(),
            CompetitionSeasonId = competitionSeasonId,
            Number = number
        };
    }
}
