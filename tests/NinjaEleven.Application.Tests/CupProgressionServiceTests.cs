using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Seasons;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A cup bracket cannot be drawn all at once, because nobody knows who is in the quarter-finals
/// before the round of 16 has been played. These tests hold the service to that: a first leg
/// decides nothing, a second leg decides exactly one tie, and the next round exists only once
/// every tie before it is settled.
/// </summary>
public class CupProgressionServiceTests
{
    private readonly Mock<ICupTieRepository> _cupTies = new(MockBehavior.Loose);
    private readonly Mock<ITrophyRepository> _trophies = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);

    /// <summary>
    /// The books a consolation prize is written to. The guard is answered "no prize yet", so
    /// every payment in these tests is written, which is what a test about a payment wants.
    /// </summary>
    private FinanceService Finance()
    {
        _finance.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _finance.Setup(repo => repo.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinanceMovement?)null);
        _finance.Setup(repo => repo.AddAsync(It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Callback((FinanceMovement line, CancellationToken _) => _written.Add(line))
            .Returns(Task.CompletedTask);

        return new FinanceService(
            _finance.Object,
            _teams.Object,
            _players.Object,
            _fixtures.Object,
            _rounds.Object,
            _matchDays.Object,
            _seasons.Object,
            InboxTestFactory.Create(_teams),
            _unitOfWork.Object,
            NullLogger<FinanceService>.Instance);
    }

    private static readonly Guid _seasonId = Guid.NewGuid();
    private readonly CompetitionSeason _cup = CompetitionSeason.Create(Guid.NewGuid(), _seasonId);

    private readonly List<CupTie> _ties = new();
    private readonly List<Round> _addedRounds = new();
    private readonly List<Fixture> _addedFixtures = new();
    private readonly List<CupTie> _addedTies = new();
    private readonly List<TrophyAward> _addedTrophies = new();

    /// <summary>Every line the books were told to write, in the order they were written.</summary>
    private readonly List<FinanceMovement> _written = new();

    /// <summary>The order the service did things in, for the tests that care about order.</summary>
    private readonly List<string> _calls = new();

    private CupProgressionService CreateService() => new(
        _cupTies.Object,
        _trophies.Object,
        _matches.Object,
        _rounds.Object,
        _fixtures.Object,
        _matchDays.Object,
        _competitions.Object,
        _unitOfWork.Object,
        Finance(),
        InboxTestFactory.Create(_teams),
        _teams.Object,
        new Random());

    public CupProgressionServiceTests()
    {
        // The whole season's days, because the cup's legs are drawn on days the championship
        // does not reach: a calendar cut at the thirtieth round would have no thirty-second day
        // for the final and the bracket would silently draw nothing.
        _days.AddRange(Enumerable.Range(1, CompetitionRules.SeasonMatchDays)
            .Select(number => MatchDay.Create(_seasonId, number, DateOnly.FromDayNumber(number))));

        _cupTies.Setup(repo => repo.GetByLegAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid fixtureId, CancellationToken _) =>
                Task.FromResult<CupTie?>(
                    _ties.FirstOrDefault(tie => tie.FirstLegFixtureId == fixtureId
                        || tie.SecondLegFixtureId == fixtureId)));

        // The round comes back untracked, as the repository really reads it: a copy per tie,
        // carrying whether that tie is decided. The service commits its own decision before
        // asking, so a tie it has just settled reads back as settled.
        _cupTies.Setup(repo => repo.ListByRoundAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, int round, CancellationToken __) =>
            {
                _calls.Add("read-round");
                return Task.FromResult<IReadOnlyList<CupTie>>(
                    _ties.Where(tie => tie.RoundNumber == round)
                        .Select(Copy)
                        .ToList());
            });

        _unitOfWork.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _calls.Add("commit");
                return Task.FromResult(0);
            });

        _cupTies.Setup(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<CupTie>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<CupTie> ties, CancellationToken _) =>
            {
                _addedTies.AddRange(ties);
                return Task.CompletedTask;
            });

        _cupTies.Setup(repo => repo.Update(It.IsAny<CupTie>()));

        _trophies.Setup(repo => repo.ListBySeasonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<TrophyAward>>(_addedTrophies.ToList()));

        _trophies.Setup(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<TrophyAward>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<TrophyAward> trophies, CancellationToken _) =>
            {
                _addedTrophies.AddRange(trophies);
                return Task.CompletedTask;
            });

        _matches.Setup(repo => repo.GetByFixtureAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid fixtureId, CancellationToken _) => Task.FromResult<Match?>(
                _matchesByFixture.TryGetValue(fixtureId, out var match) ? match : null));

        _rounds.Setup(repo => repo.AddAsync(It.IsAny<Round>(), It.IsAny<CancellationToken>()))
            .Returns((Round round, CancellationToken _) =>
            {
                _addedRounds.Add(round);
                return Task.CompletedTask;
            });

        _fixtures.Setup(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<Fixture>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Fixture> fixtures, CancellationToken _) =>
            {
                _addedFixtures.AddRange(fixtures);
                return Task.CompletedTask;
            });

        _competitions.Setup(repo => repo.GetSeasonByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<CompetitionSeason?>(_cup));

        _matchDays.Setup(repo => repo.ListBySeasonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<MatchDay>>(_days));

        _matchesByFixture = new Dictionary<Guid, Match>();
    }

    private readonly Dictionary<Guid, Match> _matchesByFixture = new();

    /// <summary>The season's days, as the drawer will find them.</summary>
    private readonly List<MatchDay> _days = new();

    /// <summary>
    /// A detached copy of a tie, decided or not as the original is — which is what an untracked
    /// read hands back, and what makes reading the round back a question about what has been
    /// committed rather than about what is held in memory.
    /// </summary>
    private static CupTie Copy(CupTie tie)
    {
        var copy = CupTie.Create(
            tie.CompetitionSeasonId,
            tie.RoundNumber,
            tie.HomeTeamId,
            tie.AwayTeamId);

        if (tie.FirstLegFixtureId is not null && tie.SecondLegFixtureId is not null)
        {
            copy.SetLegs(tie.FirstLegFixtureId.Value, tie.SecondLegFixtureId.Value);
        }

        if (tie.IsResolved)
        {
            copy.Resolve(
                tie.AggregateHomeGoals!.Value,
                tie.AggregateAwayGoals!.Value,
                tie.WentToPenalties
                    ? new ShootoutOutcome(
                        tie.HomePenaltyGoals!.Value,
                        tie.AwayPenaltyGoals!.Value,
                        tie.WinnerTeamId!.Value,
                        IsSuddenDeath: false)
                    : null);
        }

        return copy;
    }

    private static readonly Guid[] _clubs = Enumerable.Range(0, 64).Select(_ => Guid.NewGuid()).ToArray();

    /// <summary>
    /// The seed a leg reports. Nothing in the cup reads it any more — the penalties come from
    /// the match that took them — but it is part of what a leg says about itself.
    /// </summary>
    private const int TestSeed = 7;

    /// <summary>A finished leg, recorded so the service can read its score back.</summary>
    private Match PlayedLeg(Guid fixtureId, Guid home, Guid away, int homeScore, int awayScore)
    {
        var match = Match.Create(fixtureId, home, away);
        match.ApplyEngineState(90, homeScore, awayScore, 1);
        match.Finish();
        _matchesByFixture[fixtureId] = match;
        return match;
    }

    /// <summary>A tie with both legs played, in the given home/away order.</summary>
    private CupTie DecidedPairing(
        int round,
        Guid home,
        Guid away,
        int firstHome,
        int firstAway,
        int secondHome,
        int secondAway)
    {
        var tie = CupTie.Create(_cup.Id, round, home, away);
        var firstLeg = Guid.NewGuid();
        var secondLeg = Guid.NewGuid();
        tie.SetLegs(firstLeg, secondLeg);
        _ties.Add(tie);

        PlayedLeg(firstLeg, home, away, firstHome, firstAway);
        PlayedLeg(secondLeg, away, home, secondHome, secondAway);

        return tie;
    }

    /// <summary>
    /// The second leg of a tie, as the match reports it: the score, and the shootout when it
    /// went to one.
    ///
    /// The shootout is counted by this leg's sides, which is what a leg's own numbers are —
    /// the tie is the thing that reads them back by club, because the legs swap ends.
    /// </summary>
    private static CupLegOutcome SecondLegOf(
        CupTie tie,
        Guid fixtureId,
        int homeScore,
        int awayScore,
        ShootoutOutcome? shootout = null) =>
        new()
        {
            FixtureId = fixtureId,
            HomeTeamId = tie.AwayTeamId,
            AwayTeamId = tie.HomeTeamId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Seed = TestSeed,
            Shootout = shootout
        };

    /// <summary>
    /// The penalties a level tie was settled by, as the leg that took them reports them: four
    /// goals to three, won by the club that was at home in that leg.
    /// </summary>
    private static ShootoutOutcome TakenByTheLegsHomeClub(CupTie tie) =>
        new(4, 3, tie.AwayTeamId, IsSuddenDeath: false);

    [Fact]
    public async Task A_tie_is_committed_before_the_round_is_read_back()
    {
        // The round is read untracked, so a tie that has only been decided in memory reads back
        // as undecided. A service that asked the round first and committed afterwards would see
        // the last tie of the round as still being played, decide the round was unfinished, and
        // never draw the next one — a cup that stops after its first two matchdays, with no
        // error anywhere to explain why. So the commit has to come first.
        var last = CompleteTheRoundOfSixtyFourWithTheLastTieDecided();
        await CreateService().AdvanceAsync(SecondLegOf(last, last.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(last)));

        var commit = _calls.IndexOf("commit");
        var read = _calls.IndexOf("read-round");

        Assert.True(commit >= 0, "The cup committed nothing.");
        Assert.True(read >= 0, "The cup never read the round back.");
        Assert.True(
            commit < read,
            $"The round was read before the tie was committed: {string.Join(" -> ", _calls)}");
    }

    [Fact]
    public async Task A_match_that_is_not_part_of_a_tie_leaves_the_cup_alone()
    {
        // A championship match is not a cup match, and the cup must not invent a tie for it.
        await CreateService().AdvanceAsync(new CupLegOutcome
        {
            FixtureId = Guid.NewGuid(),
            HomeTeamId = _clubs[0],
            AwayTeamId = _clubs[1],
            HomeScore = 2,
            AwayScore = 0,
            Seed = 1
        });

        Assert.Empty(_addedTies);
        Assert.Empty(_addedTrophies);
        Assert.Empty(_addedRounds);
    }

    [Fact]
    public async Task The_first_leg_of_a_tie_decides_nothing()
    {
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 2, 0, 0, 0);

        // The first leg goes 2-0. A tie is two games, so this is half an answer.
        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.FirstLegFixtureId!.Value, 2, 0));

        Assert.False(tie.IsResolved);
        Assert.Null(tie.WinnerTeamId);
    }

    [Fact]
    public async Task The_second_leg_decides_the_tie_on_the_aggregate()
    {
        // First leg: the home club wins 2-0. Second leg the other way round, 1-1.
        // The aggregate is 2-1 to the club that was home in the first leg.
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 2, 0, 1, 1);

        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(tie)));

        Assert.True(tie.IsResolved);
        Assert.Equal(3, tie.AggregateHomeGoals);
        Assert.Equal(1, tie.AggregateAwayGoals);
        Assert.Equal(_clubs[0], tie.WinnerTeamId);
        Assert.Equal(_clubs[1], tie.LoserTeamId);
    }

    [Fact]
    public async Task The_aggregate_is_counted_by_club_and_not_by_side()
    {
        // The first leg goes 2-0 to the home club. The second leg is the other way round and
        // ends 3-0 to the club that is now at home — the club that lost the first leg.
        //
        // Added by club the tie is 2-3 and the away club goes through. Added by side it would
        // read 5-0 to the first leg's home club, which is the wrong answer twice over: it
        // credits a club with a goal it did not score and sends the winner out of the cup.
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 2, 0, 3, 0);

        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.SecondLegFixtureId!.Value, 3, 0));

        Assert.Equal(2, tie.AggregateHomeGoals);
        Assert.Equal(3, tie.AggregateAwayGoals);
        Assert.Equal(_clubs[1], tie.WinnerTeamId);
        Assert.False(tie.WentToPenalties);
    }

    [Fact]
    public async Task A_level_aggregate_is_decided_by_a_shootout()
    {
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 1, 1, 1, 1);

        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(tie)));

        Assert.True(tie.WentToPenalties);
        Assert.True(tie.IsResolved);
        Assert.NotNull(tie.HomePenaltyGoals);
        Assert.NotEqual(tie.HomePenaltyGoals, tie.AwayPenaltyGoals);
    }

    [Fact]
    public async Task A_replayed_tie_is_decided_the_same_way()
    {
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 1, 1, 1, 1);

        await CreateService().AdvanceAsync(SecondLegOf(
            tie, tie.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(tie)));
        var first = (tie.HomePenaltyGoals, tie.AwayPenaltyGoals, tie.WinnerTeamId);

        // A tie that is resolved cannot be resolved again, so the replay lands on the answer
        // already on the tie rather than taking the penalties a second time.
        await CreateService().AdvanceAsync(SecondLegOf(
            tie, tie.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(tie)));

        Assert.Equal(first.Item1, tie.HomePenaltyGoals);
        Assert.Equal(first.Item3, tie.WinnerTeamId);
    }

    [Fact]
    public async Task The_next_round_is_not_drawn_while_a_tie_in_it_is_undecided()
    {
        // Two ties in the round of 64: one finished, one still being played.
        var finished = DecidedPairing(1, _clubs[0], _clubs[1], 2, 0, 1, 1);
        DecidedPairing(1, _clubs[2], _clubs[3], 0, 0, 0, 0);

        await CreateService().AdvanceAsync(SecondLegOf(finished, finished.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(finished)));

        Assert.Empty(_addedRounds);
        Assert.Empty(_addedFixtures);
    }

    [Fact]
    public async Task The_next_round_is_drawn_once_the_round_before_it_is_decided()
    {
        var last = CompleteTheRoundOfSixtyFourWithTheLastTieDecided();

        await CreateService().AdvanceAsync(SecondLegOf(last, last.SecondLegFixtureId!.Value, 0, 0, TakenByTheLegsHomeClub(last)));

        // Sixty-four clubs become sixteen ties of the round of 32, over two windows, one leg each.
        Assert.Equal(16, _addedTies.Count);
        Assert.All(_addedTies, tie => Assert.Equal(2, tie.RoundNumber));
        Assert.Equal(32, _addedFixtures.Count);
        Assert.Equal(2, _addedRounds.Count);
    }

    [Fact]
    public async Task The_round_of_thirty_two_is_scheduled_on_the_days_the_rules_give_them()
    {
        // A whole round of 64 with every tie but one decided, which is enough to reach the draw:
        // the round is complete.
        var complete = CompleteTheRoundOfSixtyFourWithTheLastTieDecided();

        await CreateService().AdvanceAsync(
            SecondLegOf(complete, complete.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(complete)));

        var (expectedFirstLeg, expectedSecondLeg) = CompetitionRules.CupLegMatchDays(2);

        Assert.Equal(2, _addedRounds.Count);
        Assert.Equal(CompetitionRules.CupWindowNumber(2, 1), _addedRounds[0].Number);
        Assert.Equal(CompetitionRules.CupWindowNumber(2, 2), _addedRounds[1].Number);
        Assert.Equal(CompetitionRules.CupWindow, _addedRounds[0].Window);

        // Each leg is on the day the rules name, and the two are a day apart.
        Assert.Equal(1, expectedSecondLeg - expectedFirstLeg);

        var dayById = _days.ToDictionary(day => day.Id);
        Assert.Equal(expectedFirstLeg, dayById[_addedRounds[0].MatchDayId!.Value].Number);
        Assert.Equal(expectedSecondLeg, dayById[_addedRounds[1].MatchDayId!.Value].Number);
    }

    /// <summary>
    /// A whole round of 64 with every tie but one decided, and that last one returned so the
    /// caller can play it. Thirty-two ties is what a round of a sixty-four club cup holds, and
    /// a round of any other size is not a round the bracket may draw.
    /// </summary>
    private CupTie CompleteTheRoundOfSixtyFourWithTheLastTieDecided()
    {
        for (var index = 0; index < 31; index++)
        {
            var home = _clubs[index * 2];
            var away = _clubs[index * 2 + 1];
            var pair = CupTie.Create(_cup.Id, 1, home, away);
            pair.SetLegs(Guid.NewGuid(), Guid.NewGuid());
            _ties.Add(pair);

            PlayedLeg(pair.FirstLegFixtureId!.Value, home, away, 2, 0);
            PlayedLeg(pair.SecondLegFixtureId!.Value, away, home, 0, 1);
            pair.Resolve(3, 1);
        }

        return DecidedPairing(1, _clubs[62], _clubs[63], 0, 0, 0, 0);
    }

    [Fact]
    public async Task A_round_that_is_not_the_size_a_cup_of_sixty_four_has_is_not_drawn()
    {
        // One tie decided in a round that should hold thirty-two. The tie is settled — it was a
        // real match between two real clubs — but a bracket of one cannot be halved into the
        // next round, and a bracket that cannot be drawn must not throw: the call arrives from
        // inside the tick of a match that has already finished.
        var only = DecidedPairing(1, _clubs[0], _clubs[1], 2, 0, 1, 1);

        await CreateService().AdvanceAsync(SecondLegOf(only, only.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(only)));

        Assert.True(only.IsResolved);
        Assert.Equal(_clubs[0], only.WinnerTeamId);
        Assert.Empty(_addedRounds);
        Assert.Empty(_addedFixtures);
        Assert.Empty(_addedTies);
    }

    [Fact]
    public async Task The_final_puts_the_cup_on_the_winners_shelf_and_names_the_runner_up()
    {
        var final = DecidedPairing(CompetitionRules.CupRounds, _clubs[0], _clubs[1], 2, 1, 0, 0);

        await CreateService().AdvanceAsync(SecondLegOf(final, final.SecondLegFixtureId!.Value, 0, 0, TakenByTheLegsHomeClub(final)));

        // Two clubs left means the final, and a final is won and lost — not drawn again.
        Assert.Empty(_addedRounds);
        Assert.Equal(2, _addedTrophies.Count);

        var champion = _addedTrophies.Single(trophy => trophy.Kind == TrophyKind.Champion);
        var runnerUp = _addedTrophies.Single(trophy => trophy.Kind == TrophyKind.RunnerUp);

        Assert.Equal(_clubs[0], champion.TeamId);
        Assert.Equal(_clubs[1], runnerUp.TeamId);
        Assert.Equal(_cup.Id, champion.CompetitionSeasonId);
        Assert.Equal(_seasonId, champion.SeasonId);
    }

    [Fact]
    public async Task A_club_knocked_out_in_the_first_round_is_paid_for_having_been_there()
    {
        // The first club wins the tie 2-0 on aggregate, so the second is the one going out.
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 1, 0, 0, 1);

        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.SecondLegFixtureId!.Value, 0, 1));

        var consolation = Assert.Single(_written);

        Assert.Equal(_clubs[1], consolation.TeamId);
        Assert.Equal(FinanceMovementKind.PrizeMoney, consolation.Kind);
        Assert.Equal(PrizeRules.CupLastThirtyTwoLoser, consolation.Amount);
        Assert.Equal(_seasonId, consolation.SeasonId);

        // The club that won the tie is not paid for winning it: it is paid when it wins the
        // cup, and a first-round winner who is paid as a champion would be paid twice for one
        // evening by two different rules.
        Assert.DoesNotContain(_written, line => line.TeamId == _clubs[0]);
    }

    [Fact]
    public async Task The_final_pays_the_champion_the_cup_and_the_runner_up_three_million()
    {
        var final = DecidedPairing(CompetitionRules.CupRounds, _clubs[0], _clubs[1], 2, 1, 0, 0);

        await CreateService().AdvanceAsync(SecondLegOf(final, final.SecondLegFixtureId!.Value, 0, 0, TakenByTheLegsHomeClub(final)));

        Assert.Equal(2, _written.Count);

        var champion = _written.Single(line => line.TeamId == _clubs[0]);
        var runnerUp = _written.Single(line => line.TeamId == _clubs[1]);

        Assert.Equal(PrizeRules.CupChampionPrize, champion.Amount);
        Assert.Equal(PrizeRules.CupFinalLoser, runnerUp.Amount);
        Assert.All(_written, line => Assert.Equal(FinanceMovementKind.PrizeMoney, line.Kind));

        // The runner-up is paid once, not twice: the final's consolation and the final's
        // runner-up's cheque are the same three million, and a club that is paid for losing
        // a final twice has 2.400.000 in his book for one evening.
        Assert.Equal(1, _written.Count(line => line.TeamId == _clubs[1]));
    }

    [Fact]
    public async Task A_replayed_leg_does_not_pay_the_consolation_twice()
    {
        var tie = DecidedPairing(1, _clubs[0], _clubs[1], 1, 0, 0, 1);

        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.SecondLegFixtureId!.Value, 0, 1));
        await CreateService().AdvanceAsync(SecondLegOf(tie, tie.SecondLegFixtureId!.Value, 0, 1));

        // One line, not two. The books' guard is the reference — the same round of the same
        // cup asked about twice is one payment — and this stub answers "not paid yet" to
        // everything, so what stops the second payment here is the tie already being decided:
        // a leg that arrives for a tie that is over is not settled again at all.
        Assert.Single(_written);
    }

    [Fact]
    public async Task A_cup_is_never_awarded_twice()
    {
        var final = DecidedPairing(CompetitionRules.CupRounds, _clubs[0], _clubs[1], 2, 1, 0, 0);

        await CreateService().AdvanceAsync(SecondLegOf(final, final.SecondLegFixtureId!.Value, 0, 0, TakenByTheLegsHomeClub(final)));
        Assert.Equal(2, _addedTrophies.Count);

        // The same final, settled again from a replayed leg.
        await CreateService().AdvanceAsync(SecondLegOf(final, final.SecondLegFixtureId!.Value, 0, 0, TakenByTheLegsHomeClub(final)));

        Assert.Equal(2, _addedTrophies.Count);
    }

    [Fact]
    public async Task A_round_whose_legs_have_no_matchday_is_skipped_rather_than_guessed()
    {
        // A calendar with no matchdays at all: there is nowhere to put a quarter-final, and a
        // tie on a date that does not exist is a match nobody can play.
        _matchDays.Setup(repo => repo.ListBySeasonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<MatchDay>>(Array.Empty<MatchDay>()));

        var only = DecidedPairing(1, _clubs[0], _clubs[1], 2, 0, 1, 1);

        await CreateService().AdvanceAsync(SecondLegOf(only, only.SecondLegFixtureId!.Value, 1, 1, TakenByTheLegsHomeClub(only)));

        Assert.True(only.IsResolved);
        Assert.Empty(_addedRounds);
        Assert.Empty(_addedFixtures);
    }
}
