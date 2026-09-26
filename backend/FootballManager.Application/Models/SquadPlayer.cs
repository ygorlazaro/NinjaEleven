using FootballManager.Domain.Matches;
using FootballManager.Domain.Players;
using FootballManager.Domain.Teams;

namespace FootballManager.Application.Models;

/// <summary>
/// A player as seen from a squad: static identity plus the state he has in the season.
/// </summary>
public class SquadPlayer
{
    public required Player Player { get; init; }
    public required PlayerSeasonState SeasonState { get; init; }

    public bool IsAvailable => SeasonState.IsAvailable;
}

/// <summary>
/// A fixture enriched with both clubs and the result of the match played from it.
/// </summary>
public class FixtureDetails
{
    public required Fixture Fixture { get; init; }
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
    public Match? Match { get; init; }
}

/// <summary>
/// Everything a client needs to re-render a match: the session state plus the ordered
/// event log. This is the snapshot served by the REST endpoint that reconnects a client
/// before it resumes following SignalR.
/// </summary>
public class MatchSnapshot
{
    public required Match Match { get; init; }
    public IReadOnlyList<MatchEvent> Events { get; init; } = Array.Empty<MatchEvent>();
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
}

/// <summary>
/// Result of creating a competition edition: the edition itself plus the generated
/// schedule.
/// </summary>
public class LeagueSetup
{
    public required Guid CompetitionSeasonId { get; init; }
    public required Guid SeasonId { get; init; }
    public required Guid CompetitionId { get; init; }
    public IReadOnlyList<FixtureDetails> Fixtures { get; init; } = Array.Empty<FixtureDetails>();
}
