using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One finished match, as a table needs to see it: who played, how many goals, and how many
/// cards. It carries nothing about how the match felt, because a table is the one place in
/// the game where a goal and a booking are the whole story.
/// </summary>
public readonly record struct MatchResultRow(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeGoals,
    int AwayGoals,
    int HomeRedCards = 0,
    int AwayRedCards = 0,
    int HomeYellowCards = 0,
    int AwayYellowCards = 0)
{
    public Guid ScorerOf(Guid teamId) => teamId == HomeTeamId ? AwayTeamId : HomeTeamId;

    public int GoalsFor(Guid teamId) => teamId == HomeTeamId ? HomeGoals : AwayGoals;

    public int GoalsAgainst(Guid teamId) => teamId == HomeTeamId ? AwayGoals : HomeGoals;

    public int RedCardsOf(Guid teamId) => teamId == HomeTeamId ? HomeRedCards : AwayRedCards;

    public int YellowCardsOf(Guid teamId) => teamId == HomeTeamId ? HomeYellowCards : AwayYellowCards;

    public bool Involves(Guid teamId) => teamId == HomeTeamId || teamId == AwayTeamId;

    /// <summary>The three points for a win, one for a draw.</summary>
    public int PointsFor(Guid teamId)
    {
        var scored = GoalsFor(teamId);
        var conceded = GoalsAgainst(teamId);

        return scored > conceded ? 3 : scored == conceded ? 1 : 0;
    }

    /// <summary>How the match went for this club: a fact about the two scores and the two sides.</summary>
    public MatchOutcome OutcomeFor(Guid teamId)
    {
        var scored = GoalsFor(teamId);
        var conceded = GoalsAgainst(teamId);

        return scored > conceded ? MatchOutcome.Win : scored == conceded ? MatchOutcome.Draw : MatchOutcome.Loss;
    }

    /// <summary>
    /// The row a finished match is, as a table sees it.
    /// </summary>
    /// <remarks>
    /// It lives here, on the row itself, because the goals are on the match and the cards are on
    /// the match's statistics and a table is only the two of them put side by side. A reader
    /// that rebuilt this pair in two places — a division's table and a cup's ranking — would be
    /// two places where the cards of a match could be read as its goals, and neither would be
    /// where the mistake was made.
    ///
    /// A match with no statistics row is a match that has not been summarised yet, and its cards
    /// are zero rather than unknown: a table that refused to count them would order two clubs
    /// on a rule nobody could see, which is worse than ordering them on a rule that reads zero.
    /// </remarks>
    public static MatchResultRow From(Match match, MatchStatistics? statistics) =>
        new(
            match.HomeTeamId,
            match.AwayTeamId,
            match.HomeScore,
            match.AwayScore,
            statistics?.HomeRedCards ?? 0,
            statistics?.AwayRedCards ?? 0,
            statistics?.HomeYellowCards ?? 0,
            statistics?.AwayYellowCards ?? 0);
}

/// <summary>
/// One club's line in a table.
/// </summary>
public class StandingEntry
{
    public required Guid TeamId { get; init; }
    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }

    /// <summary>The squad strength shown next to the club's name in a table.</summary>
    public double Stars { get; init; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points => Wins * 3 + Draws;

    /// <summary>
    /// Where the club finished, counted from one. A table that is sorted has a position, and
    /// a screen that is told its position does not sort it again and get it wrong.
    /// </summary>
    public int Position { get; internal set; }
}

/// <summary>Which number of a line settles a table when the one before it could not.</summary>
public enum StandingCriterionKey
{
    Points,
    GoalDifference,
    GoalsFor,
    HeadToHeadPoints,
    HeadToHeadGoalDifference,
    HeadToHeadGoalsFor,
    RedCards,
    YellowCards,
    SquadStrength
}

/// <summary>
/// How far down the chain a criterion is asked: of every club in the division, or only of the
/// clubs the criteria above it left level.
///
/// The head-to-head is the second kind, and it is the whole reason the two kinds exist. A
/// mini-table over the matches between two clubs is a fair way to part them and an absurd way
/// to order a division: read as a rule for the table, "the head-to-head" would have the second
/// place decided by his meetings with the third while his meetings with the sixteenth counted
/// for nothing. So the chain says which is which, and the grouping is read off the same
/// declaration rather than written beside it.
/// </summary>
public enum StandingCriterionScope
{
    /// <summary>Asked of every club in the division.</summary>
    Table,

    /// <summary>Asked only of the clubs still level on the <see cref="Table"/> criteria.</summary>
    TiedClubs
}

