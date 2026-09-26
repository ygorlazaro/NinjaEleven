using FootballManager.Domain.Enums;

namespace FootballManager.Domain.Competitions;

/// <summary>
/// A competition template that exists across many seasons (e.g. "Campeonato Brasileiro").
/// The concrete season instance is <see cref="CompetitionSeason"/>.
/// </summary>
public class Competition
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public CompetitionType Type { get; private set; }

    private Competition() { }

    public static Competition Create(string name, CompetitionType type)
    {
        return new Competition
        {
            Id = Guid.NewGuid(),
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            Type = type
        };
    }
}
