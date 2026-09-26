using FootballManager.Application.Abstractions;
using FootballManager.Application.Models;
using FootballManager.Application.Leagues;
using FootballManager.Application.Repositories;
using FootballManager.Domain.Common;
using FootballManager.Domain.Competitions;
using FootballManager.Domain.Enums;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Teams;

namespace FootballManager.Application.Services;

/// <summary>
/// Competition use cases: enrolling teams, generating the schedule and deriving the
/// tables. Every rule of the competition lives here, never in the controller and never
/// in the repository.
/// </summary>
public class LeagueService
{
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LeagueService(
        ICompetitionRepository competitionRepository,
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        IMatchRepository matchRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        IUnitOfWork unitOfWork)
    {
        _competitionRepository = competitionRepository;
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _matchRepository = matchRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Enrolls the clubs in one edition of the competition and generates a double round
    /// robin schedule: every pair plays twice, once at each home. The use case is
    /// idempotent: asking for an edition that already has a schedule returns the
    /// existing one instead of failing, so a client can call it again after a reload.
    /// </summary>
    public async Task<LeagueSetup> SetupAsync(
        Guid competitionId,
        Guid seasonId,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        if (await _competitionRepository.GetAsync(competitionId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Competition", competitionId);
        }

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        var distinctTeamIds = teamIds.Distinct().ToList();
        if (distinctTeamIds.Count < 2)
        {
            throw new DomainValidationException(
                "NotEnoughTeams",
                "Uma competição precisa de pelo menos dois clubes para gerar a tabela de jogos.");
        }

        var competitionSeason = await _competitionRepository.GetSeasonAsync(competitionId, seasonId, cancellationToken);

        if (competitionSeason is not null)
        {
            var existingRounds = await _roundRepository.ListByCompetitionSeasonAsync(competitionSeason.Id, cancellationToken);
            if (existingRounds.Count > 0)
            {
                var roundIds = existingRounds.Select(round => round.Id).ToHashSet();
                var existingFixtures = (await _fixtureRepository.ListAsync(cancellationToken))
                    .Where(fixture => roundIds.Contains(fixture.RoundId))
                    .ToList();

                return new LeagueSetup
                {
                    CompetitionSeasonId = competitionSeason.Id,
                    CompetitionId = competitionId,
                    SeasonId = seasonId,
                    Fixtures = await WithTeamsAsync(existingFixtures, cancellationToken)
                };
            }
        }

        var teams = new List<Team>(distinctTeamIds.Count);
        foreach (var teamId in distinctTeamIds)
        {
            var team = await _teamRepository.GetAsync(teamId, cancellationToken)
                ?? throw new EntityNotFoundException("Team", teamId);

            teams.Add(team);
        }

        if (competitionSeason is null)
        {
            competitionSeason = CompetitionSeason.Create(competitionId, seasonId);
            var participants = teams
                .Select(team => CompetitionParticipant.Create(competitionSeason.Id, team.Id))
                .ToList();

            await _competitionRepository.AddSeasonAsync(competitionSeason, cancellationToken);
            await _competitionRepository.AddParticipantsAsync(participants, cancellationToken);
        }

        var rounds = new List<Round>();
        var fixtures = new List<Fixture>();
        var roundNumber = 0;

        foreach (var roundPairs in RoundRobin.Build(teams))
        {
            roundNumber++;
            var round = Round.Create(competitionSeason.Id, roundNumber);
            rounds.Add(round);

            foreach (var (home, away) in roundPairs)
            {
                fixtures.Add(Fixture.Create(round.Id, home.Id, away.Id));
            }
        }

        foreach (var round in rounds)
        {
            await _roundRepository.AddAsync(round, cancellationToken);
        }

        foreach (var fixture in fixtures)
        {
            await _fixtureRepository.AddAsync(fixture, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LeagueSetup
        {
            CompetitionSeasonId = competitionSeason.Id,
            CompetitionId = competitionId,
            SeasonId = seasonId,
            Fixtures = WithTeams(fixtures, teams)
        };
    }

    /// <summary>
    /// Loads the clubs behind a set of fixtures so the caller can render names and
    /// colors without a second round trip.
    /// </summary>
    private async Task<IReadOnlyList<FixtureDetails>> WithTeamsAsync(
        IReadOnlyList<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        var teams = new Dictionary<Guid, Team>();

        foreach (var teamId in fixtures
                     .SelectMany(fixture => new[] { fixture.HomeTeamId, fixture.AwayTeamId })
                     .Distinct())
        {
            var team = await _teamRepository.GetAsync(teamId, cancellationToken);

            if (team is not null)
            {
                teams[teamId] = team;
            }
        }

        return WithTeams(fixtures, teams);
    }

    /// <inheritdoc cref="WithTeamsAsync(IReadOnlyList{Fixture}, CancellationToken)"/>
    private static IReadOnlyList<FixtureDetails> WithTeams(
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<Team> teams) =>
        WithTeams(fixtures, teams.ToDictionary(team => team.Id));

    private static IReadOnlyList<FixtureDetails> WithTeams(
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyDictionary<Guid, Team> teams) =>
        fixtures
            .Select(fixture => new FixtureDetails
            {
                Fixture = fixture,
                HomeTeam = teams.GetValueOrDefault(fixture.HomeTeamId),
                AwayTeam = teams.GetValueOrDefault(fixture.AwayTeamId)
            })
            .ToList();

    /// <summary>
    /// Rounds of one competition edition, in order.
    /// </summary>
    public async Task<IReadOnlyList<Round>> GetRoundsAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default) =>
        await _roundRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);

    /// <summary>
    /// Classification table of one competition edition. Tiebreakers, in order: points,
    /// goal difference, goals scored, head-to-head, red cards, yellow cards.
    /// </summary>
    public async Task<IReadOnlyList<StandingRow>> GetStandingsAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var rounds = await _roundRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);
        if (rounds.Count == 0)
        {
            throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);
        }

