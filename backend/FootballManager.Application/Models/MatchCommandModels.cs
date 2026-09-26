using FootballManager.Domain.Matches;
using FootballManager.Domain.Teams;

namespace FootballManager.Application.Models;

/// <summary>
/// Outcome of a state changing match command. The engine answers with typed events
/// instead of throwing for an expected refusal, so a client can render the reason
/// without special casing transport errors.
/// </summary>
public class MatchCommandResult
{
    public required bool Accepted { get; init; }
    public required Guid MatchId { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<MatchEngineEvent> Events { get; init; } = Array.Empty<MatchEngineEvent>();
}

/// <summary>
/// The eleven plus the bench of both clubs, as locked in when the match started.
/// </summary>
public class MatchLineup
{
    public required Guid MatchId { get; init; }
    public required int UserTeamIndex { get; init; }
    public required Team HomeTeam { get; init; }
    public required Team AwayTeam { get; init; }
    public IReadOnlyList<MatchPlayerSnapshot> HomeLineup { get; init; } = Array.Empty<MatchPlayerSnapshot>();
    public IReadOnlyList<MatchPlayerSnapshot> AwayLineup { get; init; } = Array.Empty<MatchPlayerSnapshot>();
    public IReadOnlyList<MatchPlayerSnapshot> HomeBench { get; init; } = Array.Empty<MatchPlayerSnapshot>();
    public IReadOnlyList<MatchPlayerSnapshot> AwayBench { get; init; } = Array.Empty<MatchPlayerSnapshot>();
}

/// <summary>
/// Result of a finished match, served once the engine working memory is gone. It is
/// the persisted view a results screen renders.
/// </summary>
public class MatchResultView
{
    public required Guid MatchId { get; init; }
    public required int HomeScore { get; init; }
    public required int AwayScore { get; init; }
    public required int HomeShots { get; init; }
    public required int AwayShots { get; init; }
    public required int HomeShotsOnTarget { get; init; }
    public required int AwayShotsOnTarget { get; init; }
    public required int HomeCorners { get; init; }
    public required int AwayCorners { get; init; }
    public required int HomeCards { get; init; }
    public required int AwayCards { get; init; }
    public required int HomeFouls { get; init; }
    public required int AwayFouls { get; init; }
    public required int HomePossession { get; init; }
    public required int AwayPossession { get; init; }
    public required string FormationHome { get; init; }
    public required string FormationAway { get; init; }
    public required int SubstitutionsHome { get; init; }
    public required int SubstitutionsAway { get; init; }
}

/// <summary>
/// Live view of a running match. It is served from the engine's working memory while
/// the match is in progress, and rebuilt from the persisted snapshot afterwards.
/// </summary>
public class MatchStateView
{
    public required Guid MatchId { get; init; }
    public required int HomeScore { get; init; }
    public required int AwayScore { get; init; }
    public required int Minute { get; init; }
    public required int Second { get; init; }
    public required int Half { get; init; }
    public required int Sequence { get; init; }
    public required int StoppageTimeMinutes { get; init; }
    public required string Status { get; init; }
    public required bool IsPaused { get; init; }
    public required bool IsHalfTime { get; init; }
    public required bool IsFinished { get; init; }
    public required int Speed { get; init; }
    public required int HomeShots { get; init; }
    public required int AwayShots { get; init; }
    public required int HomeShotsOnTarget { get; init; }
    public required int AwayShotsOnTarget { get; init; }
    public required int HomeCorners { get; init; }
    public required int AwayCorners { get; init; }
    public required int HomeCards { get; init; }
    public required int AwayCards { get; init; }
    public required int HomeFouls { get; init; }
    public required int AwayFouls { get; init; }
    public required int HomePossession { get; init; }
    public required int AwayPossession { get; init; }
    public required int SubstitutionsUsedHome { get; init; }
    public required int SubstitutionsUsedAway { get; init; }
    public required bool PenaltyAwaitingSelection { get; init; }
}
