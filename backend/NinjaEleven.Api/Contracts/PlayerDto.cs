using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Contracts;

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

    /// <summary>
    /// Matches of his club the injury still keeps him out of, so a screen can say for how
    /// long instead of only that something is wrong.
    /// </summary>
    public int InjuryMatchesRemaining { get; init; }

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

    /// <summary>
    /// Matches of his club the injury still keeps him out of, so a screen can say for how
    /// long instead of only that something is wrong.
    /// </summary>
    public int InjuryMatchesRemaining { get; init; }

    public bool IsAvailable { get; init; }
    public Guid TeamId { get; init; }
    public Guid SeasonId { get; init; }
}

/// <summary>
/// A player whole, for the profile screen: who he is, the two totals columns, and every
/// match he played. The screen filters the list by season; it does not refetch, because
/// then the two filters would be two different sets of matches.
/// </summary>
public class PlayerProfileDto
{
    public Guid PlayerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public int Age { get; init; }
    public DateOnly BirthDate { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public Guid? SeasonId { get; init; }
    public Guid? TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public int Energy { get; init; }
    public bool IsAvailable { get; init; }
    public string Injury { get; init; } = string.Empty;
    public int InjuryMatchesRemaining { get; init; }
    public PlayerCareerLineDto Season { get; init; } = new();
    public PlayerCareerLineDto Total { get; init; } = new();
    public List<PlayerMatchLineDto> History { get; init; } = new();
}

/// <summary>
/// One row of the two totals columns. Appearances is a pair, not a number: "14 (3)" is
/// fourteen matches and three of them off the bench, and a single figure cannot say which.
/// </summary>
public class PlayerCareerLineDto
{
    public int Appearances { get; init; }
    public int Started { get; init; }
    public int CameOn { get; init; }
    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Saves { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int Injuries { get; init; }
    public int MatchesMissed { get; init; }
}

public class PlayerMatchLineDto
{
    public Guid MatchId { get; init; }
    public Guid? SeasonId { get; init; }
    public bool Started { get; init; }
    public bool CameOn { get; init; }
    public bool SubbedOff { get; init; }
    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Saves { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public bool WasInjured { get; init; }
    public bool InjuredOff { get; init; }
    public bool IsHome { get; init; }
    public string OpponentName { get; init; } = string.Empty;
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public int RoundNumber { get; init; }
}
