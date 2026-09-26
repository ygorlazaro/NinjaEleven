using FootballManager.Domain.Enums;
using FootballManager.Domain.Matches;

namespace FootballManager.Api.Contracts;

/// <summary>
/// Answer of a state changing match command. The engine refuses expected problems
/// with <c>Accepted = false</c> plus a reason, instead of failing the request.
/// </summary>
public class MatchCommandResultDto
{
    public bool Accepted { get; init; }
    public Guid MatchId { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<MatchEngineEventDto> Events { get; init; } = Array.Empty<MatchEngineEventDto>();
}

/// <summary>
/// An event exactly as the engine produced it, in the shape the frontend feed uses.
/// </summary>
public class MatchEngineEventDto
{
    public int Sequence { get; init; }
    public int Minute { get; init; }
    public MatchEventType Type { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? PlayerId { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public string Icon { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// The eleven and the bench of both clubs, locked in at kick-off.
/// </summary>
public class MatchLineupDto
{
    public Guid MatchId { get; init; }
    public int UserTeamIndex { get; init; }
    public TeamDto HomeTeam { get; init; } = new();
    public TeamDto AwayTeam { get; init; } = new();
    public IReadOnlyList<MatchPlayerDto> HomeLineup { get; init; } = Array.Empty<MatchPlayerDto>();
    public IReadOnlyList<MatchPlayerDto> AwayLineup { get; init; } = Array.Empty<MatchPlayerDto>();
    public IReadOnlyList<MatchPlayerDto> HomeBench { get; init; } = Array.Empty<MatchPlayerDto>();
    public IReadOnlyList<MatchPlayerDto> AwayBench { get; init; } = Array.Empty<MatchPlayerDto>();
}

/// <summary>
/// A player as the match engine sees him: identity, position and the attributes that
/// drive the simulation. The frontend only presents these values.
/// </summary>
public class MatchPlayerDto
{
    public Guid PlayerId { get; init; }
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
    public int MatchYellowCards { get; init; }
    public bool RedCard { get; init; }
    public bool EmergencyGK { get; init; }
    public bool InjuredOff { get; init; }
    public bool SubbedIn { get; init; }
    public int Goals { get; init; }
}

/// <summary>
/// Live state of a match, served from the engine's working memory while it runs and
/// from the persisted row once it is over.
/// </summary>
public class MatchStateDto
{
    public Guid MatchId { get; init; }
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public int Minute { get; init; }
    public int Second { get; init; }
    public MatchHalf CurrentHalf { get; init; }
    public int Sequence { get; init; }
    public int StoppageTimeMinutes { get; init; }
    public MatchStatus Status { get; init; }
    public bool IsPaused { get; init; }
    public bool IsHalfTime { get; init; }
    public bool IsFinished { get; init; }
    public int Speed { get; init; }
    public TeamMatchStatsDto[] Stats { get; init; } = Array.Empty<TeamMatchStatsDto>();
    public MatchPossessionDto Possession { get; init; } = new();
    public bool PenaltyAwaitingSelection { get; init; }

    /// <summary>
    /// The players the manager can send to the spot, best first. It is filled only while
    /// a penalty of his own club is waiting for a taker.
    /// </summary>
    public PenaltyTakerOptionsDto Penalty { get; init; } = new();

    /// <summary>
    /// The club the manager is watching, echoed so a client always knows which team it
    /// is sending commands for.
    /// </summary>
    public Guid? UserTeamId { get; init; }

    public int SubstitutionsUsedHome { get; init; }
    public int SubstitutionsUsedAway { get; init; }
}

/// <summary>
/// Who can take the penalty the engine awarded, and whether the manager has to name one.
/// </summary>
public class PenaltyTakerOptionsDto
{
    public bool AwaitingSelection { get; init; }
    public IReadOnlyList<MatchPlayerDto> Candidates { get; init; } = Array.Empty<MatchPlayerDto>();
}

public class TeamMatchStatsDto
{
    public int Shots { get; init; }
    public int ShotsOnTarget { get; init; }
    public int Corners { get; init; }
    public int Cards { get; init; }
    public int Fouls { get; init; }
    public int Possession { get; init; }
}

public class MatchPossessionDto
{
    public int Team { get; init; }
    public Guid? PlayerId { get; init; }
}

/// <summary>
/// Result of a match, as a results screen renders it.
/// </summary>
public class MatchResultDto
{
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public IReadOnlyList<PlayerMatchStatsDto> PlayerStats { get; init; } = Array.Empty<PlayerMatchStatsDto>();
    public int HomeShots { get; init; }
    public int AwayShots { get; init; }
    public int HomeShotsOnTarget { get; init; }
    public int AwayShotsOnTarget { get; init; }
    public int HomeCorners { get; init; }
    public int AwayCorners { get; init; }
    public int HomeCards { get; init; }
    public int AwayCards { get; init; }
    public int HomeFouls { get; init; }
    public int AwayFouls { get; init; }
    public int HomePossession { get; init; }
    public int AwayPossession { get; init; }
    public string FormationHome { get; init; } = string.Empty;
    public string FormationAway { get; init; } = string.Empty;
    public int SubstitutionsHome { get; init; }
    public int SubstitutionsAway { get; init; }
}

public class PlayerMatchStatsDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public int Goals { get; init; }
    public int YellowCards { get; init; }
    public bool RedCard { get; init; }
}

/// <summary>
/// Fixtures of a round that were played without a manager watching. The round is over
/// once this comes back, so the next one can be played.
/// </summary>
public class RoundSimulationDto
{
    public Guid RoundId { get; init; }
    public IReadOnlyList<Guid> PlayedMatchIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
/// Live score of one match of a round, published to everyone following the round. It is
/// how a client shows the other matches of the matchday while it watches its own in
/// full, without asking for anything.
/// </summary>
public class MatchScoreDto
{
    public Guid RoundId { get; init; }
    public Guid MatchId { get; init; }
    public Guid FixtureId { get; init; }
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string HomeShortName { get; init; } = string.Empty;
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public string AwayShortName { get; init; } = string.Empty;
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public int Minute { get; init; }
    public string Half { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsFinished { get; init; }
}
