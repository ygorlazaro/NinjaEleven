using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// Which fixture a club's next-match box offers, when a matchday holds two windows.
///
/// This is about the **order of the waves** and it is the reason the box does not walk the
/// windows by the number printed on them. A round's number is a number inside its own
/// competition: the fifth matchday of a division is window 5, and the first round of a cup is
/// round 1 however late in the season it is played. So a day with the championship in window 5
/// and a cup round of 16 in window 1 is a day that looks like it opens with the cup and does
/// not — the championship is played out in every division first, and the box that believed the
/// number offered a manager a cup leg his own league match had to be finished before, and the
/// server refused it with a rule the client had never heard of.
/// </summary>
public class NextFixtureWindowTests
{
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ITransferRepository> _transfers = new(MockBehavior.Loose);

    private readonly Guid _seasonId = Guid.Empty;

    /// <summary>
    /// The matchday both windows are on, held as the instance rather than as its id: a domain
    /// entity mints its own id in `Create`, so a day built in a mock setup is a *different*
    /// day from one the rounds were scheduled on, and the two never meet in a dictionary.
    /// </summary>
    private readonly MatchDay _day = MatchDay.Create(
        Guid.Empty, 5, new DateOnly(2026, 2, 1));

    /// <summary>The club whose next match is being asked about, in both windows.</summary>
    private readonly Guid _manager = Guid.NewGuid();

    /// <summary>A second club, so the championship window has a match that is not the manager's.</summary>
    private readonly Guid _rival = Guid.NewGuid();

    private readonly Competition _leagueCompetition = Competition.Create("Campeonato", CompetitionType.League);
    private readonly Competition _cupCompetition = Competition.Create("Copa", CompetitionType.Cup);

    private readonly CompetitionSeason _league = null!;
    private readonly CompetitionSeason _cup = null!;

    /// <summary>The division's window on the day, numbered as a matchday: 5.</summary>
    private readonly Round _leagueRound = null!;

    /// <summary>
    /// The cup's round of 16, numbered as a cup round: 1.
    ///
    /// The two numbers are the whole point. Ordering the day's windows by them would put the
    /// cup first, which is both the wrong answer and the one the old code gave.
    /// </summary>
    private readonly Round _cupRound = null!;

    private Fixture _leagueMatch = null!;
    private Fixture _cupTie = null!;

    /// <summary>
    /// A match of another division, on the same day and never played here.
    ///
    /// The day's league wave is over when **every** division's window is over, not when the
    /// manager's own is, so a scenario with one division cannot say anything about a window
    /// that waits. This is the fixture that keeps the championship open.
    /// </summary>
    private Fixture _otherDivisionMatch = null!;

    private CompetitionSeason _otherDivision = null!;
    private Round _otherDivisionRound = null!;

