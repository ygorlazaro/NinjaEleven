using NinjaEleven.Domain.Competitions;
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
    public double Stars { get; init; }
    public StadiumDto? Stadium { get; init; }
}

/// <summary>
/// A club's ground, as a card about it carries the ground.
/// </summary>
/// <remarks>
/// The name is on the wire because a ground has one and the world keeps it: a club's stadium is
/// stored as a name of its own rather than composed from the club's name on the way to a
/// screen, precisely so it can be something else later. It was left out of this DTO while the
/// column already existed, which is how a screen came to have to invent a ground's name — a
/// stadium is the one thing a manager reads the name of before he reads anything else about
/// the match.
/// </remarks>
public class StadiumDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public decimal TicketPrice { get; init; }
}

public class CompetitionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CompetitionType Type { get; init; }
}

/// <summary>
/// One edition of a competition inside one season.
///
/// This is what a client addresses, not the competition. "Campeonato Brasileiro" runs three
/// times in a season, once per tier, and the edition is the thing a table, a fixture and a
/// trophy all belong to. A list of competitions has nowhere to say which of the three a club
/// is in, and a client that guessed was guessing.
/// </summary>
public class CompetitionEditionDto
{
    /// <summary>The edition's id: what a table, a fixture and a trophy are addressed by.</summary>
    public Guid Id { get; init; }

    public Guid CompetitionId { get; init; }
    public Guid SeasonId { get; init; }
    public Guid? DivisionId { get; init; }

    /// <summary>1 is the top of the pyramid. Null for a cup and a Supercup.</summary>
    public int? Tier { get; init; }

    /// <summary>The competition's own name.</summary>
    public string CompetitionName { get; init; } = string.Empty;

    public CompetitionType Type { get; init; }

    /// <summary>"1ª Divisão" for a division, or the competition's name for a knockout.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Whether this edition is one tier's table rather than a knockout.</summary>
    public bool IsDivision { get; init; }
}

public class SeasonDto
{
    public Guid Id { get; init; }

    /// <summary>The season's number, counted from one. Its identity.</summary>
    public int Number { get; init; }

    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public SeasonStatus Status { get; init; }
}

/// <summary>
/// What closing a season did. It is said back rather than shown as a message because a
/// manager who closes a season wants to know which season is now the current one, and the
/// season that follows is the answer.
/// </summary>
public class SeasonCloseDto
{
    /// <summary>The season that was closed.</summary>
    public Guid SeasonId { get; init; }

    /// <summary>The season that was opened after it, or null when none was.</summary>
    public Guid? NextSeasonId { get; init; }

    /// <summary>How many clubs were paid a championship purse.</summary>
    public int PrizesPaid { get; init; }

    /// <summary>The champion of each division, top tier first.</summary>
    public IReadOnlyList<Guid> Champions { get; init; } = Array.Empty<Guid>();
}

public class RoundDto
{
    public Guid Id { get; init; }
    public Guid CompetitionSeasonId { get; init; }

    /// <summary>Counted from one inside its own competition.</summary>
    public int Number { get; init; }

    /// <summary>The matchday this window belongs to. Null only while it is being built.</summary>
    public Guid? MatchDayId { get; init; }

    /// <summary>Which window of the matchday this is: the championship is 1, the cup is 2.</summary>
    public int Window { get; init; }

    /// <summary>When the last fixture of the window was finished. Null while it is still open.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

public class MatchDayDto
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public int Number { get; init; }
    public DateOnly Date { get; init; }
}

/// <summary>
/// A season's calendar: the days, and the windows of football scheduled on them.
///
/// A season is asked for as a whole because a screen that shows the fixtures wants the
/// matchday the fixture is on, and a screen that shows the table wants to say which matchday
/// it is. Deriving either from the other on the client means the two screens can disagree
/// about what day a match is played on.
/// </summary>
public class SeasonCalendarDto
{
    public Guid SeasonId { get; init; }
    public string SeasonName { get; init; } = string.Empty;
    public int MatchDayCount { get; init; }
    public IReadOnlyList<MatchDayDto> MatchDays { get; init; } = Array.Empty<MatchDayDto>();
    public IReadOnlyList<RoundDto> Windows { get; init; } = Array.Empty<RoundDto>();
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
    public int Attendance { get; init; }
    public decimal GateRevenue { get; init; }
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

