using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;

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

    /// <summary>
    /// Whether a goal came from the spot, so a scoreline can mark one without reading the
    /// wording of the event. Decided by the engine when it emitted the goal.
    /// </summary>
    public bool FromPenalty { get; init; }

    /// <summary>
    /// What the player on this event is called, when the engine had him to hand. A scoreline
    /// names a scorer from the goal itself rather than out of a squad that may since have
    /// changed around him.
    /// </summary>
    public string? PlayerName { get; init; }
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

    /// <summary>
    /// Which of the home club's two shirts this match was played in.
    ///
    /// It is on the match and not on the club because "which shirt" is a fact about this
    /// fixture: the same club plays its first shirt at home and its second when the two colours
    /// on the pitch would be impossible to tell apart, and a screen that asked the club which
    /// shirt it is wearing would be asking a question with two answers.
    /// </summary>
    public KitSide HomeKitSide { get; init; }

    /// <summary>Which of the visiting club's two shirts this match was played in.</summary>
    public KitSide AwayKitSide { get; init; }

    /// <summary>
    /// The company on the home shirt, as stamped at the kick-off, or null when the home club had
    /// no live deal then. It is null rather than an empty company because a club without a
    /// sponsor is the ordinary case and deserves no mark at all.
    /// </summary>
    public SponsorMarkDto? HomeSponsor { get; init; }

    /// <summary>The company on the visiting shirt, stamped at the same moment as the other one.</summary>
    public SponsorMarkDto? AwaySponsor { get; init; }
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

    /// <summary>
    /// The number on his back, and null for a man who has not been given one. It travels with
    /// the snapshot rather than being asked of the club mid-match, so the shirt a card draws is
    /// the shirt he was put on in.
    /// </summary>
    public int? ShirtNumber { get; init; }

    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Stamina { get; init; }
    public int Energy { get; init; }
    public int MatchYellowCards { get; init; }
    public bool RedCard { get; init; }
    public bool EmergencyGK { get; init; }
    public bool InjuredOff { get; init; }

    /// <summary>
    /// How bad the knock of this match was, and not a sentence about it. A player who is
    /// carrying a light injury is still on the pitch and is still playing, so <see cref="InjuredOff"/>
    /// says nothing about him; the severity is what a screen draws beside his name so the
    /// manager can see who is playing hurt rather than reading it out of the feed.
    /// </summary>
    public Domain.Common.Injury Injury { get; init; }

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

public double Stars { get; init; }

    /// <summary>
    /// How many minutes of this match he has actually been on the pitch, as the match stands
    /// right now.
    /// </summary>
    /// <para>
    /// Read from the engine's own stamps rather than from the clock, because the two are not
    /// the same thing: a man who came on at the eightieth has twenty minutes at the
    /// eightieth-five whatever the scoreboard says, and a screen that printed the difference
    /// would be telling a manager a substitute had played a full match.
    /// </para>
    /// </summary>
    public int MinutesPlayed { get; init; }

    /// <summary>
    /// What the match has been worth to him so far, out of ten, and null while he has not
    /// played long enough of it to have one.
    /// </summary>
    /// <para>
    /// The band travels with it so that no screen has to work out where the lines are. A
    /// client that decided for itself where "good" starts is a client that will decide it
    /// differently from the next one, and the eleven under the scoreboard is read by a
    /// manager in the middle of a match rather than studied.
    /// </para>
    /// </summary>
    public double? Rating { get; init; }

    public MatchRatingBand RatingBand { get; init; } = MatchRatingBand.Unrated;
}

