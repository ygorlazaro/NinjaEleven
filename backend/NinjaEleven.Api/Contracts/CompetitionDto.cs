using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Api.Contracts;

public class TeamDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ShortName { get; init; } = string.Empty;
    public string PrimaryColor { get; init; } = string.Empty;
    public string SecondaryColor { get; init; } = string.Empty;
    public int Rating { get; init; }
}

public class CompetitionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CompetitionType Type { get; init; }
}

public class SeasonDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public SeasonStatus Status { get; init; }
}

public class RoundDto
{
    public Guid Id { get; init; }
    public Guid CompetitionSeasonId { get; init; }
    public int Number { get; init; }
}

public class FixtureDto
{
    public Guid Id { get; init; }
    public Guid RoundId { get; init; }
    public Guid HomeTeamId { get; init; }
    public Guid AwayTeamId { get; init; }
    public FixtureStatus Status { get; init; }
    public Guid? MatchId { get; init; }
    public int? HomeGoals { get; init; }
    public int? AwayGoals { get; init; }
    public TeamDto? HomeTeam { get; init; }
    public TeamDto? AwayTeam { get; init; }
}

public class MatchDto
{
    public Guid Id { get; init; }
    public Guid FixtureId { get; init; }
    public Guid HomeTeamId { get; init; }
    public Guid AwayTeamId { get; init; }
    public MatchStatus Status { get; init; }
    public MatchHalf Half { get; init; }
    public int CurrentMinute { get; init; }
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public int Sequence { get; init; }
    public int Seed { get; init; }
    public TeamDto? HomeTeam { get; init; }
    public TeamDto? AwayTeam { get; init; }
    public IReadOnlyList<MatchEventDto> Events { get; init; } = Array.Empty<MatchEventDto>();
}

public class MatchEventDto
{
    public Guid Id { get; init; }
    public Guid MatchId { get; init; }
    public int Sequence { get; init; }
    public int Minute { get; init; }
    public MatchEventType Type { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? PlayerId { get; init; }
    public Guid? SecondaryPlayerId { get; init; }
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public string Payload { get; init; } = "{}";

    /// <summary>
    /// Lifted out of the payload so a client can render a reconnected feed without
    /// parsing JSON itself.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;
}

public class StandingDto
{
    public Guid TeamId { get; init; }
    public TeamDto? Team { get; init; }
    public int Points { get; init; }
    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int GoalDifference { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
}

public class ScorerDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public int Age { get; init; }
    public int Goals { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
}

public class LeagueSetupResultDto
{
    public Guid CompetitionSeasonId { get; init; }
    public Guid CompetitionId { get; init; }
    public Guid SeasonId { get; init; }
    public IReadOnlyList<RoundDto> Rounds { get; init; } = Array.Empty<RoundDto>();
    public IReadOnlyList<FixtureDto> Fixtures { get; init; } = Array.Empty<FixtureDto>();
}
