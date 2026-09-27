using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Works out who is in the stand before a match starts.
///
/// Everything it needs it reads off the fixture's own competition — the tier the home club is
/// in, where it stands, how strong the division is, how far through the season it is — so the
/// crowd is a fact about the match rather than a number the caller chose. A crowd decided at
/// the call site would be a crowd that is right on the manager's match and wrong on the other
/// eleven, and the twelve would disagree about what a full house is.
/// </summary>
public class AttendanceContextFactory
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly StandingsService _standingsService;

    public AttendanceContextFactory(
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        ICompetitionRepository competitionRepository,
        StandingsService standingsService)
    {
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _competitionRepository = competitionRepository;
        _standingsService = standingsService;
    }

    /// <summary>
    /// The context for a fixture's crowd.
    ///
    /// A fixture that is not in any competition — a friendly, a replayed fixture whose round
    /// has gone — still has a crowd. It gets the neutral one: the top tier, a mid-table club,
    /// an average opponent, a match of ordinary importance. Guessing is worse than admitting
    /// that nothing is known, and nothing here throws a match out of existence over a table it
    /// could not be found in.
    /// </summary>
    public async Task<AttendanceContext> ForFixtureAsync(
        Guid fixtureId,
        Guid homeTeamId,
        Guid awayTeamId,
        CompetitionType competitionType,
        double homeSquadStars,
        double awaySquadStars,
        CancellationToken cancellationToken = default)
    {
        var round = await ResolveRoundAsync(fixtureId, cancellationToken);
        if (round is null)
        {
            return new AttendanceContext(
                Tier: 1,
                HomePosition: NeutralPosition,
                ClubsInDivision: 12,
                HomeSquadStars: homeSquadStars,
                AwaySquadStars: awaySquadStars,
                DivisionAverageStars: Math.Max(homeSquadStars, awaySquadStars),
                Matchday: 1,
                TotalMatchdays: CompetitionRules.LeagueMatchDays,
                Importance: MatchImportanceRules.ByCompetition(competitionType));
        }

        var view = await _competitionRepository.GetSeasonByIdAsync(round.CompetitionSeasonId, cancellationToken) is { } season
            ? await _competitionRepository.GetSeasonViewByIdAsync(season.Id, cancellationToken)
            : null;

        if (view is null)
        {
            return await FallbackAsync(homeSquadStars, awaySquadStars, competitionType, cancellationToken);
        }

        var collection = await _standingsService.CollectAsync(view.Id, view, cancellationToken);

        var totalMatchdays = await CountMatchdaysAsync(round.CompetitionSeasonId, view.SeasonId, cancellationToken);
        var homePosition = collection.PositionOf(homeTeamId);
        var awayPosition = collection.PositionOf(awayTeamId);

        return new AttendanceContext(
            Tier: view.Tier ?? 1,
            HomePosition: homePosition == 0 ? NeutralPosition : homePosition,
            ClubsInDivision: Math.Max(collection.ClubsInDivision, 1),
            HomeSquadStars: homeSquadStars,
            AwaySquadStars: awaySquadStars,
            DivisionAverageStars: collection.AverageStars,
            Matchday: Math.Max(round.Number, 1),
            TotalMatchdays: Math.Max(totalMatchdays, 1),
            Importance: ImportanceFor(
                competitionType,
                view,
                homePosition == 0 ? NeutralPosition : homePosition,
                awayPosition == 0 ? NeutralPosition : awayPosition,
                collection.ClubsInDivision,
                totalMatchdays,
                round.Number));
    }

    /// <summary>
    /// The matchday whose date covers the fixture's competition season, so a cup tie is
    /// measured against the calendar it is played in rather than against its own knockout
    /// round. A cup has five rounds and a stadium fills up over twenty-two matchdays; the two
    /// are not the same shape and averaging them produces a season progress that is neither.
    /// </summary>
    private async Task<int> CountMatchdaysAsync(
        Guid competitionSeasonId,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var rounds = await _roundRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);
        var numbers = rounds.Select(round => round.Number).Distinct().ToList();

        return numbers.Count == 0 ? CompetitionRules.LeagueMatchDays : numbers.Count;
    }

    private static MatchImportance ImportanceFor(
        CompetitionType competitionType,
        CompetitionSeasonView view,
        int homePosition,
        int awayPosition,
        int clubsInDivision,
        int totalMatchdays,
        int roundNumber)
    {
        // A competition that is not a league has nothing to be a race in, so the competition
        // alone decides it. A cup tie is a tie; a Supercup is one match that cannot be replayed.
        if (view.IsDivision is false || competitionType != CompetitionType.League)
        {
            return MatchImportanceRules.ByCompetition(competitionType);
        }

        var remaining = Math.Max(totalMatchdays - roundNumber, 0);

        return MatchImportanceRules.ForLeagueMatch(
            homePosition,
            awayPosition,
            clubsInDivision,
            remaining);
    }

    private async Task<AttendanceContext> FallbackAsync(
        double homeSquadStars,
        double awaySquadStars,
        CompetitionType competitionType,
        CancellationToken cancellationToken) =>
        new(
            Tier: 1,
            HomePosition: NeutralPosition,
            ClubsInDivision: 12,
            HomeSquadStars: homeSquadStars,
            AwaySquadStars: awaySquadStars,
            DivisionAverageStars: Math.Max(homeSquadStars, awaySquadStars),
            Matchday: 1,
            TotalMatchdays: CompetitionRules.LeagueMatchDays,
            Importance: MatchImportanceRules.ByCompetition(competitionType));

    private async Task<Round?> ResolveRoundAsync(Guid fixtureId, CancellationToken cancellationToken)
    {
        var fixture = await _fixtureRepository.GetAsync(fixtureId, cancellationToken);
        return fixture is null ? null : await _roundRepository.GetAsync(fixture.RoundId, cancellationToken);
    }

    /// <summary>
    /// The place a club is given when nothing is known about it. It is the middle of a
    /// twelve-club division, because a club nobody has a position for is a club nobody has
    /// said anything about, and the middle is the honest guess.
    /// </summary>
    private const int NeutralPosition = 6;
}
