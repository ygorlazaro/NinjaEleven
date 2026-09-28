using Moq;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The cup read as a bracket.
///
/// The one thing this service could get wrong without anybody noticing is the direction of a
/// leg. The two legs of a tie swap ends, so a bracket that added the two scores as they arrived
/// would print the right numbers attached to the wrong clubs — and it would be right about every
/// tie that was not level on the aggregate, which is most of them. So the tests here are about
/// the swap, and about a leg that has not been played reading as no score rather than as a
/// goalless draw.
/// </summary>
public class CupBracketServiceTests
{
    private readonly Mock<ICupTieRepository> _cupTies = new();
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<ICompetitionRepository> _competitions = new();

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _competitionId = Guid.NewGuid();
    private readonly CompetitionSeason _edition;

    private readonly Team _home = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 80);
    private readonly Team _away = Team.Create("Estrela do Norte", "EDN", "#0F5132", "#FFD700", 70);

    public CupBracketServiceTests()
    {
        _edition = CompetitionSeason.Create(_competitionId, _seasonId);

        _competitions
            .Setup(repo => repo.GetSeasonByIdAsync(_edition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_edition);
        _competitions
            .Setup(repo => repo.GetAsync(_competitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Competition.Create("Copa do Brasil", CompetitionType.Cup));
        _teams
            .Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_home, _away]);
    }

    [Fact]
    public async Task A_tie_is_read_as_two_legs_with_every_number_on_the_club_that_earned_it()
    {
        // 2-0 in Lisbon and 1-1 in the north: the tie's home club is the one that was at home in
        // the first leg, and it is away in the second. Read by side, the away club's second leg
        // would come out as the one it did not play.
        var (tie, firstLeg, secondLeg) = GivenAResolvedTie(2, 0, 1, 1);

        var bracket = await Service().GetAsync(_edition.Id);

        var round = Assert.Single(bracket!.Rounds);
        Assert.Equal("16 avos de final", round.Name);
        var read = Assert.Single(round.Ties);

        var home = read.Clubs[0];
        Assert.Equal(_home.Id, home.TeamId);
        Assert.Equal(2, home.FirstLegGoals);
        Assert.Equal(0, home.FirstLegConceded);
        Assert.Equal(1, home.SecondLegGoals);
        Assert.Equal(1, home.SecondLegConceded);
        Assert.Equal(3, home.AggregateGoals);
        Assert.Equal(1, home.AggregateConceded);
        Assert.True(home.IsWinner);

        var away = read.Clubs[1];
        Assert.Equal(_away.Id, away.TeamId);
        Assert.Equal(0, away.FirstLegGoals);
        Assert.Equal(2, away.FirstLegConceded);
        Assert.Equal(1, away.SecondLegGoals);
        Assert.Equal(1, away.SecondLegConceded);
        Assert.Equal(1, away.AggregateGoals);
        Assert.Equal(3, away.AggregateConceded);
        Assert.True(away.IsLoser);

        // The legs are the fixture's own scores: home side first, both of them.
        Assert.Equal(2, read.FirstLegScore);
        Assert.Equal(1, read.SecondLegScore);
        Assert.Equal(firstLeg.Id, read.FirstLegMatchId);
        Assert.Equal(secondLeg.Id, read.SecondLegMatchId);
    }

    [Fact]
    public async Task A_leg_that_has_not_been_played_is_no_score_and_not_a_goalless_draw()
    {
        // The second leg is a fixture with no match in it yet. Reading its 0 x 0 as a result
        // would show a tie of nil-nil as though two clubs had gone out to it, and a bracket
        // that did that would look decided while it is not.
        var (tie, _, _) = GivenAPendingTie(firstLegScore: 3, firstLegAway: 1);

        var bracket = await Service().GetAsync(_edition.Id);

        var read = Assert.Single(Assert.Single(bracket!.Rounds).Ties);
        Assert.Equal(3, read.FirstLegScore);
        Assert.Null(read.SecondLegScore);
        Assert.Null(read.SecondLegMatchId);
        Assert.All(read.Clubs, club => Assert.Null(club.SecondLegGoals));
        Assert.All(read.Clubs, club => Assert.Null(club.AggregateGoals));
        Assert.All(read.Clubs, club => Assert.False(club.IsWinner));
    }

    [Fact]
    public async Task A_round_that_has_not_been_drawn_is_not_in_the_bracket()
    {
        // Nobody knows who is in the quarter-finals before the round of 16 has been played, and
        // a bracket that drew the next round in advance would be inventing a tie between two
        // clubs that may not both be in it.
        var (tie, _, _) = GivenAResolvedTie(2, 0, 1, 1);
        GivenTheTies(tie);

        var bracket = await Service().GetAsync(_edition.Id);

        Assert.Single(bracket!.Rounds);
    }

    [Fact]
    public async Task A_cup_whose_first_round_is_not_drawn_yet_is_an_empty_bracket_and_not_an_error()
    {
        // A season two days old is a season that is working. The screen is told the bracket is
        // empty rather than handed a failure it cannot explain.
        GivenTheTies();

        var bracket = await Service().GetAsync(_edition.Id);

        Assert.NotNull(bracket);
        Assert.Empty(bracket!.Rounds);
        Assert.Equal("Copa do Brasil", bracket.CompetitionName);
        Assert.Null(bracket.ChampionTeamId);
    }

    [Fact]
    public async Task An_edition_that_does_not_exist_has_no_bracket()
    {
        _competitions
            .Setup(repo => repo.GetSeasonByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompetitionSeason?)null);

        Assert.Null(await Service().GetAsync(Guid.NewGuid()));
    }

    private CupBracketService Service() => new(
        _cupTies.Object, _matches.Object, _teams.Object, _competitions.Object);

    /// <summary>
    /// A settled tie of one round: both legs played, the aggregate given to the cup, and the
    /// winner and loser recorded on it — the same state the cup leaves a tie in.
    /// </summary>
    private (CupTie Tie, Match FirstLeg, Match SecondLeg) GivenAResolvedTie(
        int firstLegHome, int firstLegAway, int secondLegHome, int secondLegAway)
    {
        var (tie, firstLeg, _) = GivenAPendingTie(firstLegHome, firstLegAway);
        var secondLeg = GivenALeg(tie.SecondLegFixtureId!.Value, _away.Id, _home.Id, secondLegHome, secondLegAway);
        var aggregateHome = firstLegHome + secondLegAway;
        var aggregateAway = firstLegAway + secondLegHome;

        tie.Resolve(aggregateHome, aggregateAway);
        GivenTheTies(tie);

        return (tie, firstLeg, secondLeg);
    }

    /// <summary>A tie whose second leg is still to be played, with its first leg behind it.</summary>
    private (CupTie Tie, Match FirstLeg, Guid SecondLegFixture) GivenAPendingTie(
        int firstLegScore, int firstLegAway)
    {
        var round = Round.Create(_edition.Id, 1, CompetitionRules.CupWindow);
        var firstLegFixture = Fixture.Create(round.Id, _home.Id, _away.Id);
        var secondLegFixture = Fixture.Create(round.Id, _away.Id, _home.Id);

        var tie = CupTie.Create(_edition.Id, 1, _home.Id, _away.Id);
        tie.SetLegs(firstLegFixture.Id, secondLegFixture.Id);

        var firstLeg = GivenALeg(firstLegFixture.Id, _home.Id, _away.Id, firstLegScore, firstLegAway);
        GivenTheTies(tie);

        return (tie, firstLeg, secondLegFixture.Id);
    }

    private Match GivenALeg(Guid fixtureId, Guid homeTeamId, Guid awayTeamId, int homeGoals, int awayGoals)
    {
        var leg = Match.Create(fixtureId, homeTeamId, awayTeamId, CompetitionType.Cup, CompetitionRules.CupWindow);
        leg.ApplyEngineState(90, homeGoals, awayGoals, 1);
        leg.Finish();
        _playedLegs.Add((fixtureId, leg));

        return leg;
    }

    private void GivenTheTies(params CupTie[] ties)
    {
        _cupTies
            .Setup(repo => repo.ListByCompetitionSeasonAsync(_edition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ties);

        _matches
            .Setup(repo => repo.ListByFixtureIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Where(id => _playedLegs.Exists(leg => leg.FixtureId == id))
                    .Select(FindLeg)
                    .OfType<Match>()
                    .ToList());
    }

    /// <summary>
    /// Every leg this test has made, kept so the match repository can be answered from them: a
    /// second leg of a pending tie is a fixture with no match in it, and that absence is half
    /// of what the second test is about.
    /// </summary>
    private readonly List<(Guid FixtureId, Match Match)> _playedLegs = [];

    private Match? FindLeg(Guid fixtureId) =>
        _playedLegs.FirstOrDefault(leg => leg.FixtureId == fixtureId).Match;
}
