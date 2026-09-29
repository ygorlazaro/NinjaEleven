using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Service for drawing cup ties. All draws are completely random with no seeding.
/// </summary>
public class CupDrawService
{
    private readonly ICupTieRepository _cupTieRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IMatchDayRepository _matchDayRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CupDrawService(
        ICupTieRepository cupTieRepository,
        IRoundRepository roundRepository,
        IFixtureRepository fixtureRepository,
        IMatchDayRepository matchDayRepository,
        ICompetitionRepository competitionRepository,
        IUnitOfWork unitOfWork)
    {
        _cupTieRepository = cupTieRepository;
        _roundRepository = roundRepository;
        _fixtureRepository = fixtureRepository;
        _matchDayRepository = matchDayRepository;
        _competitionRepository = competitionRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Draws the first round of the cup (round of 64) with all 64 clubs.
    /// </summary>
    public async Task DrawFirstRoundAsync(
        Guid cupEditionId,
        IReadOnlyList<Guid> allClubIds,
        Random random,
        CancellationToken cancellationToken = default)
    {
        if (allClubIds.Count != 64)
        {
            throw new ArgumentException($"Cup must have exactly 64 clubs, got {allClubIds.Count}");
        }

        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(cupEditionId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", cupEditionId);

        var matchDays = (await _matchDayRepository.ListBySeasonAsync(
            competitionSeason.SeasonId, cancellationToken))
            .ToDictionary(md => md.Number);

        // Round 1: 32nd finals (64 -> 32)
        var (firstLegDay, secondLegDay) = CompetitionRules.CupLegMatchDays(1);
        
        if (!matchDays.TryGetValue(firstLegDay, out var firstMatchDay)
            || !matchDays.TryGetValue(secondLegDay, out var secondMatchDay))
        {
            throw new InvalidOperationException("Cup matchdays not found for first round");
        }

        var firstLegRound = Round.Create(
            cupEditionId,
            CompetitionRules.CupWindowNumber(1, 1),
            CompetitionRules.CupWindow);
        firstLegRound.ScheduleOn(firstMatchDay.Id);
        await _roundRepository.AddAsync(firstLegRound, cancellationToken);

        var secondLegRound = Round.Create(
            cupEditionId,
            CompetitionRules.CupWindowNumber(1, 2),
            CompetitionRules.CupWindow);
        secondLegRound.ScheduleOn(secondMatchDay.Id);
        await _roundRepository.AddAsync(secondLegRound, cancellationToken);

        // Random pairings
        var pairings = CupQualification.RandomPairings(allClubIds, random);

        var fixtures = new List<Fixture>();
        var ties = new List<CupTie>();

        foreach (var (home, away) in pairings)
        {
            var firstLeg = Fixture.Create(firstLegRound.Id, home, away);
            var secondLeg = Fixture.Create(secondLegRound.Id, away, home);

            fixtures.Add(firstLeg);
            fixtures.Add(secondLeg);

            var tie = CupTie.Create(cupEditionId, 1, home, away);
            tie.SetLegs(firstLeg.Id, secondLeg.Id);
            ties.Add(tie);
        }

        await _fixtureRepository.AddRangeAsync(fixtures, cancellationToken);
        await _cupTieRepository.AddRangeAsync(ties, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Draws the next round of the cup from the winners of the previous round.
    /// Completely random draw each round.
    /// </summary>
    public async Task<bool> DrawNextRoundAsync(
        Guid cupEditionId,
        int completedRoundNumber,
        Random random,
        CancellationToken cancellationToken = default)
    {
        var nextRoundNumber = completedRoundNumber + 1;
        
        if (nextRoundNumber > CompetitionRules.CupRounds)
        {
            return false; // Cup is complete
        }

        // Get all ties from the completed round
        var roundTies = await _cupTieRepository.ListByRoundAsync(cupEditionId, completedRoundNumber, cancellationToken);
        
        // Verify all ties are resolved
        if (roundTies.Any(t => !t.IsResolved))
        {
            return false; // Round not complete yet
        }

        var expectedSurvivors = CupQualification.SurvivorsAfter(completedRoundNumber);
        if (roundTies.Count != expectedSurvivors)
        {
            return false; // Wrong number of ties
        }

        var competitionSeason = await _competitionRepository.GetSeasonByIdAsync(cupEditionId, cancellationToken)
            ?? throw new EntityNotFoundException("CompetitionSeason", cupEditionId);

        var matchDays = (await _matchDayRepository.ListBySeasonAsync(
            competitionSeason.SeasonId, cancellationToken))
            .ToDictionary(md => md.Number);

        var (firstLegDay, secondLegDay) = CompetitionRules.CupLegMatchDays(nextRoundNumber);
        
        if (!matchDays.TryGetValue(firstLegDay, out var firstMatchDay)
            || !matchDays.TryGetValue(secondLegDay, out var secondMatchDay))
        {
            throw new InvalidOperationException($"Cup matchdays not found for round {nextRoundNumber}");
        }

        var firstLegRound = Round.Create(
            cupEditionId,
            CompetitionRules.CupWindowNumber(nextRoundNumber, 1),
            CompetitionRules.CupWindow);
        firstLegRound.ScheduleOn(firstMatchDay.Id);
        await _roundRepository.AddAsync(firstLegRound, cancellationToken);

        var secondLegRound = Round.Create(
            cupEditionId,
            CompetitionRules.CupWindowNumber(nextRoundNumber, 2),
            CompetitionRules.CupWindow);
        secondLegRound.ScheduleOn(secondMatchDay.Id);
        await _roundRepository.AddAsync(secondLegRound, cancellationToken);

        // Random draw from winners
        var pairings = CupBracket.NextRoundPairings(roundTies, random);

        var fixtures = new List<Fixture>();
        var ties = new List<CupTie>();

        foreach (var (home, away) in pairings)
        {
            var firstLeg = Fixture.Create(firstLegRound.Id, home, away);
            var secondLeg = Fixture.Create(secondLegRound.Id, away, home);

            fixtures.Add(firstLeg);
            fixtures.Add(secondLeg);

            var tie = CupTie.Create(cupEditionId, nextRoundNumber, home, away);
            tie.SetLegs(firstLeg.Id, secondLeg.Id);
            ties.Add(tie);
        }

        await _fixtureRepository.AddRangeAsync(fixtures, cancellationToken);
        await _cupTieRepository.AddRangeAsync(ties, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}