/// <summary>
/// Where a match is being played and what it is: the season and the day, the competition and
/// the phase of it, the ground, and the leg before this one when there was one.
///
/// It is what a manager reads above a scoreboard, and every word of it is a fact about the
/// match rather than a label on the screen: a cup return leg carries the aggregate's other
/// half with it, because "1 x 0" means something completely different in a tie that is
/// already 2 x 1 across.
/// </summary>
public class MatchContextDto
{
    public string SeasonName { get; init; } = string.Empty;
    public int MatchDayNumber { get; init; }
    public string CompetitionName { get; init; } = string.Empty;
    public CompetitionType CompetitionType { get; init; }
    public string EditionName { get; init; } = string.Empty;
    public string PhaseName { get; init; } = string.Empty;
    public string? LegLabel { get; init; }
    public string StadiumName { get; init; } = string.Empty;
    public int StadiumCapacity { get; init; }
    public CupLegResultDto? FirstLeg { get; init; }
}

/// <summary>One leg of a cup tie, as a screen shows it under a score.</summary>
public class CupLegResultDto
{
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public int HomeGoals { get; init; }
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public int AwayGoals { get; init; }
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
    /// When the window for naming a taker closes, or null when none is open.
    ///
    /// <para>
    /// A moment and not a number of seconds, because the count is the backend's: the screen
    /// draws what is left of it and never starts a clock of its own over a decision the
    /// engine has already decided how long to wait for.
    /// </para>
    /// </summary>
    public DateTimeOffset? PenaltyEndsAt { get; init; }

    /// <summary>
    /// When the interval closes by itself, or null while the match is not at one.
    /// </summary>
    public DateTimeOffset? HalfTimeEndsAt { get; init; }

    /// <summary>
    /// The player who cannot continue and whose replacement the manager has to name. The
    /// window is open and the match goes on around it, so this is a question the manager may
    /// answer late rather than one the match is stopped for.
    /// </summary>
    public MatchInjuryDto Injury { get; init; } = new();

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

    /// <summary>
    /// Attendance at kick-off.
    /// </summary>
    public int Attendance { get; init; }

    /// <summary>
    /// Gate revenue in limos.
    /// </summary>
    public decimal GateRevenue { get; init; }

    /// <summary>
    /// The shootout, when the match is at the spot, and nothing at all when it is not.
    /// </summary>
    public ShootoutDto? Shootout { get; init; }

    /// <summary>
    /// What the match is worth to each of the men on the pitch right now. It travels on the
    /// tick rather than on a second call, because the number on the eleven under the
    /// scoreboard is the one a manager reads while the match is still being played, and a
    /// number fetched separately is either a poll or a reading of a match that has moved on.
    /// </summary>
    public IReadOnlyList<LiveRatingDto> LiveRatings { get; init; } = Array.Empty<LiveRatingDto>();
}

/// <summary>
/// One man's card as the match stands at this tick: the note, the band it falls in, and the
/// minutes behind it. Three numbers rather than a whole player, because the client already has
/// the player and this is the part that moves.
/// </summary>
public class LiveRatingDto
{
    public Guid PlayerId { get; init; }
    public double? Rating { get; init; }
    public MatchRatingBand RatingBand { get; init; } = MatchRatingBand.Unrated;
    public int MinutesPlayed { get; init; }
}

/// <summary>
/// A shootout as a manager reads it: the coin, the two orders, the kicks and whose turn it
/// is. Null on every match that did not go to penalties.
/// </summary>
public class ShootoutDto
{
    public Guid HomeTeamId { get; init; }
    public Guid AwayTeamId { get; init; }
    public bool HomeTakesFirst { get; init; }

    /// <summary>Whose kick it is, or null when the shootout is over.</summary>
    public Guid? NextTeamId { get; init; }

    /// <summary>Who walks to the spot next, or null when it is not the manager's turn to send one.</summary>
    public Guid? NextTakerId { get; init; }

    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public int HomeKicksTaken { get; init; }
    public int AwayKicksTaken { get; init; }
    public bool IsSuddenDeath { get; init; }
    public bool IsComplete { get; init; }
    public Guid? WinnerTeamId { get; init; }

    /// <summary>Whether the match is standing at ninety minutes waiting for the manager.</summary>
    public bool AwaitingOrder { get; init; }

    /// <summary>
    /// The men the manager's club may still name, as the players the match knows them by, so
    /// a screen can show a name on a face without a second call for it.
    /// </summary>
    public IReadOnlyList<MatchPlayerDto> Candidates { get; init; } = Array.Empty<MatchPlayerDto>();