    /// <summary>
    /// Where the club stands, counted from one. The backend decides it and the screen shows
    /// it: a table that is sorted again in the browser is a table that can disagree with the
    /// promotion rules.
    /// </summary>
    public int Position { get; init; }

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
    public double Stars { get; init; }

    /// <summary>
    /// Which band of the table this line is in. The backend decides it from its own promotion
    /// and relegation rules, so a screen that colours the bands colours the same ones the
    /// season's end moves clubs by — never its own arithmetic.
    /// </summary>
    public TableZone Zone { get; init; }
}

/// <summary>
/// A table as a manager reads it: where the clubs are, and where they would be if the games
/// still being played went a certain way.
///
/// Both tables come from the same rules and are ordered by the same tiebreakers, so a club
/// that is sixth officially can be third live without either screen disagreeing with itself.
/// A client is handed both rather than asked to derive one: deriving the live table in the
/// browser means every screen growing its own copy of the promotion arithmetic.
/// </summary>
public class CompetitionStandingsDto
{
    public Guid CompetitionSeasonId { get; init; }
    public Guid? SeasonId { get; init; }
    public Guid? DivisionId { get; init; }

    /// <summary>1 is the top of the pyramid. Null for a cup and a Supercup.</summary>
    public int? Tier { get; init; }

    public string CompetitionName { get; init; } = string.Empty;

    /// <summary>The table of the games that are over. The official one.</summary>
    public IReadOnlyList<StandingDto> Official { get; init; } = Array.Empty<StandingDto>();

    /// <summary>The table counting the games in progress at their current score.</summary>
    public IReadOnlyList<StandingDto> Projected { get; init; } = Array.Empty<StandingDto>();

    /// <summary>Whether any game of this competition is being played right now.</summary>
    public bool HasLiveMatches { get; init; }
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

/// <summary>
/// One line of a club's scorers table: a man of that club, his goals in a season, and whether
/// he is still there.
/// </summary>
/// <remarks>
/// `isStillAtClub` is the field that makes the table a page about a club's history rather than
/// a list of the men currently under contract. An own goal is carried apart from the goals
/// because a goal conceded into his own net is not his scoring, and the appearances are the
/// two counts a manager reads: games started, and games entered off the bench.
/// </remarks>
public class ClubScorerDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public int Age { get; init; }
    public int Position { get; init; }
    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Started { get; init; }
    public int CameOn { get; init; }

    /// <summary>Goals per appearance, or null when the player never appeared.</summary>
    public double? GoalsPerAppearance { get; init; }

    public bool IsStillAtClub { get; init; }
}

public class LeagueSetupResultDto
{
    public Guid CompetitionSeasonId { get; init; }
    public Guid CompetitionId { get; init; }
    public Guid SeasonId { get; init; }
    public IReadOnlyList<RoundDto> Rounds { get; init; } = Array.Empty<RoundDto>();
    public IReadOnlyList<FixtureDto> Fixtures { get; init; } = Array.Empty<FixtureDto>();
}

/// <summary>
/// One finished match of a club, as its form guide shows it: who it was against, where,
/// and the score from the club's side.
///
/// The two scores are ordered for the club rather than for the fixture, so a screen does
/// not have to know which end it was on to say "2 x 1". The result itself is a reading of
/// these two numbers and is not stored: a result kept beside a score is a second answer to
/// the same question, and the two disagree the first time a match is corrected.
/// </summary>
public class TeamMatchRecordDto
{
    public Guid MatchId { get; init; }
    public string OpponentName { get; init; } = string.Empty;

    /// <summary>So the opponent's name is a door to that club, as every name in the game is.</summary>
    public Guid OpponentTeamId { get; init; }

    public bool IsHome { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int RoundNumber { get; init; }
    public DateTimeOffset PlayedAt { get; init; }

    // Head-to-head specific fields
    public string? SeasonName { get; init; }
    public string? CompetitionName { get; init; }
    public string? PhaseName { get; init; }
    public int? Attendance { get; init; }
}
