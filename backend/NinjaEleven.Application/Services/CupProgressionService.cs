using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Matches;
using System;

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
    private readonly FinanceService _finance;
    private readonly Random _random;

    public CupProgressionService(
        ICupTieRepository cupTieRepository,
        ITrophyRepository trophyRepository,
        IMatchRepository matchRepository,
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        IMatchDayRepository matchDayRepository,
        ICompetitionRepository competitionRepository,
        IUnitOfWork unitOfWork,
        FinanceService finance,
        Random random)
    {
        _cupTieRepository = cupTieRepository;
        _trophyRepository = trophyRepository;
        _matchRepository = matchRepository;
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _matchDayRepository = matchDayRepository;
        _competitionRepository = competitionRepository;
        _unitOfWork = unitOfWork;
        _finance = finance;
        _random = random;
    }

    /// <summary>
    /// The tie a fixture is a leg of, or null when it is not one.
    ///
    /// It is asked twice in a tie and by two callers: the cup asks for it to settle the tie
    /// when a leg finishes, and the match asks for it at kick-off so the engine knows what
    /// it is trying to settle. Both answers come from here, so a second leg that arrives
    /// without its first leg played is refused in the same place either way.
    /// </summary>
    public async Task<CupTie?> GetTieForLegAsync(
        Guid fixtureId,
        CancellationToken cancellationToken = default) =>
        await _cupTieRepository.GetByLegAsync(fixtureId, cancellationToken);

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

        // The club that goes out is paid for having gone out, in the round it went out in.
        // It is written here, on the tie, because the tie is the thing that knows which round
        // this was and which club lost it — and the final is not paid here: it is paid by
        // AwardChampionAsync, which is where the winner's money is, and a final in which the
        // two sides are paid from two different places is a final where one of them is not.
        if (tie.LoserTeamId is { } loserId
            && CupQualification.SurvivorsAfter(tie.RoundNumber) > 1)
        {
            await PayTheConsolationAsync(tie, loserId, cancellationToken);
        }

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

        await DrawNextRoundAsync(tie, roundTies, _random, cancellationToken);
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

        ShootoutOutcome? shootout = null;

        if (aggregateHome == aggregateAway)
        {
            // Level. There is no extra time in a cup tie, so the tie goes to penalties — and
            // the penalties are the ones the match itself played, with the eleven that
            // finished it and the order the two managers named. The cup does not take them
            // again: it reads the result it was given.
            var taken = secondLeg.Shootout ?? throw new InvalidOperationException(
                "The aggregate is level, so this leg has to have gone to a shootout for the tie to be settled.");

            // The shootout counts the leg's sides and the tie counts the clubs. The two legs
            // swap ends, so the leg's home is the tie's away: read the penalties by club or
            // the tie keeps the other club's kicks and sends the winner out of its own cup.
            var legHomeIsTieHome = secondLeg.HomeTeamId == tie.HomeTeamId;

            shootout = new ShootoutOutcome(
                HomeGoals: legHomeIsTieHome ? taken.HomeGoals : taken.AwayGoals,
                AwayGoals: legHomeIsTieHome ? taken.AwayGoals : taken.HomeGoals,
                WinnerTeamId: taken.WinnerTeamId,
                IsSuddenDeath: taken.IsSuddenDeath);
        }

        tie.Resolve(aggregateHome, aggregateAway, shootout);
        _cupTieRepository.Update(tie);
    }

    /// <summary>
    /// Draws the round after the one that has just been decided: two windows, the two legs of
    /// each new tie, and the ties themselves, on the matchdays the rules put them.
    /// </summary>
    private async Task DrawNextRoundAsync(
        CupTie decided,
        IReadOnlyCollection<CupTie> roundTies,
        Random random,
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
        var pairings = CupBracket.NextRoundPairings(roundTies, random);

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

        // The two cheques a final pays: the cup to the club that won it, and three million to
        // the club that lost it. A runner-up is paid at a rate no other round pays, because a
        // final is the one round a club loses having been the last one standing.
        await PayAPrizeAsync(
            championId,
            seasonId,
            PrizeRules.CupChampionPrize,
            $"Campeão da copa",
            $"cup:{final.RoundNumber}:champion",
            cancellationToken);

        if (final.LoserTeamId is { } runnerUpId)
        {
            await PayAPrizeAsync(
                runnerUpId,
                seasonId,
                PrizeRules.CupFinalLoser,
                $"Vice da copa",
                $"cup:{final.RoundNumber}:runner-up",
                cancellationToken);
        }
    }

    /// <summary>
    /// Pays a club what it is owed for the round it went out in.
    /// </summary>
    private async Task PayTheConsolationAsync(
        CupTie tie,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var seasonId = await ResolveSeasonIdAsync(tie.CompetitionSeasonId, cancellationToken);
        var amount = PrizeRules.CupConsolation(tie.RoundNumber);
        var round = CompetitionRules.TieRoundName(tie.RoundNumber);

        await PayAPrizeAsync(
            teamId,
            seasonId,
            amount,
            $"Eliminado na fase de {round} da copa",
            $"cup:{tie.RoundNumber}:loser",
            cancellationToken);
    }

    /// <summary>
    /// One prize, written to a club's book if it has not already been written.
    ///
    /// The reference is what makes it once: a tie that is settled twice — a replayed second
    /// leg, a season closed again — pays the same consolation once, because the second
    /// settlement is asking about the same round of the same cup and not about a new payment.
    /// </summary>
    private async Task PayAPrizeAsync(
        Guid teamId,
        Guid seasonId,
        decimal amount,
        string description,
        string reference,
        CancellationToken cancellationToken) =>
        await _finance.RecordPrizeAsync(
            teamId,
            seasonId,
            FinanceMovementKind.PrizeMoney,
            description,
            amount,
            reference,
            cancellationToken);

    private async Task<Guid> ResolveSeasonIdAsync(Guid competitionSeasonId, CancellationToken cancellationToken)
    {
        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(
            competitionSeasonId, cancellationToken);

        return competitionSeason?.SeasonId
            ?? throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);
    }
}
