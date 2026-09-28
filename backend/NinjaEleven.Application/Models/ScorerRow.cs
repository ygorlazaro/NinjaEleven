using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Models;

/// <summary>
/// One line of the top scorers table.
///
/// The goals come from the players' match lines and not from a counter kept alongside them.
/// A counter and the sum of the history are two different numbers, and a screen that showed
/// the counter in one place and the sum in another would be a screen with two answers to
/// "how many has he scored".
/// </summary>
public class ScorerRow
{
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }

    /// <summary>
    /// The club's colours. A scorer is a name in a column and a club is what the column is
    /// about, so the club is drawn beside the name — and a shield is drawn in two colours.
    /// </summary>
    public string? TeamPrimaryColor { get; init; }
    public string? TeamSecondaryColor { get; init; }
    public int Age { get; init; }
    public int Goals { get; init; }

    /// <summary>
    /// Games he played in the competition: started plus came on. It is on the row because the
    /// order of the table is decided by it — fewer games for the same goals is a better season —
    /// and a table that could not show it would be asking a manager to trust an order it could
    /// not explain.
    /// </summary>
    public int Appearances { get; init; }

    /// <summary>Yellow cards, as the order weighs them: one point each.</summary>
    public int YellowCards { get; init; }

    /// <summary>Red cards, as the order weighs them: three points each.</summary>
    public int RedCards { get; init; }

    /// <summary>
    /// The cards already weighted — a yellow is one, a red is three. Carried so that a screen
    /// never adds the two columns up on its own, and so a manager can see the number the order
    /// was settled on rather than having to work it out.
    /// </summary>
    public int CardPoints => YellowCards + RedCards * ScorerStanding.RedCardPoints;

    /// <summary>Where the player stands, counted from one. Decided by the backend.</summary>
    public int Position { get; set; }

    /// <summary>
    /// How many other players share this position, and zero when nobody does.
    /// </summary>
    /// <remarks>
    /// A scorers table is level more often than a league table is, and a table that showed two
    /// men as first and second when they are on the same number of goals would be inventing an
    /// order the game does not have. Where the chain could not separate them, they are both
    /// first, and this is what lets a screen say so.
    /// </remarks>
    public int TiedWith { get; set; }
}

/// <summary>
/// A window as a manager reads it: every match that was played in it, its scoreline, and the
/// account of it the match itself gave.
/// </summary>
public class MatchdayReport
{
    public Guid RoundId { get; set; }
    public int RoundNumber { get; set; }

    /// <summary>Which window of the matchday this was: the first, or the second.</summary>
    public int Window { get; set; }

    public List<MatchdayReportEntry> Entries { get; set; } = new();
}

/// <summary>
/// One match of the window, told in the words the match used. The summary is the goals'
/// own narration read back from the events, never a fresh account of a match that has
/// already been narrated once.
/// </summary>
public class MatchdayReportEntry
{
    public Guid FixtureId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string HomeShortName { get; set; } = string.Empty;
    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public string AwayShortName { get; set; } = string.Empty;
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public string Summary { get; set; } = string.Empty;

    /// <summary>What the tie stands at across both legs, when this is a cup match.</summary>
    public int? AggregateHomeGoals { get; set; }

    public int? AggregateAwayGoals { get; set; }
}

/// <summary>
/// One line of a club's scorers table: a man of that club, his goals in a season, and
/// whether he is still there.
/// </summary>
/// <remarks>
/// The last field is the one that turns a list of numbers into a list of men. A scorer who
/// left the club is a fact the club's history is made of, and a screen that only ever showed
/// the men still under contract would quietly delete the club's history every time a
/// transfer window opened — and a club's all-time scorers list is the one page that must never
/// lose a name. So the answer is carried on the line, decided by the backend, and the client
/// only chooses which lines it is showing.
/// </remarks>
public class ClubScorerRow
{
    public required Guid PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public int Age { get; init; }

    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Started { get; init; }
    public int CameOn { get; init; }

    /// <summary>
    /// Yellow and red cards, in two columns and weighed in a third.
    /// </summary>
    /// <remarks>
    /// They are here because the order of a club's own list of scorers is the same chain the
    /// artilharia prizes are settled on — goals, then fewest games, then fewest cards — so a
    /// striker ahead of a team-mate on the league page is ahead of him here for the same reason
    /// and by the same arithmetic. A yellow is worth one and a red three, and the weighing is
    /// done in one place.
    /// </remarks>
    public int YellowCards { get; init; }

    /// <inheritdoc cref="YellowCards"/>
    public int RedCards { get; init; }

    /// <summary>The two card columns weighed against each other, as the order weighs them.</summary>
    public int CardPoints =>
        YellowCards * ScorerStanding.YellowCardPoints + RedCards * ScorerStanding.RedCardPoints;

    /// <summary>
    /// Whether the player is still at the club: he has a membership that has not ended.
    /// Decided here and not by the client, which cannot know a contract from a shirt.
    /// </summary>
    public bool IsStillAtClub { get; init; }

    /// <summary>
    /// Games he played for the club: started plus came off the bench.
    /// </summary>
    /// <remarks>
    /// The two columns are kept as well, because a manager wants to know both — the split is
    /// the difference between a striker the staff trusted to start and one they used when
    /// nobody else was fit.
    /// </remarks>
    public int Appearances => Started + CameOn;

    /// <summary>Where the player stands in the club's table, counted from one.</summary>
    public int Position { get; set; }

    /// <summary>
    /// How many other players of the club share this position, and zero when nobody does.
    /// </summary>
    /// <remarks>
    /// The chain is goals, then games, then cards, then age, and it is the chain the division's
    /// artilharia is settled on too. Two men level on all four are both first here, and this is
    /// what lets a screen say so rather than inventing an order between them.
    /// </remarks>
    public int TiedWith { get; set; }

    /// <summary>
    /// Goals per appearance, or null when he never appeared.
    ///
    /// It is worked out here and not in the client because a number a screen invents is a
    /// number two screens may invent differently, and this one is the difference between a
    /// striker who scores every week and one who scored eight times in a season he was used
    /// twice. A player with no appearance has no rate and is given null rather than a zero:
    /// zero goals a game is a fact about a striker, and nothing at all is a fact about a man
    /// who never played.
    /// </summary>
    public double? GoalsPerAppearance { get; set; }
}
