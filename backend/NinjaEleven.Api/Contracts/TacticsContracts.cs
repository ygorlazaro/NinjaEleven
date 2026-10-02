using System.ComponentModel.DataAnnotations;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// The board a manager reads before his club's next match: the fixture, the opponent, the
/// squad, and the order he has left on it.
///
/// <para>
/// It is one call on purpose. The screen answers four questions — who am I playing, who is
/// available, what did I say last time and how have we done against them — and a screen that
/// asked four times would be able to show a squad that disagrees with the kick-off's own.
/// </para>
/// </summary>
public class TacticsBoardDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public Guid SeasonId { get; set; }

    /// <summary>The next fixture, or null when the season has nothing left to play.</summary>
    public TacticsNextFixtureDto? Next { get; set; }

    public TacticsOpponentDto? Opponent { get; set; }
    public TacticsHeadToHeadDto? HeadToHead { get; set; }

    /// <summary>The last few finished matches, newest first.</summary>
    public List<TacticsRecentMatchDto> RecentForm { get; set; } = [];

    public List<TacticsSquadRowDto> Squad { get; set; } = [];

    /// <summary>
    /// The order as it was left, or null for a manager who has never set one. Null is an
    /// honest answer rather than an empty one: the kick-off then falls back to the shape this
    /// club last went out in, and a board that showed a blank plan would read as "you have no
    /// players" instead of "you have not said".
    /// </summary>
    public TacticsPlanDto? Plan { get; set; }
}

/// <summary>The fixture the plan on the board is about, and the window it kicks off in.</summary>
public class TacticsNextFixtureDto
{
    public Guid FixtureId { get; set; }
    public int RoundNumber { get; set; }
    public int? MatchDayNumber { get; set; }
    public string CompetitionName { get; set; } = string.Empty;

    /// <summary>The competition's type, so a screen can say "Copa" rather than the season name.</summary>
    public string CompetitionType { get; set; } = string.Empty;

    /// <summary>
    /// True when the window this fixture belongs to has not kicked off yet. The board says so
    /// because it is the difference between an order that will be used and one that has been
    /// written too late.
    /// </summary>
    public bool WaveOpen { get; set; }
}

public class TacticsOpponentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Rating { get; set; }

    /// <summary>Whether this club is the one at home, which is which way round the two names go.</summary>
    public bool IsHome { get; set; }
}

public class TacticsHeadToHeadDto
{
    public int Played { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
}

public class TacticsRecentMatchDto
{
    public Guid MatchId { get; set; }
    public Guid OpponentTeamId { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int RoundNumber { get; set; }
    public string? CompetitionName { get; set; }
    public string? PhaseName { get; set; }
    public DateTimeOffset PlayedAt { get; set; }
}

/// <summary>One man of the squad, as far as a board needs him.</summary>
public class TacticsSquadRowDto
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Position { get; set; } = string.Empty;
    public double Stars { get; set; }
    public int Energy { get; set; }

    /// <summary>
    /// Whether he can be named in an order today. A suspended or injured man is still on the
    /// board — a manager needs to see who is out — but he is refused by name rather than
    /// quietly dropped, so a plan never loses two players without saying so.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// How many matches of his club the suspension still keeps him out of, and how many the
    /// injury does. A board row is greyed when a man is out, and a greyed row with no reason
    /// is a manager opening the club's page to find out what he could already have been told
    /// here.
    /// </summary>
    public int SuspensionMatches { get; set; }

    public int InjuryMatchesRemaining { get; set; }

    /// <summary>
    /// The eight attributes on the canonical 1..100 scale, in the order every other table in
    /// the game reads them: speed, finishing, dribbling, heading, strength, goalkeeper,
    /// reflexes, stamina.
    ///
    /// <para>
    /// They are a positional list and not a bag of named values on purpose. The label belongs
    /// to the screen — the squad table says "Cab" and the training sheet says "Cab", and a
    /// dictionary here would be an invitation to invent a third label for the same number. The
    /// order is fixed and shared, which is what makes one list readable by both screens
    /// without either of them having to be told the order again.
    /// </para>
    /// </summary>
    public List<int> Attributes { get; set; } = [];
}

/// <summary>The standing order, as it was left.</summary>
public class TacticsPlanDto
{
    public string TacticCode { get; set; } = string.Empty;
    public List<Guid> StarterIds { get; set; } = [];
    public List<Guid> BenchIds { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// The order being written: the shape and the men to play it in.
///
/// <para>
/// It is validated by the backend rather than by the screen because the screen is not the
/// thing that kicks off. A plan that was only checked when the manager pressed the button
/// would be a plan that could be wrong by the time the whistle went — a man suspended on
/// Tuesday, an opponent already named on Wednesday.
/// </para>
/// </summary>
public class SaveTacticsPlanRequest
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

    /// <summary>
    /// One of the catalogue's codes. Empty means "the shape this club last went out in",
    /// which is the standing order of a manager who has not yet picked a shape.
    /// </summary>
    public string? TacticCode { get; set; }

    [Required]
    public List<Guid> StarterIds { get; set; } = [];

    public List<Guid> BenchIds { get; set; } = [];
}