    /// <summary>
    /// The order each side named. Both travel, because a manager watching a shootout sees
    /// the other club's five as well as his own.
    /// </summary>
    public IReadOnlyList<Guid> HomeTakers { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<Guid> AwayTakers { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<ShootoutKickDto> Kicks { get; init; } = Array.Empty<ShootoutKickDto>();
}

/// <summary>One kick of a shootout.</summary>
public class ShootoutKickDto
{
    public Guid TeamId { get; init; }
    public Guid TakerId { get; init; }
    public bool Scored { get; init; }
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
/// <summary>
/// The beats of one match, with the match they belong to.
///
/// <para>
/// A connection follows a match in full and a round at the same time, and it can move from one
/// match to another without losing the connection — a manager who opens a second game from the
/// matchday panel is on the same socket. So an event stream that carried no owner would be a
/// stream a client could not put anywhere: the two matches arrive on one connection, and
/// whichever arrived last would be the match on the screen. This is the same envelope
/// <see cref="MatchdayEventDto"/> uses for the round, and for the same reason.
/// </para>
/// </summary>
public class MatchStreamDto
{
    public Guid MatchId { get; init; }
    public IReadOnlyList<MatchEngineEventDto> Events { get; init; } = Array.Empty<MatchEngineEventDto>();
}

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
/// <summary>
/// The match a club is playing right now, as a navigation badge shows it. Null is "not at
/// the moment", and it is the answer most of a season's hours.
/// </summary>
public class LiveMatchDto
{
    public Guid MatchId { get; init; }
    public Guid RoundId { get; init; }
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string HomeShortName { get; init; } = string.Empty;
    public string HomePrimaryColor { get; init; } = string.Empty;
    public string HomeSecondaryColor { get; init; } = string.Empty;
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public string AwayShortName { get; init; } = string.Empty;
    public string AwayPrimaryColor { get; init; } = string.Empty;
    public string AwaySecondaryColor { get; init; } = string.Empty;
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public int Minute { get; init; }
    public bool IsHome { get; init; }
    public bool AtHalfTime { get; init; }
}

public class MatchScoreDto
{
    public Guid RoundId { get; init; }
    public Guid MatchId { get; init; }
    public Guid FixtureId { get; init; }
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string HomeShortName { get; init; } = string.Empty;

    /// <summary>
    /// The two colours of each club, so a matchday can draw a shield beside the name rather
    /// than leave a manager to recognise a club by reading it. They travel with the score
    /// because a round is published once a tick to every screen following it, and reading
    /// the clubs again per row would be a read per row of a list the backend holds whole.
    /// </summary>
    public string HomePrimaryColor { get; init; } = string.Empty;
    public string HomeSecondaryColor { get; init; } = string.Empty;
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public string AwayShortName { get; init; } = string.Empty;
    public string AwayPrimaryColor { get; init; } = string.Empty;
    public string AwaySecondaryColor { get; init; } = string.Empty;
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

/// <summary>
/// The eleven plus bench the staff would pick for a club and shape.
/// </summary>
public class SquadSuggestionDto
{
    public IReadOnlyList<Guid> StarterIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> BenchIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
/// A man who is hurt, his side, and how bad it was. The severity travels with it rather
/// than being read out of the feed's prose, because a screen that has to parse a sentence
/// to know whether to put a bandage next to a name is a screen that gets it wrong the day
/// somebody rewords the sentence.
/// </summary>
public class MatchInjuryDto
{
    public bool AwaitingSubstitution { get; init; }
    public Guid? PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public int? Team { get; init; }

    /// <summary>
    /// How bad it is, and <see cref="Domain.Common.Injury.None"/> when there is nothing to
    /// report. A severity that defaulted to the worst case would make every healthy match
    /// read as a grave injury to a screen that asked before it checked.
    /// </summary>
    public Domain.Common.Injury Severity { get; init; }
}
