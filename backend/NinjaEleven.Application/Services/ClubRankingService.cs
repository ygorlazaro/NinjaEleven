using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Service for calculating the Ninja Ranking of clubs.
/// The ranking is based on league performance (last 3 seasons) and cup performance (last 3 seasons).
/// </summary>
/// <remarks>
/// The whole table is worked out in one pass over the world. The seasons' editions are read
/// together, each edition's table is built once and read by every club in it, the cup ties of
/// every season arrive in one query, and the squads of the season in progress arrive as one
/// book. A ranking that asked each club for its own history asked the same question of the
/// same tables once per club — and a division's table is a hundred round trips on its own,
/// which is how a page of forty-eight clubs became a page that timed out.
/// </remarks>
public class ClubRankingService
{
    /// <summary>
    /// The weight each of the last three seasons carries, most recent first. Three seasons of
    /// a club's football, and the one in progress is not one of them: a season being played is
    /// not a result.
    /// </summary>
    private static readonly int[] SeasonWeights = [3, 2, 1];

    /// <summary>
    /// The divisions of a season and where every club stood in each of their tables, best tier
    /// first. A club belongs to one division, so the first table it is found in is the one it
    /// played in — which is the whole reason the divisions are ordered here rather than left
    /// to whichever one the reader happened to build first.
    /// </summary>
    private sealed record SeasonTables(
        Guid SeasonId,
        IReadOnlyList<DivisionTable> Divisions);

    /// <summary>One division's table of one season, as a position per club.</summary>
    private sealed record DivisionTable(int Tier, IReadOnlyDictionary<Guid, int> Positions);

    private readonly ITeamRepository _teams;
    private readonly ICompetitionRepository _competitions;
    private readonly ISeasonRepository _seasons;
    private readonly ICupTieRepository _cupTies;
    private readonly StandingsService _standings;

    public ClubRankingService(
        ITeamRepository teams,
        ICompetitionRepository competitions,
        ISeasonRepository seasons,
        ICupTieRepository cupTies,
        StandingsService standings)
    {
        _teams = teams;
        _competitions = competitions;
        _seasons = seasons;
        _cupTies = cupTies;
        _standings = standings;
    }

