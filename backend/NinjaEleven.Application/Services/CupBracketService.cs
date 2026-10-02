using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// A cup read as a bracket, for a screen that shows one.
///
/// Nothing here decides a cup: the ties are the ones <see cref="CupProgressionService"/> drew and
/// settled, and this service only reads them — which clubs were in each tie, what the two legs
/// finished on, and which side went through. The one thing it works out for itself is the
/// per-club line of each tie, because a leg is a score with a home and an away and the two legs
/// swap ends: the aggregate of a tie is the sum of its home club's first-leg goals and its
/// second-leg goals, and a bracket that added the two columns as they arrived would show a club
/// winning a tie it lost.
///
/// A bracket is also incomplete by design, and this is what makes it a bracket rather than a
/// guess: the round after a tie is decided has not been drawn yet, so it is simply not in the
/// answer. A screen that drew the quarter-finals in advance would be showing two clubs in them
/// that neither has earned a place in.
/// </summary>
public class CupBracketService
{
    private readonly ICupTieRepository _cupTieRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ICompetitionRepository _competitionRepository;

    public CupBracketService(
        ICupTieRepository cupTieRepository,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        ICompetitionRepository competitionRepository)
    {
        _cupTieRepository = cupTieRepository;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _competitionRepository = competitionRepository;
    }

    /// <summary>
    /// The bracket of one edition, with the rounds that have been drawn, oldest first.
    /// </summary>
    /// <param name="competitionSeasonId">The cup's edition inside a season.</param>
    public async Task<CupBracketView?> GetAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _competitionRepository.GetSeasonByIdAsync(competitionSeasonId, cancellationToken);
        if (edition is null)
        {
            return null;
        }

        var competition = await _competitionRepository.GetAsync(edition.CompetitionId, cancellationToken);

        var ties = await _cupTieRepository.ListByCompetitionSeasonAsync(competitionSeasonId, cancellationToken);
        if (ties.Count == 0)
        {
            // A cup whose first round has not been drawn has no bracket to show. The screen is
            // told so by an empty list rather than by an error, because a season a few days old
            // is a season that is working, not a season that has failed.
            return new CupBracketView
            {
                CompetitionSeasonId = competitionSeasonId,
                SeasonId = edition.SeasonId,
                CompetitionName = competition?.Name ?? string.Empty
            };
        }

        var matches = await _matchRepository.ListByFixtureIdsAsync(LegFixtureIdsOf(ties), cancellationToken);

        // A fixture can hold an abandoned match and a replay, and the rows come back in the order
        // they were created. The bracket is a reading of the match of a leg, not of every match
        // that leg has ever had: the last row per fixture is that match, and an abandoned 3-0
        // that was replayed is not a leg of this cup.
        var matchByFixture = matches
            .GroupBy(match => match.FixtureId)
            .ToDictionary(group => group.Key, group => group.Last());

        var clubIds = ties
            .SelectMany(tie => new[] { tie.HomeTeamId, tie.AwayTeamId })
            .Distinct();
        var clubs = (await _teamRepository.ListByIdsAsync(clubIds, cancellationToken)).ToDictionary(team => team.Id);

        var rounds = ties
            .GroupBy(tie => tie.RoundNumber)
            .OrderBy(round => round.Key)
            .Select(round => new CupBracketRound
            {
                RoundNumber = round.Key,
                Name = CompetitionRules.TieRoundName(round.Key),
                Ties = round
                    .Select(tie => ToTieDto(tie, matchByFixture, clubs))
                    .ToList()
            })
            .ToList();

        var final = ties
            .Where(tie => tie.RoundNumber == CompetitionRules.CupRounds)
            .FirstOrDefault(tie => tie.IsResolved);

