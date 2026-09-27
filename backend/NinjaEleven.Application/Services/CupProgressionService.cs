using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Services;

/// <summary>
/// The cup, as it actually runs.
///
/// A cup bracket cannot be drawn all at once, and the reason is not a limitation of the code —
/// it is what a knockout is. Nobody knows who is in the quarter-finals before the round of 16 has
/// been played, and pretending otherwise is how you end up with a final between two clubs that
/// were knocked out in the first round. So the calendar draws the first round and nothing else,
/// and this service draws each following round at the moment the round before it is decided.
///
/// It runs on the finish of a match, not on a timer, because a bracket that advances on a clock
/// is a bracket that can be ahead of the football: the quarter-finals would be drawn before the
/// last second leg of the round of 16 was played. The moment the final leg of a round is over,
/// the round is complete, and the next one exists in the same breath.
///
/// Nothing here decides a football match. It reads a result the engine has already settled and
/// turns it into the next round, which is why it is a service of its own and not a step inside
/// the match loop: a cup tie is two matches, and the thing that spans them is not a match.
/// </summary>
public class CupProgressionService
{
    private readonly ICupTieRepository _cupTieRepository;
    private readonly ITrophyRepository _trophyRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IMatchDayRepository _matchDayRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CupProgressionService(
        ICupTieRepository cupTieRepository,
        ITrophyRepository trophyRepository,
        IMatchRepository matchRepository,
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        IMatchDayRepository matchDayRepository,
        ICompetitionRepository competitionRepository,
        IUnitOfWork unitOfWork)
    {
        _cupTieRepository = cupTieRepository;
        _trophyRepository = trophyRepository;
        _matchRepository = matchRepository;
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _matchDayRepository = matchDayRepository;
        _competitionRepository = competitionRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Settles the tie this match was a leg of, and draws whatever comes next.
    ///
    /// It is safe to call for every match in the pyramid, and it returns quietly for the ones
    /// that are not part of a tie: a championship match is not a cup match, and a first leg is
    /// not a decision.
    /// </summary>
    public async Task AdvanceAsync(CupLegOutcome outcome, CancellationToken cancellationToken = default)
    {
        var tie = await _cupTieRepository.GetByLegAsync(outcome.FixtureId, cancellationToken);

        // Not a cup leg, or already settled. A tie is decided once, and a replayed second leg
        // has to land on the same answer rather than argue with the first one.
        if (tie is null || tie.IsResolved)
        {
            return;
        }

        // The first leg is half a tie. Nothing is decided until the second one is over.
        if (tie.SecondLegFixtureId != outcome.FixtureId || tie.FirstLegFixtureId is null)
        {
            return;
        }

        var firstLeg = await _matchRepository.GetByFixtureAsync(tie.FirstLegFixtureId.Value, cancellationToken);
        if (firstLeg is null || !firstLeg.IsFinished)
        {
            return;
        }

        ResolveTie(tie, firstLeg, outcome);

        // Its own writes are committed before the round is read back, because the round is
        // read untracked and a tie that has only been decided in memory still reads as
        // undecided. A service that decided the last tie of a round and then read the round
        // back before saving would see that tie as outstanding, decide the round was still
        // being played, and never draw another one — a cup that stops after two matchdays
        // with nothing to say for it, and no error anywhere to explain why.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roundTies = await _cupTieRepository.ListByRoundAsync(
            tie.CompetitionSeasonId, tie.RoundNumber, cancellationToken);

        // The round is only over when every tie in it is. One outstanding tie means the
        // quarter-finals cannot be drawn, because a team that is still playing in the round of
        // 16 might be in them.
        if (roundTies.Any(pending => !pending.IsResolved))
        {
            return;
        }

        var survivors = CupQualification.SurvivorsAfter(tie.RoundNumber);

        if (survivors == 1)
        {
            await AwardChampionAsync(tie, cancellationToken);
            return;
        }

        await DrawNextRoundAsync(tie, roundTies, cancellationToken);
    }

    /// <summary>
    /// Adds the two legs together and decides the tie, going to penalties when they are level.
    ///
    /// The aggregate is read across the two legs by club rather than by side, because the legs
    /// swap ends: the club that was at home in the first leg is away in the second one, and an
    /// aggregate added by side would count one of the two results the wrong way round.
    /// </summary>
    private void ResolveTie(CupTie tie, Match firstLeg, CupLegOutcome secondLeg)
    {
        // The second leg is the mirror of the first, so this leg's home score is the first
        // leg's away club's contribution to the aggregate.
        var aggregateHome = firstLeg.HomeScore + secondLeg.AwayScore;
        var aggregateAway = firstLeg.AwayScore + secondLeg.HomeScore;

        PenaltyShootout? shootout = null;

        if (aggregateHome == aggregateAway)
        {
            // Level. There is no extra time in a cup tie, so the tie goes to penalties, taken
            // by the eleven that played the second leg — the same men who would have taken them
            // in the stadium.
            var home = tie.HomeTeamId;
            var away = tie.AwayTeamId;

            shootout = PenaltyShootout.Simulate(
                home,
                away,
                ConversionFor(secondLeg, home),
                ConversionFor(secondLeg, away),
                new DeterministicRandomSource(secondLeg.Seed));
        }

        tie.Resolve(aggregateHome, aggregateAway, shootout);
        _cupTieRepository.Update(tie);
    }

