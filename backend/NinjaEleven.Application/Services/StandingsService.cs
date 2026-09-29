using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// The classification table, and the live one.
///
/// Both are worked out here and by the same code. The official table counts the games that are
/// over; the live table counts the games in progress at their current score as well, so that
/// every matchday a manager can see where his club would stand if the afternoon went a certain
/// way. The tiebreakers are the domain's <see cref="StandingTable"/> in both cases, because the
/// only thing that differs between the two tables is which results are in it — a live table
/// with its own arithmetic is a second set of rules, and a second set of rules is a table that
/// disagrees with itself about who is above whom.
/// </summary>
public class StandingsService
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly ITeamRepository _teamRepository;

    public StandingsService(
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        IMatchRepository matchRepository,
        ICompetitionRepository competitionRepository,
        ITeamRepository teamRepository)
    {
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _matchRepository = matchRepository;
        _competitionRepository = competitionRepository;
        _teamRepository = teamRepository;
    }

    /// <summary>
    /// The table of one competition edition: where the clubs are, and where they would be with
    /// the unfinished games counted in.
    /// </summary>
    public async Task<CompetitionStandings> GetAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var view = await _competitionRepository.GetSeasonViewByIdAsync(competitionSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);

        var collection = await CollectAsync(competitionSeasonId, view, cancellationToken);

        // An edition with no rounds is an edition that has not been drawn yet. It is not a
        // missing thing, it is an empty one, and an empty table is the honest answer.
        if (collection.Seeds.Count == 0)
        {
            return new CompetitionStandings
            {
                CompetitionSeasonId = competitionSeasonId,
                SeasonId = view.SeasonId,
                DivisionId = view.DivisionId,
                Tier = view.Tier,
                CompetitionName = view.Name,
                Official = Array.Empty<StandingRow>(),
                Projected = Array.Empty<StandingRow>()
            };
        }

        var official = ToRows(StandingTable.Build(collection.Seeds, collection.Finished), collection.Clubs, view.Tier, collection.RecentForm);
        var projected = ToRows(
            StandingTable.Build(collection.Seeds, collection.Finished.Concat(collection.InProgress).ToList()),
            collection.Clubs, view.Tier, collection.RecentForm);

        return new CompetitionStandings
        {
            CompetitionSeasonId = competitionSeasonId,
            SeasonId = view.SeasonId,
            DivisionId = view.DivisionId,
            Tier = view.Tier,
            CompetitionName = view.Name,
            Official = official,
            Projected = projected,
            HasLiveMatches = collection.InProgress.Count > 0
        };
    }

    /// <summary>
    /// The one line of a club's, or the fact that it is not in this table.
    ///
    /// The live line is returned whenever any game of the competition is being played, because
    /// a manager who asks where his club is during a matchday is asking where it is *now* — a
    /// table that only ever showed the games that are over would be right about the season and
    /// wrong about the afternoon.
    /// </summary>
    public async Task<StandingRow> GetRowAsync(
        Guid competitionSeasonId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var standings = await GetAsync(competitionSeasonId, cancellationToken);
        var table = standings.HasLiveMatches ? standings.Projected : standings.Official;

        return table.FirstOrDefault(row => row.TeamId == teamId)
            ?? throw new EntityNotFoundException("Standing", teamId);
    }

    /// <summary>
    /// The division a club is in during a season, and the line it holds there.
    ///
    /// It is one call because a club's page is read by somebody looking at that club and not
    /// at the pyramid: the division and the position are two halves of one answer, and asking
    /// for the second one first is how a screen ends up reading four tables to find one line —
    /// or, worse, reading none of them and printing a division it guessed.
    /// </summary>
    public async Task<ClubStanding> GetClubStandingAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        // The squad's strength is answered whatever the club is enrolled in: it is a fact about
        // the men and the season, and a club with no table this season still has a squad.
        var strength = (await SquadStrengthAsync([teamId], seasonId, cancellationToken))
            .GetValueOrDefault(teamId);

        var view = await _competitionRepository.GetDivisionSeasonForTeamAsync(
            teamId, seasonId, cancellationToken);

        // A club enrolled in no division of this season has no table to be on. That is a fact
        // and not a failure, so the answer comes back with nothing in it rather than refusing.
        if (view is null)
        {
            return new ClubStanding
            {
                TeamId = teamId,
                SeasonId = seasonId,
                SquadStars = strength
            };
        }

        var standings = await GetAsync(view.Id, cancellationToken);
        var table = standings.HasLiveMatches ? standings.Projected : standings.Official;

        return new ClubStanding
        {
            TeamId = teamId,
            SeasonId = seasonId,
            CompetitionSeasonId = view.Id,
            DivisionName = view.Name,
            Tier = view.Tier,
            ClubsInDivision = standings.Official.Count,
            SquadStars = strength,
            Row = table.FirstOrDefault(row => row.TeamId == teamId)
        };
    }

    /// <summary>
    /// Everything a caller needs to reason about a table without going back to the database:
    /// the clubs, the results that are over, the results in progress, and the seeds the table
    /// is built from.
    ///
    /// The crowd calculator is the other caller. It needs the same facts about the same
    /// division — where the home club stands, how many clubs there are, how strong they are on
    /// average — and reading them a second time through a second set of queries would be two
    /// answers to one question the moment one of them drifted.
    /// </summary>
    public async Task<StandingCollection> CollectAsync(
        Guid competitionSeasonId,
        CompetitionSeasonView? view = null,
        CancellationToken cancellationToken = default)
    {
        view ??= await _competitionRepository.GetSeasonViewByIdAsync(competitionSeasonId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);

        // A division is made of its participants whether or not anything has been played in it,
        // so the clubs are read from the enrolment first and the rounds are read afterwards.
        // The other order would make a table that did not exist until the first matchday, and
        // a division whose calendar has not been drawn yet would have no clubs in it at all —
        // which would leave the season's own cup with nothing to draw from.
        var teamIds = (await _competitionRepository.ListParticipantsAsync(competitionSeasonId, cancellationToken))
            .Select(participant => participant.TeamId)
            .ToList();

        var rounds = await _roundRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);

        var fixtures = rounds.Count == 0
            ? new List<Fixture>()
            : (await _fixtureRepository.ListByRoundIdsAsync(
                rounds.Select(round => round.Id), cancellationToken)).ToList();

        teamIds = teamIds
            .Concat(fixtures.Select(fixture => fixture.HomeTeamId))
            .Concat(fixtures.Select(fixture => fixture.AwayTeamId))
            .Distinct()
            .ToList();

        var matches = fixtures.Count == 0
            ? new List<Match>()
            : (await _matchRepository.ListByFixtureIdsAsync(
                fixtures.Select(fixture => fixture.Id), cancellationToken)).ToList();

        // A fixture can hold an abandoned match and a replay. Only the newest row that was not
        // abandoned is the match of that fixture, so an interrupted 3-0 never reaches a table.
        var current = matches
            .Where(match => match.Status != MatchStatus.Abandoned)
            .GroupBy(match => match.FixtureId)
            .Select(group => group.OrderByDescending(match => match.CreatedAt).First())
            .ToList();

        var statistics = current.Count == 0
            ? new Dictionary<Guid, MatchStatistics>()
            : (await _matchRepository.ListStatisticsByMatchIdsAsync(
                current.Select(match => match.Id), cancellationToken))
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value!);

        var clubs = await _teamRepository.ListByIdsAsync(teamIds, cancellationToken);
        var strength = await SquadStrengthAsync(teamIds, view.SeasonId, cancellationToken);

        List<(Guid TeamId, double Stars)> seeds = new(teamIds.Count);
        foreach (var teamId in teamIds)
        {
            seeds.Add((teamId, strength.GetValueOrDefault(teamId)));
        }

        var finished = new List<MatchResultRow>();
        var inProgress = new List<MatchResultRow>();

        foreach (var match in current)
        {
            var row = ToResultRow(match, statistics.GetValueOrDefault(match.Id));

            if (match.Status == MatchStatus.Finished) finished.Add(row);
            else inProgress.Add(row);
        }

        return new StandingCollection(view, seeds, finished, inProgress, clubs.ToDictionary(team => team.Id))
        {
            RecentForm = RecentFormOf(finished)
        };
    }

    /// <summary>
    /// How many results a club's run keeps. Five is what a manager reads at a glance beside a
    /// position — long enough to show a run, short enough to sit in a table row — and it is a
    /// number the backend owns, because a client that kept a different number would be showing
    /// a different table of the same matches.
    /// </summary>
    public const int FormRunLength = 5;

    /// <summary>
    /// The last few finished games of every club, oldest first, read off the same rows the table
    /// itself is built from — so a result in a club's form is a result that is also in its points.
    ///
    /// The run is as long as the club has played and no longer. A division three matchdays in has
    /// three results per club, and the screen is the one that shows the two empty places: padding
    /// the run here would mean inventing matches that were never played.
    /// </summary>
    private static IReadOnlyDictionary<Guid, IReadOnlyList<MatchOutcome>> RecentFormOf(
        IReadOnlyList<MatchResultRow> finished)
    {
        var form = new Dictionary<Guid, List<MatchOutcome>>();

        // The rows arrive in the order the matches were played, so a run is the tail of a list
        // and the oldest result is the one that falls off the front.
        foreach (var row in finished)
        {
            foreach (var teamId in new[] { row.HomeTeamId, row.AwayTeamId })
            {
                if (!form.TryGetValue(teamId, out var outcomes))
                {
                    form[teamId] = outcomes = new List<MatchOutcome>(FormRunLength);
                }

                outcomes.Add(row.OutcomeFor(teamId));

                if (outcomes.Count > FormRunLength)
                {
                    outcomes.RemoveAt(0);
                }
            }
        }

        return form.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MatchOutcome>)pair.Value);
    }

    /// <summary>
    /// The average strength of each squad in the competition. It is the average of every
    /// player on the books: an injured striker and a suspended defender are not on the pitch
    /// but they are on the club, and a table that hid them would show a club as weaker
    /// because its goalkeeper was ill.
    /// </summary>
    /// <remarks>
    /// The squads and the players behind them are read in two queries rather than one per club
    /// and one per man. The strength of a table is the same fact for every club in it, and a
    /// reader that asked club by club and player by player was three hundred round trips to say
    /// twelve numbers — which is what made a division's table time out rather than arrive.
    /// </remarks>
    private async Task<Dictionary<Guid, double>> SquadStrengthAsync(
        IReadOnlyCollection<Guid> teamIds,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var strength = new Dictionary<Guid, double>();

        if (teamIds.Count == 0)
        {
            return strength;
        }

        var squads = await _teamRepository.GetSquadsAsync(teamIds, seasonId, cancellationToken);
        var playerIds = squads.Values
            .SelectMany(memberships => memberships)
            .Select(membership => membership.PlayerId)
            .Distinct()
            .ToList();
        var players = playerIds.Count == 0
            ? new Dictionary<Guid, Player>()
            : await _teamRepository.GetPlayersAsync(playerIds, cancellationToken);

        foreach (var teamId in teamIds)
        {
            var squad = squads.TryGetValue(teamId, out var memberships)
                ? memberships
                    .Select(membership => players.GetValueOrDefault(membership.PlayerId))
                    .Where(player => player is not null)
                    .Select(player => player!)
                    .ToList()
                : new List<Player>();

            strength[teamId] = PlayerRating.CalculateTeamStars(squad);
        }

        return strength;
    }

    private static IReadOnlyList<StandingRow> ToRows(
        IReadOnlyList<StandingEntry> entries,
        IReadOnlyDictionary<Guid, Team> clubs,
        int? tier,
        IReadOnlyDictionary<Guid, IReadOnlyList<MatchOutcome>>? recentForm = null) =>
        entries
            .Select(entry => new StandingRow
            {
                TeamId = entry.TeamId,
                Team = clubs.GetValueOrDefault(entry.TeamId),
                Position = entry.Position,
                Zone = CompetitionRules.ZoneFor(tier, entry.Position, entries.Count),
                Played = entry.Played,
                Wins = entry.Wins,
                Draws = entry.Draws,
                Losses = entry.Losses,
                GoalsFor = entry.GoalsFor,
                GoalsAgainst = entry.GoalsAgainst,
                YellowCards = entry.YellowCards,
                RedCards = entry.RedCards,
                Stars = entry.Stars,
                Form = recentForm?.GetValueOrDefault(entry.TeamId) ?? Array.Empty<MatchOutcome>()
            })
            .ToList();

    private static MatchResultRow ToResultRow(Match match, MatchStatistics? statistics) =>
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
