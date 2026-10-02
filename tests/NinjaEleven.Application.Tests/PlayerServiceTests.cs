using Moq;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A profile is the career of one player, and it is assembled by the service rather than by
/// the screen: two screens asking "how many goals" must not be free to answer differently.
///
/// The two numbers worth holding are the season's and the career's. They are the same lines
/// filtered two ways, so they cannot disagree with each other or with the history the manager
/// can see and count. The season state carries a goals counter of its own, and it is
/// deliberately not what is shown.
/// </summary>
public class PlayerServiceTests
{
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _lastSeasonId = Guid.NewGuid();
    private readonly Team _team = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 80);
    private readonly Player _player = Player.Create(
        "Edair Freire",
        28,
        Position.ATT,
        speed: 15,
        accuracy: 16,
        dribbling: 12,
        heading: 11,
        strength: 14,
        goalkeeperPower: 0,
        reflexes: 0);

    public PlayerServiceTests()
    {
        _players.Setup(repo => repo.GetAsync(_player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_player);
        _players.Setup(repo => repo.GetSeasonStateAsync(_player.Id, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerSeasonState.Create(_player.Id, _seasonId, _team.Id, 80));
        _teams.Setup(repo => repo.GetAsync(_team.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_team);
        // Who is on the club's books. A profile reads the contract off the membership rather
        // than off a number it kept to itself, so the card and the wage bill cannot disagree
        // about how long a man is committed for.
        _teams.Setup(repo => repo.GetSquadAsync(_team.Id, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamMembership>
            {
                TeamMembership.Create(_player.Id, _team.Id, new DateOnly(2026, 1, 1))
            });
    }

    private void History(params PlayerMatchRecord[] lines) =>
        _matches.Setup(repo => repo.GetPlayerHistoryAsync(_player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lines);

private static PlayerMatchRecord Line(
        Guid matchId,
        Guid? seasonId,
        bool started = true,
        bool cameOn = false,
        int goals = 0,
        int saves = 0) => new()
    {
        MatchId = matchId,
        SeasonId = seasonId,
        Started = started,
        CameOn = cameOn,
        Goals = goals,
        Saves = saves,
        HomeGoals = 2,
        AwayGoals = 1,
        IsHome = true,
        OpponentName = "Estrela do Norte",
        RoundNumber = 3
    };

    [Fact]
    public async Task The_season_and_the_career_are_the_same_lines_filtered_two_ways()
    {
        History(
            Line(Guid.NewGuid(), _seasonId, goals: 2),
            Line(Guid.NewGuid(), _seasonId, started: false, cameOn: true, goals: 1),
            Line(Guid.NewGuid(), _lastSeasonId, goals: 4));

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        Assert.Equal(3, profile.Total.Appearances);
        Assert.Equal(7, profile.Total.Goals);

        Assert.Equal(2, profile.Season.Appearances);
        Assert.Equal(3, profile.Season.Goals);

        // A man who came off the bench did not start, whatever a single number of appearances
        // would have said. "14 (3)" is made of these two.
        Assert.Equal(2, profile.Total.Started);
        Assert.Equal(1, profile.Total.CameOn);
    }

    /// <summary>
    /// The season state has a goals counter of its own and it is not what is shown. A
    /// profile that printed the counter in one place and the sum of the history in another
    /// would be two career numbers, and the manager could not tell which one to believe.
    /// </summary>
    [Fact]
    public async Task The_goals_shown_are_the_ones_in_the_history()
    {
        History(Line(Guid.NewGuid(), _seasonId, goals: 2));

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        Assert.Equal(2, profile.Season.Goals);
    }

    /// <summary>
    /// A profile opened without a season is the whole career, and the season column is then
    /// empty rather than quietly showing one of them as the other.
    /// </summary>
    [Fact]
    public async Task A_career_opened_without_a_season_is_the_whole_history()
    {
        History(
            Line(Guid.NewGuid(), _seasonId, goals: 2),
            Line(Guid.NewGuid(), _lastSeasonId, goals: 4));

        var profile = await CreateService().GetProfileAsync(_player.Id);

        Assert.Equal(2, profile.History.Count);
        Assert.Equal(6, profile.Total.Goals);
        Assert.Equal(0, profile.Season.Appearances);
    }

    /// <summary>
    /// A goalkeeper's saves are a career of their own, and they are carried on his lines for
    /// the same reason his goals are: a keeper is judged on what he kept out.
    /// </summary>
    [Fact]
    public async Task A_goalkeepers_saves_are_summed_over_his_matches()
    {
        History(
            Line(Guid.NewGuid(), _seasonId, saves: 4),
            Line(Guid.NewGuid(), _seasonId, saves: 6));

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        Assert.Equal(10, profile.Season.Saves);
    }

    /// <summary>
    /// A line with the club and the season it names, which is what a per-season row is grouped
    /// by. It is built on the shorter overload above so the two cannot drift apart on the
    /// fields they share.
    /// </summary>
    private static PlayerMatchRecord LineIn(
        Guid seasonId,
        Guid teamId,
        string teamName,
        int goals = 0,
        bool isHome = true,
        int homeGoals = 2,
        int awayGoals = 1,
        int yellowCards = 0,
        bool injured = false)
    {
        var line = Line(Guid.NewGuid(), seasonId, goals: goals);

        line.TeamId = teamId;
        line.TeamName = teamName;
        line.SeasonName = "Temporada";
        line.IsHome = isHome;
        line.HomeGoals = homeGoals;
        line.AwayGoals = awayGoals;
        line.YellowCards = yellowCards;
        line.WasInjured = injured;

        return line;
    }

    /// <summary>
    /// A career told season by season: one row per season, in the shirt it was played in.
    ///
    /// <para>
    /// The rows are what a manager reads before signing a man — not how he has been lately,
    /// but what a season of him looked like — so the numbers on them are the same sums the
    /// match table above is drawn from. Nothing here is worked out on the client.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_career_is_told_one_season_at_a_time()
    {
        History(
            LineIn(_seasonId, _team.Id, "Clube Aurora", goals: 3),
            LineIn(_seasonId, _team.Id, "Clube Aurora", goals: 1, yellowCards: 2, injured: true),
            LineIn(_lastSeasonId, _team.Id, "Clube Aurora", goals: 5));

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        Assert.Equal(2, profile.Seasons.Count);

        var thisSeason = profile.Seasons.Single(line => line.SeasonId == _seasonId);
        Assert.Equal(2, thisSeason.Line.Appearances);
        Assert.Equal(4, thisSeason.Line.Goals);
        Assert.Equal(2, thisSeason.Line.YellowCards);
        Assert.Equal(1, thisSeason.Line.Injuries);
        Assert.Equal(_team.Id, thisSeason.TeamId);
        Assert.Equal("Clube Aurora", thisSeason.TeamName);

        var lastSeason = profile.Seasons.Single(line => line.SeasonId == _lastSeasonId);
        Assert.Equal(5, lastSeason.Line.Goals);

        // The rows are the history, so their goals add up to the career's. Two tables on one
        // page that did not add up to each other would leave the manager choosing between them.
        Assert.Equal(profile.Total.Goals, profile.Seasons.Sum(line => line.Line.Goals));
    }

    /// <summary>
    /// A striker who moved in January has two lines in one season, and one line would credit
    /// the second club with the first one's goals.
    /// </summary>
    [Fact]
    public async Task A_season_spent_at_two_clubs_is_two_lines()
    {
        var rival = Guid.NewGuid();

        History(
            LineIn(_seasonId, _team.Id, "Clube Aurora", goals: 4),
            LineIn(_seasonId, rival, "Estrela do Norte", goals: 6));

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        Assert.Equal(2, profile.Seasons.Count);
        Assert.Equal(4, profile.Seasons.Single(line => line.TeamId == _team.Id).Line.Goals);
        Assert.Equal(6, profile.Seasons.Single(line => line.TeamId == rival).Line.Goals);
    }

    /// <summary>
    /// A win is his club's win: an away victory read off the raw home and away numbers would be
    /// counted as a defeat, which is the mistake this holds.
    /// </summary>
    [Fact]
    public async Task A_seasons_results_are_his_club_results()
    {
        History(
            LineIn(_seasonId, _team.Id, "Clube Aurora", homeGoals: 2, awayGoals: 1),
            LineIn(_seasonId, _team.Id, "Clube Aurora", homeGoals: 0, awayGoals: 0),
            LineIn(_seasonId, _team.Id, "Clube Aurora", homeGoals: 0, awayGoals: 1),
            // Away: his club scored two and conceded one. Read the wrong way round this is a
            // defeat, and a striker's season would carry a win that never happened.
            LineIn(_seasonId, _team.Id, "Clube Aurora", isHome: false, homeGoals: 1, awayGoals: 2));

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        var season = profile.Seasons.Single();

        Assert.Equal(2, season.Wins);
        Assert.Equal(1, season.Draws);
        Assert.Equal(1, season.Losses);
    }

    /// <summary>
    /// A player who has never played has no season to tell, and an empty table is the honest
    /// answer rather than a row of zeros.
    /// </summary>
    [Fact]
    public async Task A_player_with_no_history_has_no_seasons()
    {
        History();

        var profile = await CreateService().GetProfileAsync(_player.Id, _seasonId);

        Assert.Empty(profile.Seasons);
    }

    private PlayerService CreateService() =>
        new(_players.Object, _teams.Object, _seasons.Object, _matches.Object);
}
