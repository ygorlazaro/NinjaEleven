using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Leagues;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Lays a season out: the matchdays, and the fixtures in every window of every one of them.
///
/// The season is a calendar before it is a set of matches. A matchday is a date, a window is
/// a slice of that date, and a fixture belongs to exactly one window — so "when does this
/// club play" has an answer, "has the club that is resting played" has an answer, and the
/// recovery after a window can be applied once and only once, because the window it belongs
/// to is a row rather than a guess from the fixture's date.
///
/// It is idempotent for the same reason <c>POST /league/setup</c> is: a season that already
/// has matchdays is a season that has been drawn, and asking again returns what is there
/// instead of a second calendar.
/// </summary>
public class SeasonCalendarService : ISeasonCalendarBuilder
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMatchDayRepository _matchDayRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly ICupTieRepository _cupTieRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITrophyRepository _trophyRepository;
    private readonly StandingsService _standingsService;
    private readonly IUnitOfWork _unitOfWork;

    public SeasonCalendarService(
        ISeasonRepository seasonRepository,
        IMatchDayRepository matchDayRepository,
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        ICupTieRepository cupTieRepository,
        ICompetitionRepository competitionRepository,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        ITrophyRepository trophyRepository,
        StandingsService standingsService,
        IUnitOfWork unitOfWork)
    {
        _seasonRepository = seasonRepository;
        _matchDayRepository = matchDayRepository;
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _cupTieRepository = cupTieRepository;
        _competitionRepository = competitionRepository;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _trophyRepository = trophyRepository;
        _standingsService = standingsService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// The matchdays of a season, and the windows scheduled on them. A season that has already
    /// been drawn is returned as it stands.
    /// </summary>
    public async Task<SeasonCalendar> GetAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var season = await _seasonRepository.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var matchDays = await _matchDayRepository.ListBySeasonAsync(seasonId, cancellationToken);
        var views = await _competitionRepository.ListSeasonViewsAsync(seasonId, cancellationToken);

        // Only this season's editions are asked about, so the calendar of a career that has
        // played eight seasons does not read the other seven to draw the one being opened.
        var windows = views.Count == 0
            ? new List<Round>()
            : (await _roundRepository.ListByCompetitionSeasonIdsAsync(
                    views.Select(view => view.Id), cancellationToken))
                .Where(round => round.MatchDayId is not null)
                .OrderBy(round => round.Number)
                .ThenBy(round => round.Window)
                .ToList();

        return new SeasonCalendar
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            MatchDays = matchDays.OrderBy(matchDay => matchDay.Number).ToList(),
            Windows = windows
        };
    }

    /// <summary>
    /// Draws the whole season, or returns the calendar that is already there.
    ///
    /// Everything about the shape comes from <see cref="CompetitionRules"/>: thirty-four days a
    /// season, the four divisions' round-robins one round a day from day two, the cup's six
    /// tie-rounds over two legs each on the seventh, twelfth, seventeenth, twenty-second,
    /// twenty-seventh and thirty-second days, and the Supercup alone on the first day of a
    /// season that has a season before it.
    /// </summary>
    public async Task DrawAsync(Guid seasonId, CancellationToken cancellationToken = default) =>
        await BuildAsync(seasonId, cancellationToken);

    public async Task<SeasonCalendar> BuildAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var season = await _seasonRepository.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        // The guard is the fixtures, not the matchdays. A season that was interrupted halfway
        // through its first draw has matchdays and no windows, and stopping there would leave
        // it undrawable for good: every later call would find matchdays, decide the season was
        // already drawn, and return an empty calendar. A calendar with nothing in it is not a
        // calendar.
        var existing = await _matchDayRepository.ListBySeasonAsync(seasonId, cancellationToken);
        if (existing.Count > 0 && await HasFixturesAsync(seasonId, cancellationToken))
        {
            return await GetAsync(seasonId, cancellationToken);
        }

        if (existing.Count > 0)
        {
            await DiscardTheHalfDrawnCalendarAsync(seasonId, cancellationToken);
        }

        var views = await _competitionRepository.ListSeasonViewsAsync(seasonId, cancellationToken);
        var leagueViews = views.Where(view => view.Type == CompetitionType.League).ToList();
        var cupViews = views.Where(view => view.Type == CompetitionType.Cup).ToList();
        var superCupViews = views.Where(view => view.Type == CompetitionType.SuperCup).ToList();

        if (leagueViews.Count == 0)
        {
            throw new DomainValidationException(
                "SeasonHasNoLeague",
                "A season with no division in it has no calendar to draw.");
        }

        var matchDays = CreateMatchDays(season);
        await _matchDayRepository.AddRangeAsync(matchDays, cancellationToken);

        var byNumber = matchDays.ToDictionary(matchDay => matchDay.Number);

        var rounds = new List<Round>();
        var fixtures = new List<Fixture>();
        var ties = new List<CupTie>();

        foreach (var view in leagueViews)
        {
            await DrawLeagueAsync(view, byNumber, rounds, fixtures, cancellationToken);
        }

        foreach (var view in cupViews)
        {
            var entrants = await EnrolTheCupAsync(view, cancellationToken);

            if (entrants.Count >= 2)
            {
                DrawFirstRound(view, entrants, byNumber, rounds, fixtures, ties);
            }
        }

        foreach (var view in superCupViews)
        {
            var entrants = await EnrolTheSuperCupAsync(view, cancellationToken);

            if (entrants.Count >= 2)
            {
                DrawSuperCup(view, entrants, byNumber, rounds, fixtures);
            }
        }

        foreach (var round in rounds)
        {
            await _roundRepository.AddAsync(round, cancellationToken);
        }

        await _fixtureRepository.AddRangeAsync(fixtures, cancellationToken);

        if (ties.Count > 0)
        {
            await _cupTieRepository.AddRangeAsync(ties, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(seasonId, cancellationToken);
    }

    /// <summary>
    /// Whether a season's calendar has anything in it yet.
    /// </summary>
    private async Task<bool> HasFixturesAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var views = await _competitionRepository.ListSeasonViewsAsync(seasonId, cancellationToken);
        if (views.Count == 0) return false;

        var ids = (await _roundRepository.ListByCompetitionSeasonIdsAsync(
                views.Select(view => view.Id), cancellationToken))
            .Select(round => round.Id)
            .ToList();

        if (ids.Count == 0) return false;

        var fixtures = await _fixtureRepository.ListByRoundIdsAsync(ids, cancellationToken);
        return fixtures.Count > 0;
    }

    /// <summary>
    /// Throws away a calendar that was started and never finished, so that the draw can start
    /// again from nothing rather than from a half of it.
    ///
    /// A fixture is only deleted when nothing has been played in it, which is what makes this
    /// safe: once a window has a match in it, a half-drawn calendar is no longer a half-drawn
    /// calendar, it is a season in progress, and a season in progress is repaired by a person
    /// rather than by a rebuild.
    /// </summary>
    private async Task DiscardTheHalfDrawnCalendarAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var views = await _competitionRepository.ListSeasonViewsAsync(seasonId, cancellationToken);

        var mine = views.Count == 0
            ? new List<Round>()
            : (await _roundRepository.ListByCompetitionSeasonIdsAsync(
                    views.Select(view => view.Id), cancellationToken))
                .ToList();
        if (mine.Count == 0) return;

        var fixtures = await _fixtureRepository.ListByRoundIdsAsync(
            mine.Select(round => round.Id), cancellationToken);

        var played = new HashSet<Guid>(
            (await _matchRepository.ListByFixtureIdsAsync(
                fixtures.Select(fixture => fixture.Id), cancellationToken))
            .Select(match => match.FixtureId));

        var untouched = fixtures.Where(fixture => !played.Contains(fixture.Id)).ToList();

        if (untouched.Count != fixtures.Count)
        {
            // Something has been played. Half-deleting a season in progress is not this
            // method's decision to make, and the windows that already hold a match are the
            // ones a person has to look at.
            return;
        }

        _fixtureRepository.RemoveRange(untouched);
        _roundRepository.RemoveRange(mine);
    }

    /// <summary>
    /// The matchdays of a season: one a day, starting on the day the season starts and running
    /// to its rest day.
    ///
    /// The count is the whole of <see cref="CompetitionRules.SeasonMatchDays"/> and not the
    /// championship's: the cup's final is played on the thirty-second and thirty-third days, and
    /// the thirty-fourth is the day with nothing in it, so a calendar cut at the championship's
    /// thirty matchdays would leave the final off the end of the season. Day one is in the
    /// calendar too, because it is the day the Supercup is played in and it is the day a manager
    /// is told about.
    /// </summary>
    private static List<MatchDay> CreateMatchDays(Season season)
    {
        var matchDays = new List<MatchDay>(CompetitionRules.SeasonMatchDays);

        for (var number = 1; number <= CompetitionRules.SeasonMatchDays; number++)
        {
            matchDays.Add(MatchDay.Create(
                season.Id,
                number,
                season.StartDate.AddDays((number - 1) * CompetitionRules.DaysBetweenMatchDays)));
        }

        return matchDays;
    }

    /// <summary>
    /// A division's round-robin, one round a day in the first window.
    ///
    /// A division of sixteen plays every other fifteen twice, so thirty rounds fill thirty days
    /// exactly: the first fifteen days are the first leg of every pair and the next fifteen the
    /// same pairs with the ground the other way round, which is why a club's first match of the
    /// season is at home and its second is away. Day one belongs to the Supercup, so the first
    /// round is on day two. The participants are the edition's own, read from the database,
    /// because the edition is what says who is in this division this season — a club promoted
    /// into it is not in it until the season that promoted it.
    /// </summary>
    private async Task DrawLeagueAsync(
        CompetitionSeasonView view,
        IReadOnlyDictionary<int, MatchDay> matchDays,
        List<Round> rounds,
        List<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        var participants = await _competitionRepository.ListParticipantsAsync(view.Id, cancellationToken);
        var teams = await _teamRepository.ListByIdsAsync(
            participants.Select(participant => participant.TeamId), cancellationToken);

        if (teams.Count < 2)
        {
            return;
        }

        var pairs = RoundRobin.Build(teams.OrderBy(team => team.Id).ToList());
        var count = Math.Min(pairs.Count, CompetitionRules.LeagueMatchDays);

        for (var index = 0; index < count; index++)
        {
            var roundNumber = index + 1;
            var matchDayNumber = CompetitionRules.ChampionshipMatchDayOf(roundNumber);

            if (!matchDays.TryGetValue(matchDayNumber, out var matchDay))
            {
                continue;
            }

            var round = Round.Create(view.Id, roundNumber, CompetitionRules.ChampionshipWindow);
            round.ScheduleOn(matchDay.Id);
            rounds.Add(round);

            foreach (var (home, away) in pairs[index])
            {
                fixtures.Add(Fixture.Create(round.Id, home.Id, away.Id));
            }
        }
    }

    /// <summary>
    /// The sixty-four clubs in the cup, in the order the tie-round is drawn from.
    ///
    /// Entrants live on the edition rather than being worked out every time a draw is needed,
    /// because a cup that is re-seeded on every read is a cup whose draw can change between
    /// two questions asked of the same state. An edition that already has entrants keeps them.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnrolTheCupAsync(
        CompetitionSeasonView view,
        CancellationToken cancellationToken)
    {
        var enrolled = await _competitionRepository.ListParticipantsAsync(view.Id, cancellationToken);
        if (enrolled.Count > 0)
        {
            return enrolled.Select(participant => participant.TeamId).ToList();
        }

        var seeds = await GetAllClubsForCupAsync(view, cancellationToken);
        if (seeds.Count < 2)
        {
            return seeds.ToList();
        }

        await _competitionRepository.AddParticipantsAsync(
            seeds.Select(teamId => CompetitionParticipant.Create(view.Id, teamId)).ToList(),
            cancellationToken);

        return seeds.ToList();
    }

    /// <summary>
    /// The two clubs in the Supercup: the club that won the championship and the club that won
    /// the cup.
    ///
    /// Both of them are the winners of the season *before* this one, which is why the first
    /// season of a world has no Supercup at all rather than a Supercup between two clubs that
    /// have not won anything. The trophies are read from the shelf rather than worked out from
    /// last season's tables, because a table read again is a table that could be read
    /// differently after a correction.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnrolTheSuperCupAsync(
        CompetitionSeasonView view,
        CancellationToken cancellationToken)
    {
        var enrolled = await _competitionRepository.ListParticipantsAsync(view.Id, cancellationToken);
        if (enrolled.Count >= 2)
        {
            return enrolled.Select(participant => participant.TeamId).Take(2).ToList();
        }

        var current = await _seasonRepository.GetAsync(view.SeasonId, cancellationToken);
        if (current is null)
        {
            return Array.Empty<Guid>();
        }

        var previous = (await _seasonRepository.ListAsync(cancellationToken))
            .Where(season => season.Number < current.Number)
            .OrderByDescending(season => season.Number)
            .FirstOrDefault();

        if (previous is null)
        {
            return Array.Empty<Guid>();
        }

        var trophies = await _trophyRepository.ListBySeasonAsync(previous.Id, cancellationToken);
        var views = await _competitionRepository.ListSeasonViewsAsync(previous.Id, cancellationToken);

        // The two titles that decide it, read off the shelf and not worked out again: the
        // champion of the first division and the winner of the cup. A second-division champion
        // is not a candidate, which is why the view's tier is part of the test.
        var topDivision = views.FirstOrDefault(other =>
            other.Type == CompetitionType.League && other.Tier == 1);
        var cupEdition = views.FirstOrDefault(other => other.Type == CompetitionType.Cup);

        var champion = ChampionOf(trophies, topDivision?.Id);
        var cupWinner = ChampionOf(trophies, cupEdition?.Id);

        if (champion is null || cupWinner is null)
        {
            // A season that had no first division and no cup — a world that has not been played
            // yet — has no Supercup to draw, and inventing two clubs for it would be a match
            // between nobody.
            return Array.Empty<Guid>();
        }

        var home = champion.TeamId;
        var away = cupWinner.TeamId;

        if (home == away)
        {
            // A club that won both cannot play itself, so the Supercup falls to the second
            // place of the first division. A Supercup is two clubs meeting, and the double
            // winner is one club, not two.
            var second = trophies.FirstOrDefault(trophy =>
                trophy.Kind == TrophyKind.RunnerUp
                && trophy.CompetitionSeasonId == topDivision!.Id
                && trophy.TeamId != home);

            if (second is null)
            {
                return Array.Empty<Guid>();
            }

            away = second.TeamId;
        }

        await _competitionRepository.AddParticipantsAsync(
            new List<CompetitionParticipant>
            {
                CompetitionParticipant.Create(view.Id, home),
                CompetitionParticipant.Create(view.Id, away)
            },
            cancellationToken);

        return new[] { home, away };
    }

    private static TrophyAward? ChampionOf(
        IReadOnlyList<TrophyAward> trophies,
        Guid? competitionSeasonId) =>
        competitionSeasonId is null
            ? null
            : trophies.FirstOrDefault(trophy =>
                trophy.Kind == TrophyKind.Champion
                && trophy.CompetitionSeasonId == competitionSeasonId.Value);

    /// <summary>
    /// The cup's first tie-round: thirty-two ties, two legs each, the first leg in the cup's
    /// window of the seventh day and the return in the cup's window of the eighth.
    ///
    /// Only the first round is drawn here. The rounds after it are drawn from the winners of
    /// the round before, and a bracket drawn on the first day of the season would be a
    /// bracket that had decided who was in the final before anything had been played.
    /// </summary>
    private static void DrawFirstRound(
        CompetitionSeasonView view,
        IReadOnlyList<Guid> entrants,
        IReadOnlyDictionary<int, MatchDay> matchDays,
        List<Round> rounds,
        List<Fixture> fixtures,
        List<CupTie> ties)
    {
        if (entrants.Count < 2 || entrants.Count % 2 != 0)
        {
            return;
        }

        // Completely random draw for the first round (no seeding)
        var random = new Random();
        var pairings = CupQualification.RandomPairings(entrants, random);
        var (firstLegDay, secondLegDay) = CompetitionRules.CupLegMatchDays(1);

        if (!matchDays.TryGetValue(firstLegDay, out var firstMatchDay)
            || !matchDays.TryGetValue(secondLegDay, out var secondMatchDay))
        {
            return;
        }

        var firstRound = Round.Create(view.Id, CompetitionRules.CupWindowNumber(1, 1), CompetitionRules.CupWindow);
        firstRound.ScheduleOn(firstMatchDay.Id);
        rounds.Add(firstRound);

        var secondRound = Round.Create(view.Id, CompetitionRules.CupWindowNumber(1, 2), CompetitionRules.CupWindow);
        secondRound.ScheduleOn(secondMatchDay.Id);
        rounds.Add(secondRound);

        foreach (var (home, away) in pairings)
        {
            var firstLeg = Fixture.Create(firstRound.Id, home, away);
            var secondLeg = Fixture.Create(secondRound.Id, away, home);

            fixtures.Add(firstLeg);
            fixtures.Add(secondLeg);

            var tie = CupTie.Create(view.Id, 1, home, away);
            tie.SetLegs(firstLeg.Id, secondLeg.Id);
            ties.Add(tie);
        }
    }

    /// <summary>
    /// The Supercup: one match, in the first window of the first day, where nothing else is
    /// playing. The championship opens on day two and the cup's first leg is not until the
    /// seventh, so day one belongs to the two clubs that won last season's two competitions
    /// and to nobody else.
    /// </summary>
    private static void DrawSuperCup(
        CompetitionSeasonView view,
        IReadOnlyList<Guid> entrants,
        IReadOnlyDictionary<int, MatchDay> matchDays,
        List<Round> rounds,
        List<Fixture> fixtures)
    {
        if (entrants.Count < 2 || !matchDays.TryGetValue(CompetitionRules.SuperCupMatchDay, out var matchDay))
        {
            return;
        }

        var round = Round.Create(view.Id, 1, CompetitionRules.SuperCupWindow);
        round.ScheduleOn(matchDay.Id);
        rounds.Add(round);

        fixtures.Add(Fixture.Create(round.Id, entrants[0], entrants[1]));
    }

    /// <summary>
    /// The sixty-four clubs in the cup, in seed order.
    ///
    /// The last finished season's tables decide it, and that is the rule from the second
    /// season on. A brand new world has no such tables — nothing has been played — and then
    /// the pyramid and the squads themselves are the only things that are known: the clubs are
    /// ordered by the division they are in, and inside a division by the strength of the
    /// squad on their books. The four left out are the weakest clubs of the weakest division.