/// <summary>
/// One step of the order a table is settled in, said the way a manager would say it.
///
/// It is a reading of the chain rather than a second copy of it: <see cref="StandingTable"/>
/// applies <see cref="StandingTable.Chain"/>, and a criterion declared here that were not in
/// that list would be a line a "regras" screen promised and the game would not keep. The words
/// live beside the rule for the same reason the numbers do — the rule is what decides the
/// order, and a manager planning a season reads the same sentence in both places or neither.
/// </summary>
/// <param name="Key">Which number is compared.</param>
/// <param name="Descending">Whether the larger value wins.</param>
/// <param name="Scope">Of the whole table, or only of the clubs still level.</param>
/// <param name="Label">The criterion's name, in the game's own words.</param>
/// <param name="Detail">What it counts, and why it sits where it sits.</param>
public sealed record StandingCriterion(
    StandingCriterionKey Key,
    bool Descending,
    StandingCriterionScope Scope,
    string Label,
    string Detail);

/// <summary>
/// The order of a table, in one place.
///
/// The chain is <see cref="Chain"/>, and it is the same list the game sorts by: the order is
/// points, goal difference, goals scored, then — for the clubs still level on all three — the
/// head-to-head, the fewest red cards, the fewest yellows and the strength of the squad. The
/// head-to-head is a mini-table over the matches between the tied clubs rather than a pairwise
/// comparison, so a three-way tie is settled by all three of their matches against each other.
///
/// **The chain is declared rather than written out twice.** A screen that prints the criteria
/// to a manager, and a sort that applies them, are two answers to one question, and the one
/// that is a copy is the one that is wrong: a table sorted on a tiebreaker nobody was told
/// about is a table whose promotion places a club can see itself in and not explain. So the
/// list below is the sort, and the labels are the sort's own description of itself.
///
/// This lives in the domain and not in a service for the same reason the rest of the rules do:
/// a table that is sorted twice, in two places, with two different sets of tiebreakers, is a
/// table that shows one order to the manager and another to the promotion rules.
/// </summary>
public static class StandingTable
{
    /// <summary>
    /// The order a table is settled in, first criterion first, in the game's own words.
    ///
    /// The order of this list is the order the sort applies, and a criterion is not in it
    /// unless it changes the order of a table that something had not already changed.
    /// </summary>
    public static IReadOnlyList<StandingCriterion> Chain { get; } =
    [
        new(
            StandingCriterionKey.Points,
            Descending: true,
            StandingCriterionScope.Table,
            "Pontos",
            "Vitória vale 3 e empate vale 1. Nenhum outro número entra antes deste."),
        new(
            StandingCriterionKey.GoalDifference,
            Descending: true,
            StandingCriterionScope.Table,
            "Saldo de gols",
            "Gols pró menos gols sofridos. Vence quem fez mais gols do que levou."),
        new(
            StandingCriterionKey.GoalsFor,
            Descending: true,
            StandingCriterionScope.Table,
            "Gols pró",
            "Quantos gols o clube fez na temporada. Um time que só empata com quem fraco não passa por aqui."),
        new(
            StandingCriterionKey.HeadToHeadPoints,
            Descending: true,
            StandingCriterionScope.TiedClubs,
            "Pontos no confronto direto",
            "A mini-tabela dos clubes ainda empatados nos três critérios acima, computada só nos jogos entre eles."),
        new(
            StandingCriterionKey.HeadToHeadGoalDifference,
            Descending: true,
            StandingCriterionScope.TiedClubs,
            "Saldo no confronto direto",
            "O mesmo confronto direto, contado em saldo de gols."),
        new(
            StandingCriterionKey.HeadToHeadGoalsFor,
            Descending: true,
            StandingCriterionScope.TiedClubs,
            "Gols no confronto direto",
            "O mesmo confronto direto, contado em gols marcados."),
        new(
            StandingCriterionKey.RedCards,
            Descending: false,
            StandingCriterionScope.TiedClubs,
            "Cartões vermelhos",
            "Menos cartões vermelhos. A expulsão é o que tira um homem do campo."),
        new(
            StandingCriterionKey.YellowCards,
            Descending: false,
            StandingCriterionScope.TiedClubs,
            "Cartões amarelos",
            "Menos cartões amarelos."),
        new(
            StandingCriterionKey.SquadStrength,
            Descending: true,
            StandingCriterionScope.TiedClubs,
            "Força do elenco",
            "O último critério, e o único que não é da temporada: só desempata o que nenhuma outra regra separou.")
    ];

    /// <summary>
    /// The criteria every club in the division is asked, in order.
    /// </summary>
    private static IReadOnlyList<StandingCriterion> Coarse { get; } =
        Chain.Where(criterion => criterion.Scope == StandingCriterionScope.Table).ToList();

