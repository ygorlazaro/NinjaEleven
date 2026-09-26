using FootballManager.Domain.Common;
using FootballManager.Domain.Enums;

namespace FootballManager.Api.Contracts;

public class PlayerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly BirthDate { get; init; }
    public int Age { get; init; }
    public Position Position { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
}

public class PlayerSeasonStateDto
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public Guid SeasonId { get; init; }
    public Guid TeamId { get; init; }
    public int Energy { get; init; }
    public int Goals { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int SuspensionMatches { get; init; }
    public Injury Injury { get; init; }
    public bool IsAvailable { get; init; }
}

public class SquadPlayerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; }
    public Position Position { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }

    public int Energy { get; init; }
    public int Goals { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int SuspensionMatches { get; init; }
    public Injury Injury { get; init; }
    public bool IsAvailable { get; init; }
    public Guid TeamId { get; init; }
    public Guid SeasonId { get; init; }
}