        var roundIds = rounds.Select(round => round.Id).ToHashSet();
        var fixtures = (await _fixtureRepository.ListAsync(cancellationToken))
            .Where(fixture => roundIds.Contains(fixture.RoundId))
            .ToList();

        var results = await GetFinishedResultsAsync(fixtures, cancellationToken);
        var participants = await _competitionRepository.ListParticipantsAsync(competitionSeasonId, cancellationToken);

        var teamIds = participants
            .Select(participant => participant.TeamId)
            .Concat(fixtures.Select(fixture => fixture.HomeTeamId))
            .Concat(fixtures.Select(fixture => fixture.AwayTeamId))
            .Distinct()
            .ToList();

        var rows = teamIds
            .Select(teamId => BuildRow(teamId, results))
            .ToList();

        var teams = await _teamRepository.ListAsync(cancellationToken);
        var teamsById = teams.ToDictionary(team => team.Id);

        var standings = SortStandings(rows, results);

        foreach (var row in standings)
        {
            row.Team = teamsById.GetValueOrDefault(row.TeamId);
        }

        return standings;
    }

    public async Task<StandingRow> GetStandingAsync(
        Guid competitionSeasonId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var standings = await GetStandingsAsync(competitionSeasonId, cancellationToken);
        var row = standings.FirstOrDefault(standing => standing.TeamId == teamId);

        return row ?? throw new EntityNotFoundException("Standing", teamId);
    }

    /// <summary>
    /// Top scorers of a season, derived from the season state of the players.
    /// </summary>
    public async Task<IReadOnlyList<ScorerRow>> GetScorersAsync(
        Guid seasonId,
        int top = 15,
        CancellationToken cancellationToken = default)
    {
        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        var states = await _playerRepository.ListSeasonStatesAsync(seasonId, null, cancellationToken);
        var goals = states
            .Where(state => state.Goals > 0)
            .OrderByDescending(state => state.Goals)
            .ToList();

        var scorers = new List<ScorerRow>(goals.Count);

        foreach (var state in goals)
        {
            var player = await _playerRepository.GetAsync(state.PlayerId, cancellationToken);
            if (player is null)
            {
                continue;
            }

            var team = await _teamRepository.GetAsync(state.TeamId, cancellationToken);

            scorers.Add(new ScorerRow
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Age = player.CalculateAge(),
                TeamId = state.TeamId,
                TeamName = team?.Name,
                Goals = state.Goals
            });
        }

        return scorers.Take(Math.Max(top, 0)).ToList();
    }

    private async Task<IReadOnlyList<MatchResultRow>> GetFinishedResultsAsync(
        IReadOnlyCollection<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        var rows = new List<MatchResultRow>();

        foreach (var fixture in fixtures)
        {
            var match = await _matchRepository.GetByFixtureAsync(fixture.Id, cancellationToken);
            if (match is null || match.Status != MatchStatus.Finished)
            {
                continue;
            }

            var statistics = await _matchRepository.GetStatisticsAsync(match.Id, cancellationToken);

            rows.Add(new MatchResultRow(
                fixture.HomeTeamId,
                fixture.AwayTeamId,
                match.HomeScore,
                match.AwayScore,
                statistics?.HomeRedCards ?? 0,
                statistics?.AwayRedCards ?? 0,
                statistics?.HomeYellowCards ?? 0,
                statistics?.AwayYellowCards ?? 0));
        }

        return rows;
    }

    private static StandingRow BuildRow(Guid teamId, IReadOnlyCollection<MatchResultRow> results)
    {
        var played = 0;
        var wins = 0;
        var draws = 0;
        var losses = 0;
        var goalsFor = 0;
        var goalsAgainst = 0;
        var yellowCards = 0;
        var redCards = 0;

        foreach (var result in results)
        {
            var isHome = result.HomeTeamId == teamId;
            if (!isHome && result.AwayTeamId != teamId)
            {
                continue;
            }

            var scored = isHome ? result.HomeGoals : result.AwayGoals;
            var conceded = isHome ? result.AwayGoals : result.HomeGoals;

            played++;
            goalsFor += scored;
            goalsAgainst += conceded;
            yellowCards += isHome ? result.HomeYellowCards : result.AwayYellowCards;
            redCards += isHome ? result.HomeRedCards : result.AwayRedCards;

            if (scored > conceded) wins++;
            else if (scored == conceded) draws++;
            else losses++;
        }

        return new StandingRow
        {
            TeamId = teamId,
            Played = played,
            Wins = wins,
            Draws = draws,
            Losses = losses,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
            YellowCards = yellowCards,
            RedCards = redCards
        };
    }

    /// <summary>
    /// Applies the tiebreakers of the competition. Teams tied on points, goal difference
    /// and goals scored are separated by a mini table built from their mutual matches,
    /// so a three-way tie is resolved by the whole group and not by a pair.
    /// </summary>
    private static IReadOnlyList<StandingRow> SortStandings(
        IReadOnlyCollection<StandingRow> rows,
        IReadOnlyCollection<MatchResultRow> results)
    {
        var ordered = rows
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.GoalDifference)
            .ThenByDescending(row => row.GoalsFor)
            .ToList();

        var sorted = new List<StandingRow>(ordered.Count);
        var index = 0;

        while (index < ordered.Count)
        {
            var end = index + 1;
            while (end < ordered.Count && SameMainCriteria(ordered[index], ordered[end]))
            {
                end++;
            }

            var group = ordered.GetRange(index, end - index);
            sorted.AddRange(group.Count == 1 ? group : BreakTie(group, results));
            index = end;
        }

        return sorted;
    }

    private static bool SameMainCriteria(StandingRow left, StandingRow right) =>
        left.Points == right.Points
        && left.GoalDifference == right.GoalDifference
        && left.GoalsFor == right.GoalsFor;

    private static IEnumerable<StandingRow> BreakTie(
        IReadOnlyCollection<StandingRow> group,
        IReadOnlyCollection<MatchResultRow> results)
    {
        var groupIds = group.Select(row => row.TeamId).ToHashSet();
        var headToHead = results.Where(result => groupIds.Contains(result.HomeTeamId)).ToList();

        return group
            .OrderByDescending(row => HeadToHead(row.TeamId, headToHead, (scored, conceded) =>
                scored > conceded ? 3 : scored == conceded ? 1 : 0))
            .ThenByDescending(row => HeadToHead(row.TeamId, headToHead, (scored, conceded) => scored - conceded))
            .ThenByDescending(row => HeadToHead(row.TeamId, headToHead, (scored, _) => scored))
            .ThenBy(row => row.RedCards)
            .ThenBy(row => row.YellowCards)
            .ThenByDescending(row => row.GoalsFor)
            .ToList();
    }

    private static int HeadToHead(
        Guid teamId,
        IReadOnlyCollection<MatchResultRow> results,
        Func<int, int, int> selector)
    {
        var total = 0;

        foreach (var result in results)
        {
            var isHome = result.HomeTeamId == teamId;
            if (!isHome && result.AwayTeamId != teamId)
            {
                continue;
            }

            var scored = isHome ? result.HomeGoals : result.AwayGoals;
            var conceded = isHome ? result.AwayGoals : result.HomeGoals;
            total += selector(scored, conceded);
        }

        return total;
    }

    private record MatchResultRow(
        Guid HomeTeamId,
        Guid AwayTeamId,
        int HomeGoals,
        int AwayGoals,
        int HomeRedCards,
        int AwayRedCards,
        int HomeYellowCards,
        int AwayYellowCards);
}
