using Moq;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// Where a match is being played, and what kind of match it is.
///
/// The header above a scoreboard used to be a fixed line, and a fixed line is only wrong in
/// a way nobody notices: it says the same thing about a Supercup and about a return leg, so
/// a manager reads a tie that is already decided without knowing it. These tests are about
/// the two facts that decide how a result is read — which phase of which competition, and
/// what the other leg was.
/// </summary>
public class MatchContextServiceTests
{
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ICupTieRepository> _ties = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);

    private readonly Guid _matchId = Guid.NewGuid();
    private Fixture _fixture = null!;
    private readonly Guid _windowId = Guid.NewGuid();
    private readonly Guid _editionId = Guid.NewGuid();
    private readonly Guid _dayId = Guid.NewGuid();
    private readonly Guid _seasonId = Guid.NewGuid();

    private readonly Guid _homeId = Guid.NewGuid();
    private readonly Guid _awayId = Guid.NewGuid();

    private Competition _competition = null!;
    private CompetitionSeason _edition = null!;
    private CupTie _tie = null!;

    public MatchContextServiceTests()
    {
        _competition = Competition.Create("Copa do Brasil", CompetitionType.Cup);
        _edition = CompetitionSeason.Create(_competition.Id, _seasonId);

        _tie = CupTie.Create(_editionId, 3, Guid.NewGuid(), Guid.NewGuid());

        _seasons.Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season.Create(2, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)));
        _matchDays.Setup(repo => repo.GetAsync(_dayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MatchDay.Create(_seasonId, 16, new DateOnly(2027, 5, 3)));
        var window = Round.Create(_editionId, 5);
        window.ScheduleOn(_dayId);
        _rounds.Setup(repo => repo.GetAsync(_windowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(window);
        _fixture = Fixture.Create(_windowId, _homeId, _awayId);

        _fixtures.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fixture);
        _matches.Setup(repo => repo.GetAsync(_matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Match.Create(_fixture.Id, _homeId, _awayId));

        _competitions.Setup(repo => repo.GetSeasonViewByIdAsync(_editionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Models.CompetitionSeasonView
            {
                Id = _editionId,
                CompetitionId = _competition.Id,
                SeasonId = _seasonId,
                CompetitionName = _competition.Name,
                Type = CompetitionType.Cup
            });
        _ties.Setup(repo => repo.GetByLegAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tie);
    }

    private MatchContextService CreateService() => new(
        _matches.Object,
        _fixtures.Object,
        _rounds.Object,
        _competitions.Object,
        _matchDays.Object,
        _seasons.Object,
        _ties.Object,
        _teams.Object);

    private void WithHomeGround()
    {
        var club = Team.Create("Clube do Slope", "ABC", "#000000", "#ffffff", 80);
        var ground = Stadium.Create(club.Id, "Arena");
        club.SetStadium(ground);

        _teams.Setup(repo => repo.GetAsync(_homeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(club);
    }

    [Fact]
    public async Task A_cup_window_is_named_by_the_round_of_the_tie_and_not_by_its_own_number()
    {
        // Window 5 of a cup is the second leg of the third tie-round: the calendar lays the
        // two legs out as two windows, so the window's number is not the bracket's.
        _tie.SetLegs(Guid.NewGuid(), _fixture.Id);

        var context = await CreateService().GetAsync(_matchId);

        Assert.NotNull(context);
        Assert.Equal("quartas de final", context!.PhaseName);
        Assert.Equal(16, context.MatchDayNumber);
        Assert.Equal("Temporada II", context.SeasonName);
        Assert.Equal("partida de volta", context.LegLabel);
    }

    [Fact]
    public async Task A_first_leg_says_so_and_carries_no_previous_result()
    {
        _tie.SetLegs(_fixture.Id, Guid.NewGuid());

        var context = await CreateService().GetAsync(_matchId);

        Assert.Equal("partida de ida", context!.LegLabel);
        Assert.Null(context.FirstLeg);
    }

    [Fact]
    public async Task A_return_leg_carries_the_result_of_the_leg_before_it()
    {
        // The two legs swap ends, so the aggregate can only be read by club — and the screen
        // shows the leg as it was played, which is the only form the manager saw it in.
        var firstLegFixture = Guid.NewGuid();
        var firstLegHome = _awayId;
        var firstLegAway = _homeId;

        _tie.SetLegs(firstLegFixture, _fixture.Id);

        var played = Match.Create(firstLegFixture, firstLegHome, firstLegAway);
        played.ApplyEngineState(90, 2, 1, 1);
        played.Finish();

        _matches.Setup(repo => repo.GetByFixtureAsync(firstLegFixture, It.IsAny<CancellationToken>()))
            .ReturnsAsync(played);

        _teams.Setup(repo => repo.GetAsync(_awayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Team.Create("Clube da Casa", "CCA", "#111111", "#ffffff", 80));
        _teams.Setup(repo => repo.GetAsync(_homeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Team.Create("Clube fora", "CF", "#222222", "#ffffff", 80));

        var context = await CreateService().GetAsync(_matchId);

        Assert.NotNull(context!.FirstLeg);
        Assert.Equal(2, context.FirstLeg!.HomeGoals);
        Assert.Equal(1, context.FirstLeg.AwayGoals);
        Assert.Equal("Clube da Casa", context.FirstLeg.HomeTeamName);
    }

    [Fact]
    public async Task A_championship_match_is_a_round_and_never_a_leg()
    {
        var league = Competition.Create("Campeonato Brasileiro", CompetitionType.League);
        var division = CompetitionSeason.Create(league.Id, _seasonId, Guid.NewGuid());

        _competitions.Setup(repo => repo.GetSeasonViewByIdAsync(_editionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Models.CompetitionSeasonView
            {
                Id = _editionId,
                CompetitionId = league.Id,
                SeasonId = _seasonId,
                CompetitionName = league.Name,
                Type = CompetitionType.League,
                Tier = 1
            });
        _ties.Setup(repo => repo.GetByLegAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CupTie?)null);

        var context = await CreateService().GetAsync(_matchId);

        Assert.Equal("Rodada 5", context!.PhaseName);
        Assert.Equal("1ª Divisão", context.EditionName);
        Assert.Null(context.LegLabel);
        Assert.Null(context.FirstLeg);
    }

    [Fact]
    public async Task The_header_says_which_ground_the_match_is_being_played_in()
    {
        WithHomeGround();

        var context = await CreateService().GetAsync(_matchId);

        Assert.Equal("Arena Stadium", context!.StadiumName);
    }
}