    /// <summary>
    /// How likely a club's man is to score from twelve yards, as the engine already measures
    /// it: the taker's accuracy and control against the keeper's reflexes and power.
    /// </summary>
    private static double ConversionFor(CupLegOutcome outcome, Guid teamId)
    {
        if (!outcome.Takers.TryGetValue(teamId, out var penalty) || penalty.Taker is null)
        {
            // A club that sent nobody out cannot win a shootout by luck, and a tie between two
            // sides with no taker is a tie the shootout itself has to resolve, so the floor of
            // the engine's own conversion is the honest answer.
            return 0.75;
        }

        return MatchEngine.PenaltyConversion(penalty.Taker, penalty.Keeper);
    }

    /// <summary>
    /// Draws the round after the one that has just been decided: two windows, the two legs of
    /// each new tie, and the ties themselves, on the matchdays the rules put them.
    /// </summary>
    private async Task DrawNextRoundAsync(
        CupTie decided,
        IReadOnlyCollection<CupTie> roundTies,
        CancellationToken cancellationToken)
    {
        // The round has to be the shape a cup of this size has: a round of sixteen holds
        // sixteen ties, a quarter-final holds eight. Anything else is a bracket that does not
        // add up, and it is left alone rather than halved — the domain refuses to pair an odd
        // number of ties, and an exception escaping from here would land inside the tick of a
        // match that had already finished, so a bracket that cannot be drawn is not allowed to
        // take the football down with it.
        var expectedTies = CupQualification.SurvivorsAfter(decided.RoundNumber);

        if (roundTies.Count != expectedTies)
        {
            return;
        }

        var nextRoundNumber = decided.RoundNumber + 1;
        var pairings = CupBracket.NextRoundPairings(roundTies);

        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(
            decided.CompetitionSeasonId, cancellationToken);

        if (competitionSeason is null)
        {
            return;
        }

        var matchDays = (await _matchDayRepository.ListBySeasonAsync(
                competitionSeason.SeasonId, cancellationToken))
            .ToDictionary(matchDay => matchDay.Number);

        var (firstLegDay, secondLegDay) = CompetitionRules.CupLegMatchDays(nextRoundNumber);

        // A round is only drawn if the calendar has a matchday for each of its legs. Skipping
        // it rather than guessing a date is the honest answer: a tie on a matchday that does
        // not exist is a match nobody can play.
        if (!matchDays.TryGetValue(firstLegDay, out var firstMatchDay)
            || !matchDays.TryGetValue(secondLegDay, out var secondMatchDay))
        {
            return;
        }

        var firstLegRound = Round.Create(
            decided.CompetitionSeasonId,
            CompetitionRules.CupWindowNumber(nextRoundNumber, 1),
            CompetitionRules.CupWindow);
        firstLegRound.ScheduleOn(firstMatchDay.Id);
        await _roundRepository.AddAsync(firstLegRound, cancellationToken);

        var secondLegRound = Round.Create(
            decided.CompetitionSeasonId,
            CompetitionRules.CupWindowNumber(nextRoundNumber, 2),
            CompetitionRules.CupWindow);
        secondLegRound.ScheduleOn(secondMatchDay.Id);
        await _roundRepository.AddAsync(secondLegRound, cancellationToken);

        var fixtures = new List<Fixture>();
        var ties = new List<CupTie>();

        foreach (var (home, away) in pairings)
        {
            var firstLeg = Fixture.Create(firstLegRound.Id, home, away);
            var secondLeg = Fixture.Create(secondLegRound.Id, away, home);

            fixtures.Add(firstLeg);
            fixtures.Add(secondLeg);

            var tie = CupTie.Create(decided.CompetitionSeasonId, nextRoundNumber, home, away);
            tie.SetLegs(firstLeg.Id, secondLeg.Id);
            ties.Add(tie);
        }

        await _fixtureRepository.AddRangeAsync(fixtures, cancellationToken);
        await _cupTieRepository.AddRangeAsync(ties, cancellationToken);

        // The next round is the last thing this service writes, and it is written the moment
        // the round before it is decided rather than at the end of the season or on a sweep.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Puts the cup on the winner's shelf.
    ///
    /// It is written rather than worked out from the table later, for the same reason a table is
    /// kept: a club that is relegated after winning the cup still won it, and a season's
    /// history has to be able to say so.
    /// </summary>
    private async Task AwardChampionAsync(
        CupTie final,
        CancellationToken cancellationToken)
    {
        var championId = CupBracket.ChampionOf(final);

        var seasonId = await ResolveSeasonIdAsync(final.CompetitionSeasonId, cancellationToken);

        var alreadyAwarded = await _trophyRepository.ListBySeasonAsync(seasonId, cancellationToken);

        if (alreadyAwarded.Any(trophy => trophy.CompetitionSeasonId == final.CompetitionSeasonId))
        {
            // The final is decided once. Two trophies for one cup would be a shelf that lies.
            return;
        }

        var awards = new List<TrophyAward>
        {
            TrophyAward.Create(
                championId,
                seasonId,
                final.CompetitionSeasonId,
                null,
                TrophyKind.Champion)
        };

        // The other side of the final is the runner-up, and a final that went to penalties has
        // one exactly as much as a final that did not.
        if (final.LoserTeamId is not null)
        {
            awards.Add(TrophyAward.Create(
                final.LoserTeamId.Value,
                seasonId,
                final.CompetitionSeasonId,
                null,
                TrophyKind.RunnerUp));
        }

        await _trophyRepository.AddRangeAsync(awards, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> ResolveSeasonIdAsync(Guid competitionSeasonId, CancellationToken cancellationToken)
    {
        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(
            competitionSeasonId, cancellationToken);

        return competitionSeason?.SeasonId
            ?? throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);
    }
}