    /// <summary>
    /// The criteria that part the clubs the coarse ones left level, in order.
    /// </summary>
    private static IReadOnlyList<StandingCriterion> Fine { get; } =
        Chain.Where(criterion => criterion.Scope == StandingCriterionScope.TiedClubs).ToList();

    /// <summary>
    /// Builds one line per club from the results, then orders them. Every club is a
    /// participant, whether it has played or not, so a table on the first matchday is a table
    /// rather than an empty screen.
    /// </summary>
    /// <param name="teamIds">The clubs in the division, with their squad strength.</param>
    /// <param name="results">Every finished match of the division.</param>
    public static IReadOnlyList<StandingEntry> Build(
        IReadOnlyCollection<(Guid TeamId, double Stars)> teamIds,
        IReadOnlyCollection<MatchResultRow> results)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        ArgumentNullException.ThrowIfNull(results);

        var rows = teamIds
            .Select(entry => BuildRow(entry.TeamId, entry.Stars, results))
            .ToList();

        return Sort(rows, results);
    }

    private static StandingEntry BuildRow(Guid teamId, double stars, IReadOnlyCollection<MatchResultRow> results)
    {
        var played = 0;
        var wins = 0;
        var draws = 0;
        var losses = 0;
        var goalsFor = 0;
        var goalsAgainst = 0;
        var yellows = 0;
        var reds = 0;

        foreach (var result in results)
        {
            if (!result.Involves(teamId)) continue;

            played++;
            goalsFor += result.GoalsFor(teamId);
            goalsAgainst += result.GoalsAgainst(teamId);
            yellows += result.YellowCardsOf(teamId);
            reds += result.RedCardsOf(teamId);

            var points = result.PointsFor(teamId);
            if (points == 3) wins++;
            else if (points == 1) draws++;
            else losses++;
        }

        return new StandingEntry
        {
            TeamId = teamId,
            Played = played,
            Wins = wins,
            Draws = draws,
            Losses = losses,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
            YellowCards = yellows,
            RedCards = reds,
            Stars = stars
        };
    }

    /// <summary>
    /// Orders a set of lines and stamps each one with the position it ended in. Grouping the
    /// lines that are level on the criteria the whole table is asked, and breaking only those
    /// groups by the rest, is what makes the head-to-head the last resort it is supposed to be.
    /// </summary>
    public static IReadOnlyList<StandingEntry> Sort(
        IReadOnlyCollection<StandingEntry> rows,
        IReadOnlyCollection<MatchResultRow> results)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(results);

        var ordered = Apply(rows, Coarse, mutual: null).ToList();

        var sorted = new List<StandingEntry>(ordered.Count);
        var index = 0;

        while (index < ordered.Count)
        {
            var end = index + 1;
            while (end < ordered.Count && LevelOnTheWholeTable(ordered[index], ordered[end])) end++;

            var group = ordered.GetRange(index, end - index);
            sorted.AddRange(group.Count == 1 ? group : BreakTie(group, results));
            index = end;
        }

        for (var position = 0; position < sorted.Count; position++)
        {
            sorted[position].Position = position + 1;
        }

        return sorted;
    }

    /// <summary>
    /// Two lines are level on the table when nothing the whole table is asked can part them.
    ///
    /// It reads the chain rather than naming three fields, so a criterion added to the coarse
    /// end of the chain becomes a criterion that also forms the groups — which is the only
    /// thing that keeps the two ends from disagreeing about which clubs are still tied.
    /// </summary>
    private static bool LevelOnTheWholeTable(StandingEntry left, StandingEntry right) =>
        Coarse.All(criterion => KeyOf(criterion, left, mutual: null) == KeyOf(criterion, right, mutual: null));

    private static IEnumerable<StandingEntry> BreakTie(
        IReadOnlyCollection<StandingEntry> group,
        IReadOnlyCollection<MatchResultRow> results)
    {
        var groupIds = group.Select(row => row.TeamId).ToHashSet();

        // A head-to-head is a mini-table over the tied clubs' games against **each other**, so
        // both sides have to be in the group. It used to admit every match whose *home* team was
        // in it, which pulled each tied club's home games against clubs that were not tied into
        // its private table: a club could be handed a head-to-head decided by a match against
        // the sixteenth of the division, and two clubs level on everything could come out of the
        // one criterion that is supposed to part them still level, because the games that
        // separated them were being counted for both of them. That is a real ordering, it just
        // is not football, and a "regras" screen that says the confrontation is between the
        // tied clubs has to be able to say it because the sort is doing it.
        var mutual = results
            .Where(result => groupIds.Contains(result.HomeTeamId) && groupIds.Contains(result.AwayTeamId))
            .ToList();

        // Squad strength is last, and it is here because a table is ordered even when nothing
        // has separated the clubs in it. On the morning of matchday one every line is zero for
        // every club, and a table ordered by nothing at all is a table in whatever order the
        // database happened to return: the manager's own club could be first because he is the
        // strongest or last because his id sorts low, and neither is a fact about football. The
        // strength of the squad is the one thing actually known about two clubs level on
        // everything else, so it decides — as the last resort it is, behind every result.
        return Apply(group, Fine, mutual);
    }

    /// <summary>
    /// Orders a set of lines by the given criteria, in the order they were given.
    /// </summary>
    /// <param name="rows">The lines to order.</param>
    /// <param name="criteria">The steps of the chain to apply, in order.</param>
    /// <param name="mutual">
    /// The matches the head-to-head is counted over, and null for the pass that does not use
    /// it. A head-to-head is a mini-table, so the set is the group it is being run on rather
    /// than a property of the line.
    /// </param>
    private static IEnumerable<StandingEntry> Apply(
        IEnumerable<StandingEntry> rows,
        IReadOnlyList<StandingCriterion> criteria,
        IReadOnlyCollection<MatchResultRow>? mutual)
    {
        IOrderedEnumerable<StandingEntry>? ordered = null;

        foreach (var criterion in criteria)
        {
            // A stable sort, so two lines the chain cannot part keep the order they arrived in
            // — the order the pass above left them in. That is what makes a head-to-head a
            // tiebreaker and not a reshuffle.
            ordered = ordered is null
                ? Order(rows, criterion, mutual)
                : ThenBy(ordered, criterion, mutual);
        }

        return ordered is null ? rows : ordered.ToList();
    }

    private static IOrderedEnumerable<StandingEntry> Order(
        IEnumerable<StandingEntry> rows,
        StandingCriterion criterion,
        IReadOnlyCollection<MatchResultRow>? mutual) =>
        criterion.Descending
            ? rows.OrderByDescending(row => KeyOf(criterion, row, mutual))
            : rows.OrderBy(row => KeyOf(criterion, row, mutual));

    private static IOrderedEnumerable<StandingEntry> ThenBy(
        IOrderedEnumerable<StandingEntry> ordered,
        StandingCriterion criterion,
        IReadOnlyCollection<MatchResultRow>? mutual) =>
        criterion.Descending
            ? ordered.ThenByDescending(row => KeyOf(criterion, row, mutual))
            : ordered.ThenBy(row => KeyOf(criterion, row, mutual));

    /// <summary>
    /// The number a criterion compares, on a line.
    ///
    /// Everything comes back as a double so the chain is a list of one kind of comparison:
    /// points and goals are small integers, and the squad's strength is the one criterion that
    /// is not a count of anything.
    /// </summary>
    private static double KeyOf(
        StandingCriterion criterion,
        StandingEntry row,
        IReadOnlyCollection<MatchResultRow>? mutual) =>
        criterion.Key switch
        {
            StandingCriterionKey.Points => row.Points,
            StandingCriterionKey.GoalDifference => row.GoalDifference,
            StandingCriterionKey.GoalsFor => row.GoalsFor,
            StandingCriterionKey.HeadToHeadPoints =>
                SumOfTheTie(mutual, row.TeamId, (scored, conceded) => scored > conceded ? 3 : scored == conceded ? 1 : 0),
            StandingCriterionKey.HeadToHeadGoalDifference =>
                SumOfTheTie(mutual, row.TeamId, (scored, conceded) => scored - conceded),
            StandingCriterionKey.HeadToHeadGoalsFor =>
                SumOfTheTie(mutual, row.TeamId, (scored, _) => scored),
            StandingCriterionKey.RedCards => row.RedCards,
            StandingCriterionKey.YellowCards => row.YellowCards,
            StandingCriterionKey.SquadStrength => row.Stars,
            _ => throw new ArgumentOutOfRangeException(
                nameof(criterion),
                criterion.Key,
                "A table is not settled on a criterion the game does not have.")
        };

    private static double SumOfTheTie(
        IReadOnlyCollection<MatchResultRow>? results,
        Guid teamId,
        Func<int, int, double> selector)
    {
        if (results is null) return 0d;

        var total = 0d;
        foreach (var result in results)
        {
            if (!result.Involves(teamId)) continue;
            total += selector(result.GoalsFor(teamId), result.GoalsAgainst(teamId));
        }

        return total;
    }
}