        return new CupBracketView
        {
            CompetitionSeasonId = competitionSeasonId,
            SeasonId = edition.SeasonId,
            CompetitionName = competition?.Name ?? string.Empty,
            Rounds = rounds,
            ChampionTeamId = final?.WinnerTeamId,
            ChampionTeamName = final?.WinnerTeamId is { } champion ? NameOf(clubs, champion) : null,
            RunnerUpTeamName = final?.LoserTeamId is { } runnerUp ? NameOf(clubs, runnerUp) : null
        };
    }

    /// <summary>
    /// One tie as a bracket shows it: the home club of the first leg first, then the other one,
    /// each carrying its own two legs and the aggregate it finished on.
    /// </summary>
    private static CupBracketTie ToTieDto(
        CupTie tie,
        IReadOnlyDictionary<Guid, Match> matchByFixture,
        IReadOnlyDictionary<Guid, Team> clubs)
    {
        var firstLeg = LegMatchOf(tie, matchByFixture, tie.FirstLegFixtureId);
        var secondLeg = LegMatchOf(tie, matchByFixture, tie.SecondLegFixtureId);

        // The legs swap ends, so the second leg's home is the tie's away. Reading each leg's
        // score by club is the only way round this that is true for both of them.
        (int Goals, int Conceded)? firstForHome = firstLeg is null ? null : ScoredBy(firstLeg, tie.HomeTeamId);
        (int Goals, int Conceded)? secondForHome = secondLeg is null ? null : ScoredBy(secondLeg, tie.HomeTeamId);

        return new CupBracketTie
        {
            TieId = tie.Id,
            RoundNumber = tie.RoundNumber,
            FirstLegScore = firstLeg is null ? null : firstLeg.HomeScore,
            SecondLegScore = secondLeg is null ? null : secondLeg.HomeScore,
            FirstLegMatchId = firstLeg?.Id,
            SecondLegMatchId = secondLeg?.Id,
            FirstLegLive = firstLeg?.IsLive ?? false,
            SecondLegLive = secondLeg?.IsLive ?? false,
            Clubs =
            [
                ClubLine(
                    clubs, tie.HomeTeamId,
                    firstForHome, secondForHome,
                    tie.AggregateHomeGoals, tie.AggregateAwayGoals, tie.HomePenaltyGoals,
                    tie.WinnerTeamId == tie.HomeTeamId, tie.LoserTeamId == tie.HomeTeamId),
                ClubLine(
                    clubs, tie.AwayTeamId,
                    firstForHome is null ? null : (firstForHome.Value.Conceded, firstForHome.Value.Goals),
                    secondForHome is null ? null : (secondForHome.Value.Conceded, secondForHome.Value.Goals),
                    tie.AggregateAwayGoals, tie.AggregateHomeGoals, tie.AwayPenaltyGoals,
                    tie.WinnerTeamId == tie.AwayTeamId, tie.LoserTeamId == tie.AwayTeamId)
            ]
        };
    }

    /// <summary>
    /// One club's line. The leg scores are the club's own goals and the goals it let in, and the
    /// aggregate is the tie's, which the cup settled and this service only reads.
    /// </summary>
    private static CupBracketClub ClubLine(
        IReadOnlyDictionary<Guid, Team> clubs,
        Guid teamId,
        (int Goals, int Conceded)? firstLeg,
        (int Goals, int Conceded)? secondLeg,
        int? aggregateGoals,
        int? aggregateConceded,
        int? penaltyGoals,
        bool isWinner,
        bool isLoser)
    {
        clubs.TryGetValue(teamId, out var club);

        return new CupBracketClub
        {
            TeamId = teamId,
            Name = club?.Name ?? string.Empty,
            PrimaryColor = club?.PrimaryColor ?? string.Empty,
            SecondaryColor = club?.SecondaryColor ?? string.Empty,
            FirstLegGoals = firstLeg?.Goals,
            FirstLegConceded = firstLeg?.Conceded,
            SecondLegGoals = secondLeg?.Goals,
            SecondLegConceded = secondLeg?.Conceded,
            AggregateGoals = aggregateGoals,
            AggregateConceded = aggregateConceded,
            PenaltyGoals = penaltyGoals,
            IsWinner = isWinner,
            IsLoser = isLoser
        };
    }

    private static (int Goals, int Conceded) ScoredBy(Match leg, Guid teamId) =>
        leg.HomeTeamId == teamId
            ? (leg.HomeScore, leg.AwayScore)
            : (leg.AwayScore, leg.HomeScore);

    /// <summary>
    /// The match of a leg, and only when the leg has been played or is live. A leg that is
    /// scheduled has a fixture and no match, and a bracket that read its 0 x 0 as a result
    /// would show a tie of nil-nil as though two clubs had gone out to it. A leg that is
    /// in progress is returned so the screen can offer a link to watch it.
    /// </summary>
    private static Match? LegMatchOf(
        CupTie tie,
        IReadOnlyDictionary<Guid, Match> matchByFixture,
        Guid? fixtureId) =>
        fixtureId is { } id
        && matchByFixture.TryGetValue(id, out var match)
        && (match.IsFinished || match.IsLive)
            ? match
            : null;

    private static IEnumerable<Guid> LegFixtureIdsOf(IEnumerable<CupTie> ties) =>
        ties
            .SelectMany(tie => new[] { tie.FirstLegFixtureId, tie.SecondLegFixtureId })
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct();

    private static string NameOf(IReadOnlyDictionary<Guid, Team> clubs, Guid teamId) =>
        clubs.TryGetValue(teamId, out var club) ? club.Name : string.Empty;
}
