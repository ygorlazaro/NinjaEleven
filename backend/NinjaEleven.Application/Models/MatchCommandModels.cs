using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

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
    public required int HomeSaves { get; init; }
    public required int AwaySaves { get; init; }
    public required int HomePossession { get; init; }
    public required int AwayPossession { get; init; }
    public required int SubstitutionsUsedHome { get; init; }
    public required int SubstitutionsUsedAway { get; init; }
    public required bool PenaltyAwaitingSelection { get; init; }

    /// <summary>
    /// Which side has the ball right now, and which of its players. It is not the same
    /// question as the share of the ball: a side can be behind on possession and still be
    /// the one holding it, and the eleven cards under the scoreboard highlight whoever it
    /// is.
    /// </summary>
    public required int PossessionTeam { get; init; }

    public Guid? PossessionPlayerId { get; init; }

    /// <summary>
    /// A player who cannot carry on and whose replacement the manager has to name, when
    /// there is one. Nothing is asked for the other club: the engine covers that absence
    /// itself, so a state with an injury in it is always a decision of the manager's.
    /// </summary>
    public MatchInjuryView Injury { get; init; } = new();

    /// <summary>
    /// The shape each side is playing, as three numbers on a team sheet.
    /// </summary>
    public required string FormationHome { get; init; }

    public required string FormationAway { get; init; }

    /// <summary>
    /// Attendance at kick-off.
    /// </summary>
    public int Attendance { get; init; }

    /// <summary>
    /// Gate revenue in limos.
    /// </summary>
    public decimal GateRevenue { get; init; }

    public PenaltyTakerOptions Penalty { get; init; } = new();

    /// <summary>
    /// The shootout, when the match has gone to one, and nothing at all when it has not.
    /// </summary>
    public ShootoutView? Shootout { get; init; }

    /// <summary>
    /// The club a manager is watching, when the match was started by one. A client sends
    /// it back with the commands it issues, so it never has to guess which side of the
    /// scoreboard is his.
    /// </summary>
    public Guid? UserTeamId { get; init; }
}

/// <summary>
/// The match a club is playing right now, as a badge in a navigation column can show it.
/// Null is the answer "not at the moment", and it is the answer most of a season's hours.
/// </summary>
public class LiveMatchSummary
{
    public required Guid MatchId { get; init; }
    public required Guid RoundId { get; init; }
    public required Guid HomeTeamId { get; init; }
    public required string HomeTeamName { get; init; }
    public required string HomeShortName { get; init; }
    public required string HomePrimaryColor { get; init; }
    public required string HomeSecondaryColor { get; init; }
    public required Guid AwayTeamId { get; init; }
    public required string AwayTeamName { get; init; }
    public required string AwayShortName { get; init; }
    public required string AwayPrimaryColor { get; init; }
    public required string AwaySecondaryColor { get; init; }
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }
    public required int Minute { get; init; }

    /// <summary>
    /// Whether the club asked about is the home one. A badge shows the opponent first and
    /// this is what says which of the two is the other one.
    /// </summary>
    public required bool IsHome { get; init; }

    /// <summary>
    /// Whether the match is standing at the interval, where it is not late — it is stopped.
    /// A badge that said "45'" while the clock waits for a manager would read as a game
    /// dragging on, and the manager would come back to a half he had already decided.
    /// </summary>
    public required bool AtHalfTime { get; init; }
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

    /// <summary>
    /// The two colours of each club, so a scoreboard can draw a shield next to the name.
    ///
    /// A matchday is a column of small rows and the name is what identifies a club in it;
    /// the shield is what lets a manager recognise one before he has finished reading it,
    /// and it is the same shield he sees on the club screen and beside his own eleven. They
    /// travel with the score rather than being asked for per row, because a round is
    /// published once a tick to every screen following it and a second read of the clubs
    /// would be a read per row of a list the backend already holds whole.
    /// </summary>
    public required string HomePrimaryColor { get; init; }
    public required string HomeSecondaryColor { get; init; }
    public required string AwayPrimaryColor { get; init; }
    public required string AwaySecondaryColor { get; init; }
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }
    public required int Minute { get; init; }
    public required string Half { get; init; }
    public required string Status { get; init; }
    public required bool IsFinished { get; init; }

    /// <summary>
    /// What the match produced besides goals: who made a mess of it, who got hurt, and who
    /// was booked. A matchday shows all of it, because "1 x 0" does not tell a manager
    /// whether the side in front of him won a game or survived one.
    /// </summary>
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
/// A shootout as a manager reads it from the stand: the coin, the two orders, the kicks
/// taken so far and whose turn it is.
///
/// It is present in a state only while the match is at the spot, and it carries the men the
/// manager may still name — which is the only way a screen can offer him a choice rather than
/// read one back to him. The takers are ids because a name is a profile and a profile is a
/// screen's business, not the match's.
/// </summary>
public class ShootoutView
{
    public required Guid HomeTeamId { get; init; }
    public required Guid AwayTeamId { get; init; }

