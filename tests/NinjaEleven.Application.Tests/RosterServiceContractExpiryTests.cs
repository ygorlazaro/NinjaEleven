using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What a season boundary does to a contract: it ends the ones that are spent, and it warns
/// the club about the one about to be.
///
/// <para>
/// Two facts and one order. A contract signed for two seasons in season one is a free man at
/// the end of season two and not at the end of season three, and a contract with one season
/// left is a warning rather than an ending — because there is still a season in which the
/// manager can do something about it. Getting the boundary wrong by a season either way is
/// quiet: a club keeps a man it never paid for, or loses one it did.
/// </para>
///
/// <para>
/// The warning is sent after the season's states exist, because it prices the man on the
/// contract as he will be for the season that is opening. Sent before them, every lookup misses
/// and the world goes a whole season without telling anybody anything.
/// </para>
/// </summary>
public class RosterServiceContractExpiryTests
{
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<ITransferRepository> _transfers = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IInboxMessageRepository> _messages = new();

    private readonly Team _club = CreateAManagerClub();

    // The boundary these tests stand on is the end of the second season, because that is the
    // first boundary at which a two-season deal is actually spent. Closing season one instead
    // would prove nothing either way: a contract signed for two has all but one of its seasons
    // left at that point.
    private readonly Season _previous = Season.Create(2, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31));
    private readonly Season _season = Season.Create(3, new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31));

    private readonly List<Player> _everyone = [];
    private readonly List<PlayerSeasonState> _added = [];
    private readonly List<TeamMembership> _contracts = [];
    private readonly List<TeamMembership> _updated = [];

    public RosterServiceContractExpiryTests()
    {
        _players.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _everyone.ToList());
        _players.Setup(repo => repo.ListAllSeasonStatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _added.ToList());
        _players.Setup(repo => repo.AddSeasonStateAsync(It.IsAny<PlayerSeasonState>(), It.IsAny<CancellationToken>()))
            .Callback((PlayerSeasonState state, CancellationToken _) => _added.Add(state))
            .Returns(Task.CompletedTask);

        _teams.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { _club });
        _teams.Setup(repo => repo.GetAsync(_club.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_club);
        _teams.Setup(repo => repo.GetLiveContractsAsync(_club.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contracts.Where(contract => contract.EndDate is null).ToList());
        _teams.Setup(repo => repo.ListAllContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contracts.Where(contract => contract.EndDate is null).ToList());
        _teams.Setup(repo => repo.UpdateMembership(It.IsAny<TeamMembership>()))
            .Callback((TeamMembership contract) => _updated.Add(contract));

        _transfers.Setup(repo => repo.ListWaitingForSeasonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());
    }

    [Fact]
    public async Task AContractOfTwoSeasonsIsSpentAtTheEndOfTheSecond()
    {
        // The boundary, exactly. Signed in season one for two seasons, he covers seasons one
        // and two and is a free man when the second closes — not at the end of the third, which
        // is what a season too generous with somebody's contract looks like.
        AContractedPlayer("Dois Temporadas", seasons: 2, wage: 400_000m, startSeasonNumber: 1);

        await Service().OpenSeasonAsync(_previous, _season);

        var contract = Assert.Single(_contracts);

        Assert.NotNull(contract.EndDate);
        Assert.Equal(_previous.EndDate, contract.EndDate);
        Assert.Contains(contract, _updated);
    }

    [Fact]
    public async Task AContractWithASeasonStillToPlayIsNotSpent()
    {
        // Three seasons signed in season one has season three still to play, and a manager with
        // a season of football left in him is not a free agent on the morning it opens.
        AContractedPlayer("Tres Temporadas", seasons: 3, wage: 400_000m, startSeasonNumber: 1);

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.Null(Assert.Single(_contracts).EndDate);
        Assert.Empty(_updated);
    }

    [Fact]
    public async Task AMenRenamedInTheSeasonThatClosedKeepsHisContract()
    {
        // The renewal resets the clock, and the clock is what is read. A man whose deal was
        // signed for three seasons in season two has two of them still to play when that season
        // closes, and ending his contract there would undo the renewal the moment it was
        // agreed.
        AContractedPlayer("Renovado", seasons: 3, wage: 400_000m, startSeasonNumber: 2);

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.Null(Assert.Single(_contracts).EndDate);
        Assert.Empty(_updated);
    }

    [Fact]
    public async Task AClubIsToldWhenAManEntersHisLastSeason()
    {
        // The warning is the whole reason the expiry rule is survivable: a contract ending
        // without ever being announced is a man walking out of the door on the last day of the
        // season, and a manager who never heard is a manager who was never told.
        AContractedPlayer("Na Ultima", seasons: 2, wage: 400_000m, startSeasonNumber: 2);

        await Service().OpenSeasonAsync(_previous, _season);

        var warning = Assert.Single(_posted);

        Assert.Equal(_club.Id, warning.RecipientTeamId);
        Assert.Contains("Na Ultima", warning.Subject);

        // Keyed by the contract rather than by the man or the season, so a world that opens
        // this season twice sends it once.
        Assert.Equal($"contract:{Assert.Single(_contracts).Id}:expiring", warning.Reference);
    }

    [Fact]
    public async Task TheWarningPricestheManAsHeWillBeForTheSeasonOpening()
    {
        // Priced off the state the world has just written, which is the man as the manager will
        // see him in the season he can still act in. The contract is on four hundred thousand
        // and is not revalued by the passage of a season, so a warning that quotes the renewal
        // as the same number is a warning that copied the contract rather than reading the man —
        // and a manager renews against that number.
        var man = AContractedPlayer("Na Ultima", seasons: 2, wage: 400_000m, startSeasonNumber: 2);

        await Service().OpenSeasonAsync(_previous, _season);

        var state = Assert.Single(_added);
        var warning = Assert.Single(_posted);

        // The service says the figure has not moved when, and only when, the two agree.
        Assert.NotEqual(400_000m, PlayerValuation.SeasonWage(man, state));
        Assert.DoesNotContain("não mudou", warning.Body);
        Assert.Contains("passa para", warning.Body);
    }

    [Fact]
    public async Task AMemberWithSeasonsToSpareIsNotWarnedAbout()
    {
        // One warning about the right men, rather than one about the whole squad. A club told
        // every season that all twenty-three contracts were expiring has learned nothing and
        // will learn to ignore the post.
        AContractedPlayer("Ainda Tem Tres", seasons: 3, wage: 400_000m, startSeasonNumber: 2);

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.Empty(_posted);
    }

    [Fact]
    public async Task AFreeAgentIsNobodyToWarn()
    {
        // He is on no contract, so there is no last season of it to warn anybody about. The
        // warning follows contracts and not men, or every free agent in the world would be
        // announced to a club he has never played for.
        APlayerOfAge(26, "Sem Clube");

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.Empty(_posted);
    }

    [Fact]
    public async Task AContractAlreadySpentIsNotWarnedAboutOnTopOfBeingEnded()
    {
        // Both halves of the boundary in one player. He is ended because his seasons are gone,
        // and he is not warned a moment before, because the season that would have been his last
        // is the one that just closed.
        AContractedPlayer("Ja Acabado", seasons: 1, wage: 400_000m, startSeasonNumber: 1);

        await Service().OpenSeasonAsync(_previous, _season);

        Assert.NotNull(Assert.Single(_contracts).EndDate);
        Assert.Empty(_posted);
    }

    // --- Helpers ------------------------------------------------------------------

    private List<InboxMessage> _posted = [];

    /// <summary>
    /// A club somebody is running, because the inbox refuses to deliver to one nobody is: a
    /// warning sent to a stadium with no manager in it is a warning that was never read.
    /// </summary>
    private static Team CreateAManagerClub()
    {
        var club = Team.Create("Esporte Clube Riachuelo", "Riachuelo", "#0a5", "#fff");
        club.MarkAsManagerClub();

        return club;
    }

    private RosterService Service() => new(
        _players.Object,
        _teams.Object,
        _transfers.Object,
        _unitOfWork.Object,
        Inbox(InboxTestFactory.Create(_teams, _messages)),
        NullLogger<RosterService>.Instance);

    /// <summary>
    /// An inbox that keeps what it was told, because a warning nobody can read is not a test
    /// of a warning.
    /// </summary>
    private InboxService Inbox(InboxService inner)
    {
        _posted = [];

        _messages.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _posted.Add(message))
            .Returns(Task.CompletedTask);

        return inner;
    }

    private Player APlayerOfAge(int age, string name)
    {
        var player = Player.Create(
            name, age, Position.ATT,
            speed: 12, accuracy: 12, dribbling: 12, heading: 12, strength: 12,
            goalkeeperPower: 0, reflexes: 0);

        _everyone.Add(player);

        return player;
    }

    private Player AContractedPlayer(
        string name,
        int seasons,
        decimal wage,
        int startSeasonNumber = 1)
    {
        var player = APlayerOfAge(26, name);

        _contracts.Add(TeamMembership.Create(
            player.Id, _club.Id, _previous.StartDate, seasons, startSeasonNumber, wage: wage));

        return player;
    }
}