namespace NinjaEleven.Application.Models;

/// <summary>
/// One season's cup, read as a bracket.
///
/// A knockout is the one thing in the game that is not a table, and it is read the way a manager
/// reads it: a column per round, and inside each tie the two clubs with the two legs they played
/// and the aggregate they finished on. The bracket here is a reading of the ties the cup has
/// already drawn — the round after a tie is decided does not exist yet, and a bracket that drew
/// it in advance would be showing a quarter-final between two clubs that are still playing.
///
/// The whole shape is decided by the backend for the same reason the table is: the aggregate is
/// a sum across two legs that swapped ends, and a client that added them by side would print a
/// different answer to the one the tie was decided on.
/// </summary>
public class CupBracketView
{
    public Guid CompetitionSeasonId { get; init; }

    public Guid SeasonId { get; init; }

    /// <summary>The competition's own name, without a tier: a cup has none.</summary>
    public string CompetitionName { get; init; } = string.Empty;

    /// <summary>The rounds that have been drawn, in the order they were played.</summary>
    public IReadOnlyList<CupBracketRound> Rounds { get; init; } = Array.Empty<CupBracketRound>();

    /// <summary>The club that won it, and null while the final is still to be played.</summary>
    public Guid? ChampionTeamId { get; init; }

    public string? ChampionTeamName { get; init; }

    /// <summary>The losing side of the final, which is a fact in its own right: the runner-up.</summary>
    public string? RunnerUpTeamName { get; init; }

    /// <summary>
    /// Whether the cup has been decided, and who won it — the one thing a podium is a podium of.
    /// </summary>
    public bool IsDecided => ChampionTeamId is not null;

    /// <summary>
    /// Every club the cup has drawn, in the order it ranks: the furthest a club got first, and
    /// inside a round by the championship's own tiebreakers.
    /// </summary>
    /// <remarks>
    /// It is sent with the bracket rather than behind a second route because it is a reading of
    /// the same ties and the same matches. A screen that asked the bracket and then asked for
    /// the ranking would read the cup twice, and the two answers would be two answers about how
    /// far each club had got — taken a minute apart, which is a long time in a semi-final.
    /// </remarks>
    public IReadOnlyList<CupRankingRow> Ranking { get; init; } = Array.Empty<CupRankingRow>();
}

/// <summary>
/// One line of the cup's ranking: how far a club got, and what it did to get there.
/// </summary>
/// <remarks>
/// The round is the primary key and the championship's tiebreakers are the rest, so the ranking
/// is a ladder of rounds with a table inside each step: two clubs knocked out in the same
/// quarter-final are ordered by the same chain that orders a league table, head-to-head and
/// cards included, because "who had the better campaign" is a question this game answers one way.
/// </remarks>
public class CupRankingRow
{
    /// <summary>Where the club ranks, counted from one.</summary>
    public int Position { get; init; }

    public Guid TeamId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string PrimaryColor { get; init; } = string.Empty;

    public string SecondaryColor { get; init; } = string.Empty;

    /// <summary>
    /// The tie-round the club reached, and the last one the cup plays for the club still in it.
    /// A cup in progress puts the clubs of the round being played on the same step, which is
    /// what they are: four clubs that have not lost are four clubs that have not been ranked
    /// against each other yet.
    /// </summary>
    public int RoundNumber { get; init; }

    /// <summary>The round in the game's words, because a ranking headed "5" is not a ranking.</summary>
    public string RoundName { get; init; } = string.Empty;

    public int Played { get; init; }

    public int Wins { get; init; }

    public int Draws { get; init; }

    public int Losses { get; init; }

    public int GoalsFor { get; init; }

    public int GoalsAgainst { get; init; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    /// <summary>
    /// The three points for a win and one for a draw, which is the first of the tiebreakers and
    /// not a cup table: the ranking sends it because the rule that decides the order uses it.
    /// </summary>
    public int Points { get; init; }

    /// <summary>What the club's run is paid, and null while it is still running.</summary>
    public decimal? Prize { get; init; }

    public bool IsChampion { get; init; }

    /// <summary>Lost the final, which is not the same as being knocked out in it.</summary>
    public bool IsRunnerUp { get; init; }
}

/// <summary>One round of a bracket: the sixteen, the quarter-finals, the final, whatever it is.</summary>
public class CupBracketRound
{
    /// <summary>Which round, counted from one.</summary>
    public int RoundNumber { get; init; }

    /// <summary>What the round is called, in the game's own words rather than as a number.</summary>
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<CupBracketTie> Ties { get; init; } = Array.Empty<CupBracketTie>();
}

/// <summary>
/// One tie: two clubs, two legs, the aggregate and, when there was one, the shootout.
/// </summary>
public class CupBracketTie
{
    public Guid TieId { get; init; }

    public int RoundNumber { get; init; }

    /// <summary>
    /// The two clubs, the tie's own home club first — the one that was at home in the first leg.
    /// Every number on a club is that club's: the goals it scored, the goals it conceded, and
    /// what it finished on. A bracket that printed "2 x 1" without saying which way round would
    /// be a bracket that has to be decoded.
    /// </summary>
    public IReadOnlyList<CupBracketClub> Clubs { get; init; } = Array.Empty<CupBracketClub>();

    /// <summary>Goals the first leg finished on, home club first, and null while it is unplayed.</summary>
    public int? FirstLegScore { get; init; }

    /// <summary>Goals the second leg finished on, home club first — the same club, the other end.</summary>
    public int? SecondLegScore { get; init; }

    public Guid? FirstLegMatchId { get; init; }

    public Guid? SecondLegMatchId { get; init; }

    /// <summary>Whether the first leg's match is currently in progress.</summary>
    public bool FirstLegLive { get; init; }

    /// <summary>Whether the second leg's match is currently in progress.</summary>
    public bool SecondLegLive { get; init; }

    public bool IsResolved => Clubs.Any(club => club.IsWinner);
}

/// <summary>
/// One club's line of a tie: who it is, what it scored in each leg, and how the tie ended.
/// </summary>
public class CupBracketClub
{
    public Guid TeamId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string PrimaryColor { get; init; } = string.Empty;

    public string SecondaryColor { get; init; } = string.Empty;

    /// <summary>Goals this club scored in the first leg, and null while the leg is unplayed.</summary>
    public int? FirstLegGoals { get; init; }

    /// <summary>Goals this club conceded in the first leg.</summary>
    public int? FirstLegConceded { get; init; }

    /// <summary>Goals this club scored in the second leg, and null while the leg is unplayed.</summary>
    public int? SecondLegGoals { get; init; }

    /// <summary>Goals this club conceded in the second leg.</summary>
    public int? SecondLegConceded { get; init; }

    /// <summary>Goals this club scored across the two legs, which is the tie's aggregate.</summary>
    public int? AggregateGoals { get; init; }

    public int? AggregateConceded { get; init; }

    /// <summary>What it scored in the shootout, and null when the tie never needed one.</summary>
    public int? PenaltyGoals { get; init; }

    public bool IsWinner { get; init; }

    public bool IsLoser { get; init; }
}
