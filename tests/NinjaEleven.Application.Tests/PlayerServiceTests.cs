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
        new DateOnly(1997, 3, 28),
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

    private PlayerService CreateService() =>
        new(_players.Object, _teams.Object, _seasons.Object, _matches.Object);
}
