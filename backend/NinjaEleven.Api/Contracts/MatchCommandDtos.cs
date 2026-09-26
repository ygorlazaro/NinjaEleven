using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Api.Contracts;

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

    /// <summary>
    /// He left the pitch and is spent: a substitute who has been taken off cannot be named
    /// again. The substitution screen uses this to stop offering him.
    /// </summary>
    public bool SubbedOff { get; init; }

    /// <summary>
    /// Goals for the season, and so the number on a player profile.
    /// </summary>
    public int Goals { get; init; }

    /// <summary>
    /// Goals in this match, and so the number on the eleven card under the scoreboard. It
    /// is a different number on purpose: a striker with nine for the season who has not
    /// scored today has scored nothing in this match.
    /// </summary>
    public int MatchGoals { get; init; }

    /// <summary>
    /// Own goals in this match, the red ball on his card. Nobody's favourite statistic and
    /// it belongs to him all the same.
    /// </summary>
    public int MatchOwnGoals { get; init; }

    /// <summary>
    /// Saves this goalkeeper made in this match, on his card beside his goals.
    /// </summary>
    public int MatchSaves { get; init; }

    /// <summary>
    /// The chance this player would convert a penalty right now, between 0 and 1. It is
    /// filled only on the candidates of a penalty waiting for a taker, and it is the same
    /// number the engine rolls, so the manager compares takers instead of guessing.
    /// </summary>
    public double? PenaltyChance { get; init; }
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
    /// The share of the ball each side has actually had, as a percentage. It is a fact
    /// about the match and not an estimate of it: the engine adds up the seconds each side
    /// was in possession, and the bar under the scoreboard reads off these two numbers.
    /// </summary>
    public int HomePossessionPercent { get; init; }

    public int AwayPossessionPercent { get; init; }

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

    /// <summary>
    /// The shape each side is playing right now, as three numbers on a team sheet. It
    /// moves during the match: a substitution that changes the balance of a side changes
    /// the shape it is playing, which is the point of reading the shape off the eleven
    /// instead of off a setting.
    /// </summary>
    public string FormationHome { get; init; } = string.Empty;

    public string FormationAway { get; init; } = string.Empty;
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

    /// <summary>
    /// Saves by this side's goalkeeper. What the defence did is a statistic of a match, and
    /// a keeper who made six of them has done the most valuable thing on the pitch.
    /// </summary>
    public int Saves { get; init; }
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
/// The beats of one match of the round, published to everyone following the round, and
/// carrying the match they came from.
///
/// A scoreboard that can only say 1 x 0 does not tell a manager whether the club down the
/// street is winning or hanging on, and a round the manager is not in is still a round he
/// is watching. The events are the same ones the match's own followers get — the same
/// words, from the same match — and the match id is what keeps the four logs apart on a
/// client that is following all of them at once.
/// </summary>
public class MatchdayEventDto
{
    public Guid RoundId { get; init; }
    public Guid MatchId { get; init; }
    public IReadOnlyList<MatchEngineEventDto> Events { get; init; } = Array.Empty<MatchEngineEventDto>();
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
    public int HomeOwnGoals { get; init; }
    public int AwayOwnGoals { get; init; }
    public int HomeYellowCards { get; init; }
    public int AwayYellowCards { get; init; }
    public int HomeRedCards { get; init; }
    public int AwayRedCards { get; init; }
    public int HomeInjuries { get; init; }
    public int AwayInjuries { get; init; }
}

/// <summary>
/// One shape a manager can order his eleven to be built in, as the lineup screen needs
/// it: the code he sends back, the name he reads, and the three bands of the eleven.
/// </summary>
public class TacticDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Defenders { get; init; }
    public int Midfielders { get; init; }
    public int Attackers { get; init; }
}

/// <summary>
/// A round told back, for the league screen: what was played and how each match went.
/// </summary>
public class MatchdayReportDto
{
    public Guid RoundId { get; init; }
    public int RoundNumber { get; init; }
    public List<MatchdayReportEntryDto> Entries { get; init; } = new();
}

public class MatchdayReportEntryDto
{
    public Guid FixtureId { get; init; }
    public Guid MatchId { get; init; }
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string HomeShortName { get; init; } = string.Empty;
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public string AwayShortName { get; init; } = string.Empty;
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public string Summary { get; init; } = string.Empty;
}
