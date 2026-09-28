using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What opening a season does to the men in it: everybody is a year older, the rule announces
/// the last season of whoever it reaches, and the announcement is carried out when the season
/// it was about is over.
///
/// The order of those three things is the whole test. A world that announced its retirements
/// off last season's ages would lose a player a year and keep him a year too long, and a world
/// that left the announcement on the state of a season already gone would give a man a second
/// season he never agreed to play. So the tests here are about which season a fact lands in,
/// not about the flag itself.
/// </summary>
public class RosterServiceTests
{
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<ITransferRepository> _transfers = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Team _club = Team.Create("Esporte Clube Riachuelo", "Riachuelo", "#0a5", "#fff");
    private readonly Season _previous = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
    private readonly Season _season = Season.Create(2, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31));

    private readonly List<Player> _everyone = new();
    private readonly List<PlayerSeasonState> _added = new();
    private readonly List<PlayerSeasonState> _lastSeasonStates = new();
    private readonly List<TeamMembership> _memberships = new();

    public RosterServiceTests()
    {
        _players.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _everyone.ToList());
        _players.Setup(repo => repo.ListAllSeasonStatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _lastSeasonStates.ToList());
        _players.Setup(repo => repo.AddSeasonStateAsync(It.IsAny<PlayerSeasonState>(), It.IsAny<CancellationToken>()))
            .Callback((PlayerSeasonState state, CancellationToken _) => _added.Add(state))
            .Returns(Task.CompletedTask);

        _teams.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { _club });
        _teams.Setup(repo => repo.GetLiveContractsAsync(_club.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _memberships.Where(m => m.EndDate is null).ToList());
        _teams.Setup(repo => repo.ListAllContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _memberships.Where(m => m.EndDate is null).ToList());

        _transfers.Setup(repo => repo.ListWaitingForSeasonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());
    }

    private RosterService Service() => new(
        _players.Object,
        _teams.Object,
        _transfers.Object,
        _unitOfWork.Object,
        NullLogger<RosterService>.Instance);

    private Player APlayerOfAge(int age, string name = "Jogador")
    {
        var player = Player.Create(
            name,
            age,
            Position.ATT,
            speed: 12,
            accuracy: 12,
            dribbling: 12,
            heading: 12,
            strength: 12,
            goalkeeperPower: 0,
            reflexes: 0);

        _everyone.Add(player);

        return player;
    }

    private void OnAContract(Player player) =>
        _memberships.Add(TeamMembership.Create(
            player.Id, _club.Id, _previous.StartDate,
            NinjaEleven.Domain.Finance.FinanceRules.DefaultContractSeasons, 1));

    [Fact]
    public async Task OpeningASeasonMakesEverybodyExactlyOneYearOlderAndStopsAtTheOldestAge()
    {
        var striker = APlayerOfAge(26, "Zé Grande");
        var veteran = APlayerOfAge(Player.OldestAge, "Veterano");

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.Equal(27, striker.Age);

        // The cap is a fact about the world rather than an accident: a man who has been
        // playing since the first season does not become a hundred and one.
        Assert.Equal(Player.OldestAge, veteran.Age);
    }

    [Fact]
    public async Task ASeasonStateIsGivenToEveryPlayerAndNobodyIsCreated()
    {
        var players = Enumerable.Range(0, 5).Select(index => APlayerOfAge(24, $"Jogador {index}")).ToList();

        var result = await Service().OpenSeasonAsync(_previous, _season);

        Assert.Equal(players.Count, result.SquadsCreated);
        Assert.Equal(players.Count, _added.Count);
        Assert.All(_added, state => Assert.Equal(_season.Id, state.SeasonId));
    }

    [Fact]
    public async Task APlayerWithAContractOpensTheSeasonAtHisClubAndOneWithoutOpensItWithNoClub()
    {
        var signed = APlayerOfAge(24, "Contratado");
        var free = APlayerOfAge(24, "Livre");
        OnAContract(signed);

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.Equal(_club.Id, _added.Single(state => state.PlayerId == signed.Id).TeamId);
        Assert.Null(_added.Single(state => state.PlayerId == free.Id).TeamId);
    }

    [Fact]
    public async Task TheRuleAnnouncesTheLastSeasonOfWhoeverItReachesAndOfNobodyElse()
    {
        var tooYoung = APlayerOfAge(35, "Ainda Jovem");
        var first = APlayerOfAge(36, "O Primeiro Do Ano");
        var last = APlayerOfAge(41, "O Último Do Ano");
        var past = APlayerOfAge(42, "Já Passou");
        var older = APlayerOfAge(43, "Depois da Banda");

        var result = await Service().OpenSeasonAsync(_previous, _season);

        // 36 becomes 37, the first year of the band, and 41 becomes 42, the last. 42 becomes 43
        // and 43 becomes 44: past the band the rule stops announcing anything, and a man who is
        // still there is not being asked to leave — the game simply stops giving a club the
        // notice it would plan a summer around.
        Assert.Equal(2, result.Announced);
        Assert.True(_added.Single(state => state.PlayerId == first.Id).Retiring);
        Assert.True(_added.Single(state => state.PlayerId == last.Id).Retiring);
        Assert.False(_added.Single(state => state.PlayerId == tooYoung.Id).Retiring);
        Assert.False(_added.Single(state => state.PlayerId == past.Id).Retiring);
        Assert.False(_added.Single(state => state.PlayerId == older.Id).Retiring);
    }

    [Fact]
    public async Task ARetiredManIsAnnouncedForOneSeasonAndThenHasNoStateAtAll()
    {
        var veteran = APlayerOfAge(36, "VETERANO");
        OnAContract(veteran);

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.True(_added.Single(state => state.PlayerId == veteran.Id).Retiring);

        // The season the announcement was about is over, and the next one opens without him:
        // the contract ends on the last day of the season he announced in, and the announcement
        // is acted on where it was made rather than being read again.
        _lastSeasonStates.Add(_added.Single(state => state.PlayerId == veteran.Id));
        _added.Clear();

        var next = Season.Create(3, new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31));
        var result = await Service().OpenSeasonAsync(_season, next);

        Assert.DoesNotContain(_added, state => state.PlayerId == veteran.Id);
        Assert.Equal(0, _added.Count(state => state.PlayerId == veteran.Id));

        // The contract ends on the last day of the season he announced in — the one he played,
        // not the one the retirement was noticed in — so the club stops carrying him when that
        // season stops and not a matchday later.
        var membership = _memberships.Single(m => m.PlayerId == veteran.Id);
        Assert.NotNull(membership.EndDate);
        Assert.Equal(_season.EndDate, membership.EndDate);
    }

}
