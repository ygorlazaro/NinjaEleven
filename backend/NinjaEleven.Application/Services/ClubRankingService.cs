using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Service for calculating the Ninja Ranking of clubs.
/// The ranking is based on league performance (last 3 seasons) and cup performance (last 3 seasons).
/// </summary>
public class ClubRankingService
{
    private readonly ITeamRepository _teams;
    private readonly ICompetitionRepository _competitions;
    private readonly ISeasonRepository _seasons;
    private readonly IDivisionRepository _divisions;
    private readonly ITrophyRepository _trophies;
    private readonly IMatchRepository _matches;
    private readonly IFixtureRepository _fixtures;
    private readonly IRoundRepository _rounds;
    private readonly IMatchDayRepository _matchDays;
    private readonly ICupTieRepository _cupTies;
    private readonly StandingsService _standings;

    public ClubRankingService(
        ITeamRepository teams,
        ICompetitionRepository competitions,
        ISeasonRepository seasons,
        IDivisionRepository divisions,
        ITrophyRepository trophies,
        IMatchRepository matches,
        IFixtureRepository fixtures,
        IRoundRepository rounds,
        IMatchDayRepository matchDays,
        ICupTieRepository cupTies,
        StandingsService standings)
    {
        _teams = teams;
        _competitions = competitions;
        _seasons = seasons;
        _divisions = divisions;
        _trophies = trophies;
        _matches = matches;
        _fixtures = fixtures;
        _rounds = rounds;
        _matchDays = matchDays;
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

        // Get the last 3 finished seasons (excluding current if in progress)
        var allSeasons = await _seasons.ListAsync(cancellationToken);
        var finishedSeasons = allSeasons
            .Where(s => s.Status == SeasonStatus.Finished && s.Number < currentSeason.Number)
            .OrderByDescending(s => s.Number)
            .Take(3)
            .ToList();

        var allTeams = await _teams.ListAsync(cancellationToken);
        var allDivisions = (await _divisions.ListAsync(cancellationToken)).ToDictionary(d => d.Tier);

        var entries = new List<ClubRankingEntry>();

        foreach (var team in allTeams)
        {
            var rankingBase = await CalculateRankingBase(team.Id, finishedSeasons, cancellationToken);
            var cupScore = await CalculateCupScoreAsync(team.Id, finishedSeasons, cancellationToken);
            var (currentDivision, currentStrength) = await GetCurrentDivisionAndStrengthAsync(
                team.Id, currentSeasonId, cancellationToken);

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
                CurrentDivision = currentDivision,
                CurrentDivisionName = allDivisions.TryGetValue(currentDivision, out var div) 
                    ? CompetitionRules.DivisionName(currentDivision) 
                    : "",
                Strength = currentStrength,
                SeasonScores = await GetSeasonScoresAsync(team.Id, finishedSeasons, cancellationToken),
                CupScores = await GetCupScoresAsync(team.Id, finishedSeasons, cancellationToken)
            });
        }

        // Sort by ranking points descending
        return entries
            .OrderByDescending(e => e.RankingPoints)
            .ThenByDescending(e => e.Strength)
            .ToList()
            .Select((e, i) => { e.Position = i + 1; return e; })
            .ToList();
    }

    private async Task<int> CalculateRankingBase(Guid teamId, IReadOnlyList<Season> finishedSeasons, CancellationToken cancellationToken)
    {
        if (finishedSeasons.Count == 0)
        {
            return 0;
        }

        var total = 0;
        var weights = new[] { 3, 2, 1 }; // Most recent season gets weight 3

        for (var i = 0; i < Math.Min(finishedSeasons.Count, 3); i++)
        {
            var season = finishedSeasons[i];
            var weight = weights[i];
            var seasonScore = await CalculateSeasonScore(teamId, season.Id, cancellationToken);
            total += seasonScore * weight;
        }

        return total;
    }

    private async Task<int> CalculateSeasonScore(Guid teamId, Guid seasonId, CancellationToken cancellationToken)
    {
        // Find the league edition for each division this team played in
        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);
        var leagueViews = views.Where(v => v.Type == CompetitionType.League && v.Tier.HasValue).ToList();

        foreach (var view in leagueViews)
        {
            var collection = await _standings.CollectAsync(view.Id, view, cancellationToken);
            if (collection.Seeds.Count == 0)
            {
                continue;
            }

            var table = StandingTable.Build(collection.Seeds, collection.Finished);
            var tableList = table.ToList();
            var position = tableList.FindIndex(s => s.TeamId == teamId) + 1;

            if (position > 0)
            {
                // PositionScore = 17 - position (1st = 16, 16th = 1)
                var positionScore = 17 - position;
                // DivisionWeight = 5 - division (1st div = 4, 4th div = 1)
                var divisionWeight = 5 - view.Tier!.Value;
                return positionScore * divisionWeight;
            }
        }

        return 0;
    }

    private async Task<int> CalculateCupScoreAsync(Guid teamId, IReadOnlyList<Season> finishedSeasons, CancellationToken cancellationToken)
    {
        if (finishedSeasons.Count == 0)
        {
            return 0;
        }

        var total = 0;
        var weights = new[] { 3, 2, 1 };

        for (var i = 0; i < Math.Min(finishedSeasons.Count, 3); i++)
        {
            var season = finishedSeasons[i];
            var weight = weights[i];
            var cupPoints = await GetCupPointsForSeasonAsync(teamId, season.Id, cancellationToken);
            total += cupPoints * weight;
        }

        return total;
    }

    private async Task<int> GetCupPointsForSeasonAsync(Guid teamId, Guid seasonId, CancellationToken cancellationToken)
    {
        // Find cup edition for this season
        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);
        var cupView = views.FirstOrDefault(v => v.Type == CompetitionType.Cup);

        if (cupView is null)
        {
            return 0;
        }

        // Find how far this team went in the cup
        var ties = await _cupTies.ListByCompetitionSeasonAsync(cupView.Id, cancellationToken);
        var teamTies = ties.Where(t => t.HomeTeamId == teamId || t.AwayTeamId == teamId).ToList();

        if (teamTies.Count == 0)
        {
            return 0; // Eliminated in 32nd round (didn't qualify - but with 64 clubs all should qualify)
        }

        // Find the latest round this team played and whether they won
        var latestTie = teamTies.OrderByDescending(t => t.RoundNumber).First();
        
        // Points based on round reached
        var points = latestTie.RoundNumber switch
        {
            1 => 0, // Eliminated in 32nd round (64 -> 32)
            2 => 5, // Reached 16th round (32 -> 16)
            3 => 10, // Reached round of 16 (16 -> 8)
            4 => 20, // Reached quarter-finals (8 -> 4)
            5 => 35, // Reached semi-finals (4 -> 2)
            6 => latestTie.WinnerTeamId == teamId ? 70 : 50, // Final: winner 70, runner-up 50
            _ => 0
        };

        return points;
    }

    private async Task<(int Division, double Strength)> GetCurrentDivisionAndStrengthAsync(
        Guid teamId, Guid seasonId, CancellationToken cancellationToken)
    {
        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);
        var leagueViews = views.Where(v => v.Type == CompetitionType.League && v.Tier.HasValue).ToList();

        foreach (var view in leagueViews)
        {
            var collection = await _standings.CollectAsync(view.Id, view, cancellationToken);
            if (collection.Seeds.Count == 0)
            {
                continue;
            }

            var table = StandingTable.Build(collection.Seeds, collection.Finished);
            if (table.Any(s => s.TeamId == teamId))
            {
                var squad = await GetSquadForTeamAsync(teamId, seasonId, cancellationToken);
                var strength = ClubStrength.Calculate(squad);
                return (view.Tier!.Value, strength);
            }
        }

        return (0, 0);
    }

    private async Task<List<Player>> GetSquadForTeamAsync(Guid teamId, Guid seasonId, CancellationToken cancellationToken)
    {
        var memberships = await _teams.GetSquadAsync(teamId, seasonId, cancellationToken);
        var players = new List<Player>();

        foreach (var membership in memberships)
        {
            var player = await _teams.GetPlayerAsync(membership.PlayerId, cancellationToken);
            if (player is not null)
            {
                players.Add(player);
            }
        }

        return players;
    }

    private async Task<Dictionary<int, int>> GetSeasonScoresAsync(
        Guid teamId, IReadOnlyList<Season> finishedSeasons, CancellationToken cancellationToken)
    {
        var scores = new Dictionary<int, int>();
        foreach (var season in finishedSeasons)
        {
            scores[season.Number] = await CalculateSeasonScore(teamId, season.Id, cancellationToken);
        }
        return scores;
    }

    private async Task<Dictionary<int, int>> GetCupScoresAsync(
        Guid teamId, IReadOnlyList<Season> finishedSeasons, CancellationToken cancellationToken)
    {
        var scores = new Dictionary<int, int>();
        foreach (var season in finishedSeasons)
        {
            scores[season.Number] = await GetCupPointsForSeasonAsync(teamId, season.Id, cancellationToken);
        }
        return scores;
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