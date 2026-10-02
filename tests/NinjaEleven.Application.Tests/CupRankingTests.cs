using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using Xunit;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What a cup is worth to the sixty-four clubs it drew, and in what order they are told.
/// </summary>
/// <remarks>
/// The ranking is read out of the bracket's own ties rather than out of a table, so these are
/// built from ties: a cup that has not decided its final has two clubs on the last step, and a
/// cup that has decided it has a champion on a step of his own above the final.
/// </remarks>
public class CupRankingTests
{
    private readonly Mock<ICupTieRepository> _cupTies = new();
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<ICompetitionRepository> _competitions = new();
    private readonly Mock<ISquadStrengthReader> _squadStrength = new();

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _competitionId = Guid.NewGuid();
    private readonly CompetitionSeason _edition;

    /// <summary>
    /// The round before the final. It is read off the final rather than written as a number:
    /// the semis are whichever round the final follows, and a test that says "four" is a test
    /// that is wrong the day a round is added.
    /// </summary>
    private const int SemiFinalRound = CompetitionRules.CupRounds - 1;

    private readonly List<CupTie> _drawn = [];
    private readonly List<(Guid FixtureId, Match Match)> _legs = [];

    public CupRankingTests()
    {
        _edition = CompetitionSeason.Create(_competitionId, _seasonId);

        _competitions
            .Setup(repo => repo.GetSeasonByIdAsync(_edition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_edition);
        _competitions
            .Setup(repo => repo.GetAsync(_competitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Competition.Create("Copa do Brasil", CompetitionType.Cup));

        _cupTies
            .Setup(repo => repo.ListByCompetitionSeasonAsync(_edition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _drawn);
        _matches
            .Setup(repo => repo.ListByFixtureIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _legs.Where(leg => ids.Contains(leg.FixtureId)).Select(leg => leg.Match).ToList());
        _matches
            .Setup(repo => repo.ListStatisticsByMatchIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, MatchStatistics>());
        _squadStrength
            .Setup(reader => reader.ForTeamsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _teams
            .Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(ClubOf).Where(team => team is not null).Select(team => team!).ToList());
    }

    [Fact]
    public async Task The_champion_stands_above_the_final_and_the_runner_up_stands_on_it()
    {
        var champion = Club("Campeão");
        var runnerUp = Club("Vice");
        var one = Club("Semi 1");
        var two = Club("Semi 2");

        GivenATie(SemiFinalRound, champion, one, 2, 1);
        GivenATie(SemiFinalRound, runnerUp, two, 3, 1);
        GivenATie(CompetitionRules.CupRounds, champion, runnerUp, 3, 2);

        var ranking = (await RankingAsync()).ToList();

        Assert.Equal(champion.Id, ranking[0].TeamId);
        Assert.Equal(runnerUp.Id, ranking[1].TeamId);
        Assert.True(ranking[0].IsChampion);
        Assert.True(ranking[1].IsRunnerUp);
        Assert.Equal(new[] { 1, 2, 3, 4 }, ranking.Select(row => row.Position));
    }

    /// <summary>
    /// Two clubs knocked out in the same round are ordered by the championship's chain, and not
    /// by the order the cup happened to draw them in: this is the same rule the league table and
    /// the promotion use, and a cup that invented its own would answer "who had the better run"
    /// differently from every other ranking in the game.
    /// </summary>
    [Fact]
    public async Task Two_clubs_out_in_the_same_round_are_ordered_by_the_championship_tiebreak()
    {
        var better = Club("Melhor campanha");
        var worse = Club("Campanha pior");
        var winner = Club("Campeão");

        // Both clubs are knocked out in the quarter-final, level on points and on goal
        // difference, and the two are told apart by goals scored — the third of the chain, and
        // the championship's own.
        var firstOpponent = Club("Primeiro adversario");
        var secondOpponent = Club("Segundo adversario");
        // Two for one win and two for the defeat, against one for each: the two are level on
        // points and on goal difference, and the club that scored more is ahead.
        GivenATie(1, better, firstOpponent, 2, 0);
        GivenATie(1, worse, secondOpponent, 1, 0);
        GivenATie(3, winner, better, 2, 0);
        GivenATie(3, winner, worse, 1, 0);

        var ranking = (await RankingAsync()).ToList();
        var betterRow = ranking.Single(row => row.TeamId == better.Id);
        var worseRow = ranking.Single(row => row.TeamId == worse.Id);

        Assert.True(betterRow.Position < worseRow.Position);
        Assert.Equal(betterRow.Points, worseRow.Points);
        Assert.Equal(betterRow.GoalDifference, worseRow.GoalDifference);
        Assert.True(betterRow.GoalsFor > worseRow.GoalsFor);
        Assert.Equal(3, betterRow.RoundNumber);
        Assert.Equal(CompetitionRules.TieRoundName(3), betterRow.RoundName);
    }

    [Fact]
    public async Task A_run_is_paid_by_the_round_it_ended_in_and_the_runner_up_is_paid_the_final()
    {
        var champion = Club("Campeão");
        var runnerUp = Club("Vice");
        var semiFinalist = Club("Semi");

        GivenATie(SemiFinalRound, champion, semiFinalist, 2, 0);
        GivenATie(CompetitionRules.CupRounds, champion, runnerUp, 3, 1);

        var ranking = (await RankingAsync()).ToList();

        Assert.Equal(
            PrizeRules.CupChampionPrize,
            ranking.Single(row => row.TeamId == champion.Id).Prize);
        Assert.Equal(
            PrizeRules.CupConsolation(PrizeRules.RunnerUpTieRound),
            ranking.Single(row => row.TeamId == runnerUp.Id).Prize);
        Assert.Equal(
            PrizeRules.CupConsolation(SemiFinalRound),
            ranking.Single(row => row.TeamId == semiFinalist.Id).Prize);
    }

    /// <summary>
    /// A club still in the competition has not been paid anything yet, and the line says so with
    /// nothing rather than a zero: a zero is what a cup pays a club it has finished with, and a
    /// semifinal that is being played right now is not that.
    /// </summary>
    [Fact]
    public async Task A_club_still_in_the_cup_is_paid_nothing_yet()
    {
        var stillIn = Club("Ainda na copa");
        var gone = Club("Ja eliminado");
        var winner = Club("Campeão");

        GivenATie(3, winner, gone, 2, 0);

        var finalRound = Round.Create(_edition.Id, CompetitionRules.CupRounds, CompetitionRules.CupWindow);
        var firstLeg = Fixture.Create(finalRound.Id, winner.Id, stillIn.Id);
        var secondLeg = Fixture.Create(finalRound.Id, stillIn.Id, winner.Id);
        var tie = CupTie.Create(_edition.Id, CompetitionRules.CupRounds, winner.Id, stillIn.Id);
        tie.SetLegs(firstLeg.Id, secondLeg.Id);
        _drawn.Add(tie);

        var ranking = await RankingAsync();

        Assert.Null(ranking.Single(row => row.TeamId == stillIn.Id).Prize);
        Assert.False(ranking.Single(row => row.TeamId == stillIn.Id).IsRunnerUp);

        // The club already out is paid, the finalist is not: a cup pays on the way out and a
        // club that has not gone out has not been paid for going out.
        Assert.Equal(
            PrizeRules.CupConsolation(3),
            ranking.Single(row => row.TeamId == gone.Id).Prize);
    }

    /// <summary>
    /// The two clubs of a final that has not been played are on one step between them, because
    /// they have not been separated by anything yet. Ordering them by anything would be inventing
    /// a result the cup has not had.
    /// </summary>
    [Fact]
    public async Task An_unplayed_final_leaves_its_two_clubs_level_above_the_rest()
    {
        var one = Club("Finalista A");
        var two = Club("Finalista B");

        // A final that has been drawn and not played: a tie with legs and no matches behind them.
        var finalRound = Round.Create(_edition.Id, CompetitionRules.CupRounds, CompetitionRules.CupWindow);
        var firstLeg = Fixture.Create(finalRound.Id, one.Id, two.Id);
        var secondLeg = Fixture.Create(finalRound.Id, two.Id, one.Id);
        var final = CupTie.Create(_edition.Id, CompetitionRules.CupRounds, one.Id, two.Id);
        final.SetLegs(firstLeg.Id, secondLeg.Id);
        _drawn.Add(final);

        var ranking = (await RankingAsync()).ToList();

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].RoundNumber, ranking[1].RoundNumber);
        Assert.All(ranking, row => Assert.Null(row.Prize));
        Assert.All(ranking, row => Assert.False(row.IsChampion || row.IsRunnerUp));
    }

    [Fact]
    public async Task A_cup_nobody_has_played_yet_has_nothing_to_rank()
    {
        Assert.Empty(await RankingAsync());
    }

    private async Task<IReadOnlyList<Application.Models.CupRankingRow>> RankingAsync()
    {
        var bracket = await new CupBracketService(
            _cupTies.Object, _matches.Object, _teams.Object, _competitions.Object, _squadStrength.Object)
            .GetAsync(_edition.Id);

        return bracket!.Ranking;
    }

    /// <summary>
    /// One tie of a round, decided on an aggregate — the state the cup leaves a tie in once both
    /// legs are behind it.
    /// </summary>
    /// <remarks>
    /// The aggregate is what a test says, not the two legs it came from: a cup tie is read by
    /// the sum, and a test that had to add the legs up in its head was a test that would get
    /// the aggregate level and ask the cup for a shootout it had not arranged. The legs are
    /// spread one to each — `n x 0` and `0 x m` — which is a tie decided in two halves rather
    /// than two draws, and a tie's story is not what the ranking reads.
    /// </remarks>
    private void GivenATie(int roundNumber, Team homeClub, Team awayClub, int homeAggregate, int awayAggregate)
    {
        if (homeAggregate == awayAggregate)
        {
            throw new ArgumentException("A level tie goes to penalties; arrange a shootout instead.");
        }

        var home = homeClub.Id;
        var away = awayClub.Id;
        var round = Round.Create(_edition.Id, roundNumber, CompetitionRules.CupWindow);
        var firstLegFixture = Fixture.Create(round.Id, home, away);
        var secondLegFixture = Fixture.Create(round.Id, away, home);

        var tie = CupTie.Create(_edition.Id, roundNumber, home, away);
        tie.SetLegs(firstLegFixture.Id, secondLegFixture.Id);
        tie.Resolve(homeAggregate, awayAggregate);

        _drawn.Add(tie);
        _legs.Add((firstLegFixture.Id, GivenALeg(firstLegFixture.Id, home, away, homeAggregate, 0)));
        _legs.Add((secondLegFixture.Id, GivenALeg(secondLegFixture.Id, away, home, 0, awayAggregate)));
    }

    private static Match GivenALeg(Guid fixtureId, Guid home, Guid away, int homeGoals, int awayGoals)
    {
        var leg = Match.Create(fixtureId, home, away, CompetitionType.Cup, CompetitionRules.CupWindow);
        leg.ApplyEngineState(90, homeGoals, awayGoals, 1);
        leg.Finish();

        return leg;
    }

    private readonly Dictionary<Guid, Team> _clubs = [];

    private Team Club(string name)
    {
        var team = Team.Create(name, name[..3].ToUpperInvariant(), "#E07B00", "#2B2B2B", 80);
        _clubs[team.Id] = team;

        return team;
    }

    private Team? ClubOf(Guid teamId) => _clubs.GetValueOrDefault(teamId);
}