using Moq;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A club's scorers table is the one page about a club that must never lose a name, and three
/// things have to hold for it to be worth reading.
///
/// - **The order is the football one.** Goals first, and then the fewest games for them: a
///   striker with eight in ten and one with eight in twenty are not level, and a table that
///   ranked them level would be hiding the whole difference between them.
/// - **A man who has left is kept, and says so.** He is in the answer with the flag against
///   him rather than filtered out by the service, because the service does not know which
///   question was asked — a screen that only ever showed the men under contract would quietly
///   rewrite the club's history every time a window opened.
/// - **The rate is worked out here.** A number the client invents is a number two clients
///   may invent differently, and a striker with no appearance is given null rather than a
///   zero, because nothing at all is a fact about a man who never played.
/// </summary>
public class TeamServiceScorersTests
{
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);

    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _seasonId = Guid.NewGuid();

    /// <summary>
    /// The men of the world, so the batched reader the service uses has an answer. A club's
    /// scorers list is a page about a hundred names, and reading them one at a time was a
    /// hundred queries for one page.
    /// </summary>
    private readonly List<Player> _roster = new();

    private TeamService Service() => new(
        _teams.Object, _players.Object, _seasons.Object, _matches.Object, Mock.Of<IUnitOfWork>());

    private Player GivenPlayer(string name, int age = 26)
    {
        var player = Player.Create(
            name,
            age,
            Position.ATT,
            speed: 15,
            accuracy: 15,
            dribbling: 14,
            heading: 13,
            strength: 13,
            goalkeeperPower: 0,
            reflexes: 8);

        _roster.Add(player);
        _players.Setup(repository => repository.GetAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _teams.Setup(repository => repository.GetPlayersAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> playerIds, CancellationToken _) =>
                Task.FromResult<Dictionary<Guid, Player>>(
                    _roster.Where(roster => playerIds.Contains(roster.Id))
                        .ToDictionary(roster => roster.Id)));

        return player;
    }

    private void GivenTheClubExists()
    {
        var team = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 80);
        _teams.Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _seasons.Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task Order_is_goals_first_and_then_the_fewest_games_for_them()
    {
        GivenTheClubExists();
        var prolific = GivenPlayer("Zé Grande");
        var efficient = GivenPlayer("Almino Fino");

        _players
            .Setup(repository => repository.ListClubScorerLinesAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CompetitionType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClubScorerLine>
            {
                // Same goals, more games: the same number and a different striker.
                new() { PlayerId = prolific.Id, TeamId = _teamId, Goals = 8, Started = 20, CameOn = 0 },
                new() { PlayerId = efficient.Id, TeamId = _teamId, Goals = 8, Started = 10, CameOn = 0 }
            });
        _teams.Setup(repository => repository.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamMembership>());

        var scorers = await Service().GetScorersAsync(_teamId, _seasonId);

        Assert.Collection(
            scorers,
            first =>
            {
                Assert.Equal("Almino Fino", first.PlayerName);
                Assert.Equal(1, first.Position);
            },
            second =>
            {
                Assert.Equal("Zé Grande", second.PlayerName);
                Assert.Equal(2, second.Position);
            });
    }

    [Fact]
    public async Task A_player_who_has_left_stays_on_the_list_with_the_flag_against_him()
    {
        GivenTheClubExists();
        var legend = GivenPlayer("T Historically");
        var present = GivenPlayer("Ainda Aqui");

        _players
            .Setup(repository => repository.ListClubScorerLinesAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CompetitionType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClubScorerLine>
            {
                new() { PlayerId = legend.Id, TeamId = _teamId, Goals = 40, Started = 200, CameOn = 0 },
                new() { PlayerId = present.Id, TeamId = _teamId, Goals = 3, Started = 12, CameOn = 0 }
            });

        // A live contract is a membership with no end date, so the club holds only one of them.
        var live = TeamMembership.Create(present.Id, _teamId, new DateOnly(2024, 1, 1));
        _teams.Setup(repository => repository.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { live });

        var scorers = await Service().GetScorersAsync(_teamId, _seasonId);

        Assert.Equal(2, scorers.Count);
        Assert.False(scorers.Single(row => row.PlayerId == legend.Id).IsStillAtClub);
        Assert.True(scorers.Single(row => row.PlayerId == present.Id).IsStillAtClub);
    }

    [Fact]
    public async Task A_player_who_never_appeared_has_no_rate_and_not_a_zero()
    {
        GivenTheClubExists();
        var ghost = GivenPlayer("Nunca Jogou");

        _players
            .Setup(repository => repository.ListClubScorerLinesAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CompetitionType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClubScorerLine>
            {
                new() { PlayerId = ghost.Id, TeamId = _teamId, Goals = 2, Started = 0, CameOn = 0 }
            });
        _teams.Setup(repository => repository.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamMembership>());

        var scorers = await Service().GetScorersAsync(_teamId, _seasonId);

        Assert.Null(scorers.Single().GoalsPerAppearance);
    }

    [Fact]
    public async Task The_rate_is_goals_over_the_games_he_actually_played()
    {
        GivenTheClubExists();
        var striker = GivenPlayer("Ataque Real");

        _players
            .Setup(repository => repository.ListClubScorerLinesAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CompetitionType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClubScorerLine>
            {
                // 8 in 10 started and 2 off the bench is eight goals in twelve games.
                new() { PlayerId = striker.Id, TeamId = _teamId, Goals = 8, Started = 10, CameOn = 2 }
            });
        _teams.Setup(repository => repository.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamMembership>());

        var scorers = await Service().GetScorersAsync(_teamId, _seasonId);

        Assert.Equal(0.67, scorers.Single().GoalsPerAppearance);
    }

    [Fact]
    public async Task A_club_that_does_not_exist_has_no_scorers_table()
    {
        _teams.Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Service().GetScorersAsync(Guid.NewGuid(), _seasonId));
    }
}
