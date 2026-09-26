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
    public PenaltyTakerOptions Penalty { get; init; } = new();

    /// <summary>
    /// The club a manager is watching, when the match was started by one. A client sends
    /// it back with the commands it issues, so it never has to guess which side of the
    /// scoreboard is his.
    /// </summary>
    public Guid? UserTeamId { get; init; }
}

/// <summary>
/// The score of a match as the rest of the matchday sees it. It is built from the live
/// session when there is one and from the persisted row after the match ends, so the
/// scoreboard never shows a match that stopped updating.
/// </summary>
public class MatchScoreRow
{
    public required Guid RoundId { get; init; }
    public required Guid MatchId { get; init; }
    public required Guid FixtureId { get; init; }
    public required Guid HomeTeamId { get; init; }
    public required string HomeTeamName { get; init; }
    public required string HomeShortName { get; init; }
    public required Guid AwayTeamId { get; init; }
    public required string AwayTeamName { get; init; }
    public required string AwayShortName { get; init; }
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }
    public required int Minute { get; init; }
    public required string Half { get; init; }
    public required string Status { get; init; }
    public required bool IsFinished { get; init; }
}

/// <summary>
/// Who can take a penalty right now. The engine decides when a penalty happens and which
/// club was awarded it; the manager of the awarded club names the taker, and only the
/// players still on the pitch are offered.
/// </summary>
public class PenaltyTakerOptions
{
    public bool MatchIsLive { get; init; } = true;
    public bool AwaitingSelection { get; init; }
    public IReadOnlyList<MatchPlayerSnapshot> Candidates { get; init; } = Array.Empty<MatchPlayerSnapshot>();
}