    /// <summary>
    /// Calculates the Ninja Ranking for all clubs.
    /// RankingNinja = RankingBase + CupScore
    /// RankingBase = Season[-1] * 3 + Season[-2] * 2 + Season[-3] * 1
    /// SeasonScore = (17 - position) * (5 - division)
    /// CupScore = Cup[-1] * 3 + Cup[-2] * 2 + Cup[-3] * 1
    /// </summary>
    public async Task<IReadOnlyList<ClubRankingEntry>> CalculateRankingAsync(
        Guid currentSeasonId,
        CancellationToken cancellationToken = default)
    {
        var currentSeason = await _seasons.GetAsync(currentSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", currentSeasonId);

        // The last three seasons that are over, and the season in progress: every answer below
        // is a question about the same set of editions, so they are read together.
        var allSeasons = await _seasons.ListAsync(cancellationToken);
        var finishedSeasons = allSeasons
            .Where(season => season.Status == SeasonStatus.Finished && season.Number < currentSeason.Number)
            .OrderByDescending(season => season.Number)
            .Take(SeasonWeights.Length)
            .ToList();

        var allTeams = await _teams.ListAsync(cancellationToken);
        var teamIds = allTeams.Select(team => team.Id).ToList();

        var seasonIds = new[] { currentSeasonId }.Concat(finishedSeasons.Select(season => season.Id)).ToList();
        var views = await _competitions.ListSeasonViewsAsync(seasonIds, cancellationToken);

        var tables = await SeasonTablesAsync(views, cancellationToken);
        var cupTies = await CupTiesAsync(views, finishedSeasons, cancellationToken);
        var currentTier = await _teams.GetTeamDivisionsAsync(currentSeasonId, teamIds, cancellationToken);
        var strength = await SquadStrengthAsync(currentSeasonId, teamIds, cancellationToken);

        var entries = new List<ClubRankingEntry>();

        foreach (var team in allTeams)
        {
            var seasonScores = new Dictionary<int, int>();
            var cupScores = new Dictionary<int, int>();
            var rankingBase = 0;
            var cupScore = 0;

            for (var i = 0; i < finishedSeasons.Count; i++)
            {
                var season = finishedSeasons[i];
                var weight = SeasonWeights[i];

                var seasonScore = SeasonScoreOf(tables, season.Id, team.Id);
                var cupPoints = CupPointsOf(cupTies.GetValueOrDefault(season.Id), team.Id);

                seasonScores[season.Number] = seasonScore;
                cupScores[season.Number] = cupPoints;

                rankingBase += seasonScore * weight;
                cupScore += cupPoints * weight;
            }

            entries.Add(new ClubRankingEntry
            {
                TeamId = team.Id,
                TeamName = team.Name,
                TeamShortName = team.ShortName,
                PrimaryColor = team.PrimaryColor,
                SecondaryColor = team.SecondaryColor,
                ManagerName = "", // Will be filled by caller if needed
                RankingPoints = rankingBase + cupScore,
                RankingBase = rankingBase,
                CupScore = cupScore,
                CurrentDivision = currentTier.GetValueOrDefault(team.Id),
                CurrentDivisionName = currentTier.TryGetValue(team.Id, out var tier)
                    ? CompetitionRules.DivisionName(tier)
                    : "",
                Strength = strength.GetValueOrDefault(team.Id),
                SeasonScores = seasonScores,
                CupScores = cupScores
            });
        }

        return entries
            .OrderByDescending(entry => entry.RankingPoints)
            .ThenByDescending(entry => entry.Strength)
            .ToList()
            .Select((entry, index) => { entry.Position = index + 1; return entry; })
            .ToList();
    }

    /// <summary>
    /// The tables of every season asked about, one per edition. A division's table is built
    /// once and read by every club in it: the ranking needs a position for forty-eight clubs
    /// out of twelve tables, and a table that was rebuilt per club was a table read forty-eight
    /// times.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, SeasonTables>> SeasonTablesAsync(
        IReadOnlyDictionary<Guid, IReadOnlyList<CompetitionSeasonView>> views,
        CancellationToken cancellationToken)
    {
        var tables = new Dictionary<Guid, SeasonTables>();

        foreach (var pair in views)
        {
            var divisions = new List<DivisionTable>();

            foreach (var view in pair.Value
                         .Where(view => view.Type == CompetitionType.League && view.Tier.HasValue))
            {
                var collection = await _standings.CollectAsync(view.Id, view, cancellationToken);
                if (collection.Seeds.Count == 0)
                {
                    continue;
                }

                divisions.Add(new DivisionTable(
                    view.Tier!.Value,
                    StandingTable.Build(collection.Seeds, collection.Finished)
                        .ToDictionary(entry => entry.TeamId, entry => entry.Position)));
            }

            tables[pair.Key] = new SeasonTables(
                pair.Key,
                divisions.OrderBy(division => division.Tier).ToList());
        }

        return tables;
    }