    public NextFixtureWindowTests()
    {
        _league = CompetitionSeason.Create(_leagueCompetition.Id, _seasonId, Guid.NewGuid());
        _cup = CompetitionSeason.Create(_cupCompetition.Id, _seasonId);
        _otherDivision = CompetitionSeason.Create(_leagueCompetition.Id, _seasonId, Guid.NewGuid());

        _leagueRound = Round.Create(_league.Id, 5);
        _cupRound = Round.Create(_cup.Id, 1);
        _otherDivisionRound = Round.Create(_otherDivision.Id, 5);
        _leagueRound.ScheduleOn(_day.Id);
        _cupRound.ScheduleOn(_day.Id);
        _otherDivisionRound.ScheduleOn(_day.Id);

        _leagueMatch = Fixture.Create(_leagueRound.Id, _manager, _rival);
        _cupTie = Fixture.Create(_cupRound.Id, _rival, _manager);
        _otherDivisionMatch = Fixture.Create(_otherDivisionRound.Id, Guid.NewGuid(), Guid.NewGuid());

        _matchDays.Setup(repo => repo.ListBySeasonAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _day });
        _matchDays.Setup(repo => repo.GetAsync(_day.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_day);

        _competitions.Setup(repo => repo.ListSeasonViewsAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                View(_league.Id, _leagueCompetition.Id, CompetitionType.League, tier: 1),
                View(_otherDivision.Id, _leagueCompetition.Id, CompetitionType.League, tier: 2),
                View(_cup.Id, _cupCompetition.Id, CompetitionType.Cup, tier: null)
            });
        _competitions.Setup(repo => repo.GetSeasonByIdAsync(_league.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_league);
        _competitions.Setup(repo => repo.GetSeasonByIdAsync(_cup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cup);
        _competitions.Setup(repo => repo.GetAsync(_leagueCompetition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_leagueCompetition);
        _competitions.Setup(repo => repo.GetAsync(_cupCompetition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cupCompetition);

        _rounds.Setup(repo => repo.ListByCompetitionSeasonIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _leagueRound, _otherDivisionRound, _cupRound });
        _rounds.Setup(repo => repo.ListByMatchDayAsync(_day.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _leagueRound, _otherDivisionRound, _cupRound });

        // `IEnumerable` and not the `IReadOnlyList` the interface declares: the service hands
        // the ids down as a lazy projection in one caller and as a materialised list in the
        // other, and a matcher for the declared type silently matches neither.
        var all = new[] { _leagueMatch, _otherDivisionMatch, _cupTie };
        _fixtures.Setup(repo => repo.ListByRoundIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                all.Where(fixture => ids.Contains(fixture.RoundId)).ToList());

        _matches.Setup(repo => repo.GetByFixtureAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid fixtureId, CancellationToken _) =>
            {
                var fixture = all.FirstOrDefault(candidate => candidate.Id == fixtureId);

                return fixture is not null && fixture.Status != FixtureStatus.Scheduled
                    ? Match.Create(fixture.Id, fixture.HomeTeamId, fixture.AwayTeamId)
                    : null;
            });
    }

    private static CompetitionSeasonView View(
        Guid id, Guid competitionId, CompetitionType type, int? tier) => new()
    {
        Id = id,
        CompetitionId = competitionId,
        SeasonId = Guid.Empty,
        Tier = tier,
        CompetitionName = type == CompetitionType.Cup ? "Copa" : "Campeonato",
        Type = type
    };

    private MatchdayService CreateService() => new(
        _matchDays.Object,
        _rounds.Object,
        _fixtures.Object,
        _competitions.Object,
        _matches.Object,
        new ScorerPrizeService(
            _competitions.Object,
            _players.Object,
            _teams.Object,
            new FinanceService(
                _finance.Object,
                _teams.Object,
                _players.Object,
                _fixtures.Object,
                _rounds.Object,
                _matchDays.Object,
                _seasons.Object,
                InboxTestFactory.Create(_teams),
                _unitOfWork.Object,
                NullLogger<FinanceService>.Instance),
            InboxTestFactory.Create(_teams),
            NullLogger<ScorerPrizeService>.Instance),
        new TransferService(
            _transfers.Object,
            _teams.Object,
            _players.Object,
            _seasons.Object,
            _competitions.Object,
            _rounds.Object,
            _matches.Object,
            _finance.Object,
            InboxTestFactory.Create(_teams),
            new ManagedClubs(),
            _unitOfWork.Object,
            NullLogger<TransferService>.Instance),
        _unitOfWork.Object,
        NullLogger<MatchdayService>.Instance);

    [Fact]
    public async Task The_championship_of_the_day_is_the_next_match_even_though_the_cup_window_is_numbered_first()
    {
        var window = await CreateService().GetNextFixtureWindowAsync(_manager, _seasonId);

        // The cup round is round 1 and the championship window is 5, and the championship is
        // the one that goes first. A box that ordered by the number would have sent the manager
        // to the cup and the server would have answered that the day is still a league day.
        Assert.NotNull(window);
        Assert.Equal(_leagueMatch.Id, window!.FixtureId);
        Assert.Equal(5, window.RoundNumber);
        Assert.Equal(5, window.MatchDayNumber);
        Assert.True(window.WaveOpen);
        Assert.Null(window.WaitingFor);
    }

    [Fact]
    public async Task A_cup_tie_behind_an_unfinished_championship_is_offered_with_the_wave_it_waits_for()
    {
        // The manager's own league match is over and another division's is not, so the day is
        // still a league day. The cup tie is the club's next fixture and cannot be played, and
        // the answer has to say so rather than let a manager walk to a refusal.
        _leagueMatch.MarkFinished();

        var window = await CreateService().GetNextFixtureWindowAsync(_manager, _seasonId);

        Assert.NotNull(window);
        Assert.Equal(_cupTie.Id, window!.FixtureId);
        Assert.False(window.WaveOpen);
        Assert.Equal(CompetitionType.League, window.WaitingFor);
    }

    [Fact]
    public async Task A_finished_match_is_not_a_next_match()
    {
        // A club that has played both of the day's windows has nothing left on the day, and the
        // box says so rather than offering a match that is already in the record book.
        _leagueMatch.MarkFinished();
        _cupTie.MarkFinished();

        Assert.Null(await CreateService().GetNextFixtureWindowAsync(_manager, _seasonId));
    }
}
