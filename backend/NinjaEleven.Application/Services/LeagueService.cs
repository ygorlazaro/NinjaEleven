using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Leagues;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Application.Services;

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
    /// What each division's table is paid out of, and how the purse is shared between the
    /// twelve clubs of it.
    ///
    /// It is asked for and not worked out on the screen because the share is a geometric weight
    /// and the twelfth club is paid the rounding remainder of the other eleven: a client dividing
    /// the purse by twelve would be a client publishing a championship that does not pay out its
    /// own purse, and the last club's cheque is the one number a manager adds up the other
    /// eleven to check.
    ///
    /// The whole pyramid, not one division: the money is what makes the table worth playing in
    /// the first place, and a manager reading his own table wants to know what the table above
    /// and the table below are playing for.
    /// </summary>
    public IReadOnlyList<DivisionPurse> GetChampionshipPurses() =>
        CompetitionRules.Tiers()
            .Select(tier =>
            {
                var purse = PrizeRules.PurseForTier(tier);
                var shares = PrizeRules.ChampionshipPrizes(CompetitionRules.ClubsPerDivision, purse);

                return new DivisionPurse(
                    tier,
                    CompetitionRules.DivisionName(tier),
                    purse,
                    CompetitionRules.ClubsPerDivision,
                    shares.Select((amount, index) => new PrizeShare(index + 1, amount)).ToList());
            })
            .ToList();

/// <summary>

    /// Top scorers of a season, derived from the season state of the players.
    /// </summary>
    /// <param name="seasonId">The season being counted.</param>
    /// <param name="top">How many to return.</param>
    /// <param name="competition">
    /// League, Cup or Supercup; null for the whole season.
    ///
    /// A season's own state counts every kind of competition at once, so a cup's chart cannot be
    /// read off it: it is counted from the match lines instead, which is the same sum restricted
    /// to the cup's matches. A cup chart built from the season total would be a league's goals
    /// wearing the cup's name, and a manager would read his striker's cup run off it wrong.
    /// </param>
    public async Task<IReadOnlyList<ScorerRow>> GetScorersAsync(
        Guid seasonId,
        int top = 15,
        CompetitionType? competition = null,
        CancellationToken cancellationToken = default)
    {
        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        if (competition.HasValue)
        {
            var lines = await _playerRepository.ListSeasonScorerLinesAsync(
                seasonId, competition.Value, cancellationToken);

            // A player who changed clubs inside the season has a line per club, and a scorers
            // list with the same man on it twice is a list of nobody in particular. He is
            // counted once, under the club he scored for most.
            var byPlayer = lines
                .GroupBy(line => line.PlayerId)
                .Select(group => group.OrderByDescending(line => line.Goals).First())
                .ToList();

            var players = (await _playerRepository.ListAsync(cancellationToken))
                .Where(player => byPlayer.Any(line => line.PlayerId == player.Id))
                .ToList();
            var clubs = await _teamRepository.ListByIdsAsync(byPlayer.Select(line => line.TeamId), cancellationToken);
            var playerById = players.ToDictionary(player => player.Id);
            var clubById = clubs.ToDictionary(club => club.Id);

            return byPlayer
                .Where(line => playerById.ContainsKey(line.PlayerId))
                .Select(line => new ScorerRow
                {
                    PlayerId = line.PlayerId,
                    PlayerName = playerById[line.PlayerId].Name,
                    Age = playerById[line.PlayerId].CalculateAge(),
                    TeamId = line.TeamId,
                    TeamName = clubById.TryGetValue(line.TeamId, out var club) ? club.Name : null,
                    TeamPrimaryColor = club?.PrimaryColor,
                    TeamSecondaryColor = club?.SecondaryColor,
                    Goals = line.Goals
                })
                .Take(Math.Max(top, 0))
                .ToList();
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
                TeamPrimaryColor = team?.PrimaryColor,
                TeamSecondaryColor = team?.SecondaryColor,
                Goals = state.Goals
            });
        }

        return scorers.Take(Math.Max(top, 0)).ToList();
    }
}