///
/// That is a weaker rule than a table, and it is said out loud rather than pretended
/// away: a first cup drawn from a first season is drawn from what the world knows, and
/// every season after it is drawn from a table.
/// </summary>
    private async Task<IReadOnlyList<Guid>> GetAllClubsForCupAsync(
        CompetitionSeasonView view,
        CancellationToken cancellationToken)
    {
        var views = await _competitionRepository.ListSeasonViewsAsync(view.SeasonId, cancellationToken);

        var byTier = new Dictionary<int, IReadOnlyList<StandingEntry>>();

        foreach (var tierView in views.Where(candidate => candidate.Tier is not null))
        {
            var collection = await _standingsService.CollectAsync(tierView.Id, tierView, cancellationToken);

            if (collection.Seeds.Count == 0) continue;

            byTier[tierView.Tier!.Value] = StandingTable.Build(collection.Seeds, collection.Finished);
        }

        if (byTier.Count == 0)
        {
            // Nothing in the pyramid to seed the cup from, and a cup with no draw is not a cup
            // that is waiting to be drawn: it is a season whose calendar is missing a
            // competition, and saying so is the only honest answer.
            throw new DomainValidationException(
                "CupCannotBeDrawn",
                "A copa é sorteada a partir do campeonato, e a temporada não tem nenhuma divisão " +
                "com clubes para servir de semente.");
        }

        // Get all 64 clubs for the cup (completely random draw, no seeding)
        return CupQualification.GetAllClubsForCup(byTier);
    }
}