    /// <summary>
    /// The cup ties of every season asked about, keyed by the season they belong to. The ties
    /// know the edition they were drawn in, and the season is what the ranking sums over, so
    /// the two are joined here once instead of inside a loop over clubs.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CupTie>>> CupTiesAsync(
        IReadOnlyDictionary<Guid, IReadOnlyList<CompetitionSeasonView>> views,
        IReadOnlyList<Season> finishedSeasons,
        CancellationToken cancellationToken)
    {
        var cupEditions = new Dictionary<Guid, Guid>();
        foreach (var season in finishedSeasons)
        {
            var cup = views.TryGetValue(season.Id, out var seasonViews)
                ? seasonViews.FirstOrDefault(view => view.Type == CompetitionType.Cup)
                : null;

            if (cup is not null)
            {
                cupEditions[season.Id] = cup.Id;
            }
        }

        if (cupEditions.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CupTie>>();
        }

        var byEdition = await _cupTies.ListByCompetitionSeasonsAsync(
            cupEditions.Values.Distinct().ToList(),
            cancellationToken);

        return cupEditions
            .Where(pair => byEdition.ContainsKey(pair.Value))
            .ToDictionary(pair => pair.Key, pair => byEdition[pair.Value]);
    }

    /// <summary>
    /// What one season of one club is worth: the place it took in the division it played,
    /// weighted by that division. A club with no table in that season scored nothing, which is
    /// a club that was not there rather than a club that finished last.
    /// </summary>
    private static int SeasonScoreOf(
        IReadOnlyDictionary<Guid, SeasonTables> tables,
        Guid seasonId,
        Guid teamId)
    {
        if (!tables.TryGetValue(seasonId, out var season))
        {
            return 0;
        }

        foreach (var division in season.Divisions)
        {
            if (division.Positions.TryGetValue(teamId, out var position))
            {
                return (17 - position) * (5 - division.Tier);
            }
        }

        return 0;
    }

    /// <summary>
    /// How many points a club's cup run was worth, taken from the round it fell in and from
    /// whether it won the last tie it played. The final pays two amounts because the losing
    /// side of a final is a fact in its own right: the runner-up is paid nearly the winner's
    /// money, which is what makes the cup worth playing to the bottom of it.
    /// </summary>
    private static int CupPointsOf(IReadOnlyList<CupTie>? ties, Guid teamId)
    {
        if (ties is null)
        {
            return 0;
        }

        var latestTie = ties
            .Where(tie => tie.HomeTeamId == teamId || tie.AwayTeamId == teamId)
            .OrderByDescending(tie => tie.RoundNumber)
            .FirstOrDefault();

        if (latestTie is null)
        {
            return 0;
        }

        return latestTie.RoundNumber switch
        {
            1 => 0,     // Out in the round of thirty-two: the club was never really in it.
            2 => 5,
            3 => 10,
            4 => 20,
            5 => 35,
            6 => latestTie.WinnerTeamId == teamId ? 70 : 50,
            _ => 0
        };
    }

    /// <summary>
    /// The strength of every squad of a season, read as one book rather than club by club.
    /// The strength column is what separates two clubs with the same points, and it is the
    /// same number a table is seeded with.
    /// </summary>
    private async Task<Dictionary<Guid, double>> SquadStrengthAsync(
        Guid seasonId,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        var strength = new Dictionary<Guid, double>();

        if (teamIds.Count == 0)
        {
            return strength;
        }

        var squads = await _teams.GetSquadsAsync(teamIds, seasonId, cancellationToken);
        var playerIds = squads.Values
            .SelectMany(memberships => memberships)
            .Select(membership => membership.PlayerId)
            .Distinct()
            .ToList();
        var players = playerIds.Count == 0
            ? new Dictionary<Guid, Player>()
            : await _teams.GetPlayersAsync(playerIds, cancellationToken);

        foreach (var teamId in teamIds)
        {
            var squad = squads.TryGetValue(teamId, out var memberships)
                ? memberships
                    .Select(membership => players.GetValueOrDefault(membership.PlayerId))
                    .Where(player => player is not null)
                    .Select(player => player!)
                    .ToList()
                : new List<Player>();

            strength[teamId] = ClubStrength.Calculate(squad);
        }

        return strength;
    }
}

/// <summary>
/// A single entry in the club ranking table.
/// </summary>
public class ClubRankingEntry
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamShortName { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public int Position { get; set; }
    public int RankingPoints { get; set; }
    public int RankingBase { get; set; }
    public int CupScore { get; set; }
    public int CurrentDivision { get; set; }
    public string CurrentDivisionName { get; set; } = string.Empty;
    public double Strength { get; set; }
    public Dictionary<int, int> SeasonScores { get; set; } = new();
    public Dictionary<int, int> CupScores { get; set; } = new();
}