    /// <summary>Which club the coin sent to the spot first.</summary>
    public required bool HomeTakesFirst { get; init; }

    /// <summary>Whose kick it is, as the club, and null when the shootout is over.</summary>
    public Guid? NextTeamId { get; init; }

    /// <summary>
    /// The man who walks to the spot next, and null when it is the other side's turn or the
    /// shootout is over.
    /// </summary>
    public Guid? NextTakerId { get; init; }

    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }
    public required int HomeKicksTaken { get; init; }
    public required int AwayKicksTaken { get; init; }

    /// <summary>Whether the five kicks each side is given have both been taken.</summary>
    public required bool IsSuddenDeath { get; init; }

    /// <summary>Whether the shootout has produced a winner.</summary>
    public required bool IsComplete { get; init; }

    public Guid? WinnerTeamId { get; init; }

    /// <summary>
    /// True while the match is standing at ninety minutes waiting for the manager to name
    /// his order. The clock is held for exactly as long as it takes him to decide, the same
    /// way it is held for the taker of a penalty.
    /// </summary>
    public required bool AwaitingOrder { get; init; }

    /// <summary>
    /// The men the manager's club may still name, in the order the engine would read them:
    /// the best taker first. It is empty once he has named his five.
    ///
    /// They are the snapshots the match already holds rather than bare ids, because a screen
    /// that has to open a profile for a man to show his name should not have to ask for him
    /// twice — and because the chance each of them has from twelve yards is the engine's
    /// number, so the screen reads it instead of working it out.
    /// </summary>
    public required IReadOnlyList<Domain.Matches.MatchPlayerSnapshot> Candidates { get; init; }

    /// <summary>
    /// The keeper the candidates would be shooting at, so each of them is presented with
    /// the chance he would really have against him rather than a general one.
    /// </summary>
    public Domain.Matches.MatchPlayerSnapshot? DefendingGoalkeeper { get; init; }

    /// <summary>
    /// The order each side named, in the order it will take in. Both are sent, because a
    /// manager watching a shootout gets to see the other club's five and not only his own —
    /// and the one that is not his is the one the engine filled in.
    /// </summary>
    public required IReadOnlyList<Guid> HomeTakers { get; init; }

    public required IReadOnlyList<Guid> AwayTakers { get; init; }

    /// <summary>Every kick taken, in the order they were taken.</summary>
    public required IReadOnlyList<ShootoutKickView> Kicks { get; init; }
}

/// <summary>One kick of a shootout, as the feed and the shootout panel both show it.</summary>
public class ShootoutKickView
{
    public required Guid TeamId { get; init; }
    public required Guid TakerId { get; init; }
    public required bool Scored { get; init; }
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

    /// <summary>
    /// The goalkeeper the candidates are shooting at, so each one is presented with the
    /// chance he would really have against him.
    /// </summary>
    public MatchPlayerSnapshot? DefendingGoalkeeper { get; init; }
}

/// <summary>
/// The man who is hurt and the club he plays for, so a substitution screen can open already
/// pointed at him instead of asking the manager to find him in the eleven. It is asked
/// because the clock is held: nothing else about the match moves while it is out.
/// </summary>
public class MatchInjuryView
{
    public bool AwaitingSubstitution { get; init; }
    public Guid? PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public int? Team { get; init; }
    public Domain.Common.Injury Severity { get; init; }
}
