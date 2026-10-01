using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The market's answer for the club nobody is playing for: a rival club picking one of the
/// manager's men, and the manager being told.
///
/// <para>
/// The gap this covers is a sentence in the code that used to be a lie. The sweep that moves
/// computer clubs said the offer "sits in the manager's inbox until he accepts or refuses it"
/// and then wrote nothing at all — the proposal was created and the manager never heard of it,
/// so a rival club could have been circling his best player all season and the only place that
/// said so was a screen he had to know to open and remember to visit.
/// </para>
///
/// <para>
/// A pending proposal and a proposal the manager knows about are the same row and two entirely
/// different games. One of them ends in him selling; the other ends in him losing a player
/// because nobody ever presented him with a decision.
/// </para>
///
/// <para>
/// Note the shape of the fixture: a rival that is above the minimum squad size, because a
/// club below it is sent after free agents and never reads anybody's contract. Every assertion
/// is about one named man rather than about the count of messages, because a sweep is allowed
/// to table more than one offer in a round and a test that insisted on exactly one would be
/// asserting the shape of the rival's squad rather than the news reaching the manager.
/// </para>
/// </summary>
public class TransferNpcOfferInboxTests
{
    private const string TheStar = "Valter Cassini";

    private readonly Mock<ITransferRepository> _transfers = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<IInboxMessageRepository> _messages = new(MockBehavior.Loose);

    private readonly Team _managerClub;
    private readonly Team _rival;
    private readonly Season _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    private readonly List<Player> _everybody = [];
    private readonly List<PlayerSeasonState> _states = [];
    private readonly List<TeamMembership> _contracts = [];
    private readonly List<Transfer> _proposals = [];
    private readonly List<InboxMessage> _written = [];

    private Player _star = null!;

    public TransferNpcOfferInboxTests()
    {
        _managerClub = Team.Create("Esporte Clube Riachuelo", "Riachuelo", "#0a5", "#fff");
        _managerClub.MarkAsManagerClub();
        _rival = Team.Create("Porto Marítimo", "Porto", "#06c", "#fff");

        _seasons.Setup(repo => repo.GetAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);
        _seasons.Setup(repo => repo.GetByNumberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, CancellationToken __) => _season);

        _teams.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { _managerClub, _rival });
        _teams.Setup(repo => repo.GetAsync(_managerClub.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_managerClub);
        _teams.Setup(repo => repo.GetAsync(_rival.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_rival);
        _teams.Setup(repo => repo.ListAllContractsAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contracts.ToList());
        _teams.Setup(repo => repo.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contracts.ToList());

        _players.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _everybody.ToList());
        _players.Setup(repo => repo.ListAllSeasonStatesAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _states.ToList());

        _transfers.Setup(repo => repo.ListLiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());
        _transfers.Setup(repo => repo.ListAcceptedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());
        _transfers.Setup(repo => repo.AddAsync(It.IsAny<Transfer>(), It.IsAny<CancellationToken>()))
            .Callback((Transfer proposal, CancellationToken _) => _proposals.Add(proposal))
            .Returns(Task.CompletedTask);

        // A loose mock answers a list-shaped call with null rather than an empty list, and the
        // sweep asks for the table on its way out as well as on its way in.
        _transfers.Setup(repo => repo.ListPendingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _proposals.Where(proposal => proposal.Status == TransferStatus.Pending).ToList());

        // A season with no round played is a season in its first matchday, which is the round a
        // window is read against and the one that decides where a proposal would land.
        _competitions.Setup(repo => repo.ListSeasonViewsAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompetitionSeasonView>());
        _matches.Setup(repo => repo.GetMatchDaysAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay>());

        _finance.Setup(repo => repo.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinanceMovement?)null);
    }

    [Fact]
    public async Task ARivalClubPickingOneOfMyPlayersIsSomethingIMustBeToldAbout()
    {
        // The whole feature. The rival's offer lands in my inbox the moment it is made, names me
        // the man, names me the club that wants him, and carries the money.
        GivenAWorldWhereTheRivalIsShopping();

        await TheSweep();

        var message = TheMessageAbout(TheStar);

        Assert.Equal(InboxCategory.TransferOffer, message.Category);
        Assert.Equal(_managerClub.Id, message.RecipientTeamId);
        Assert.Contains("Porto", message.Subject);
        Assert.Contains("L$", message.Subject);
        Assert.Equal("/transfer", message.LinkRoute);
        Assert.Equal("Ver o mercado", message.LinkLabel);
    }

    [Fact]
    public async Task TheOfferIsPutInFrontOfMeBeforeAnythingHasMoved()
    {
        // A deal nobody has answered is not a departure. If the message went out after the books
        // were touched I would be reading about a transfer that had already happened and could
        // no longer be refused.
        GivenAWorldWhereTheRivalIsShopping();

        await TheSweep();

        var proposal = Assert.Single(_proposals.Where(p => p.PlayerId == _star.Id));

        Assert.Equal(TransferStatus.Pending, proposal.Status);
        Assert.Equal(_managerClub.Id, proposal.SellingClubId);
        Assert.Equal(_rival.Id, proposal.BuyingClubId);
    }

    [Fact]
    public async Task TheMessageIsKeyedToTheProposalItIsAbout()
    {
        // One row per proposal, so a sweep that runs twice over a window the manager never
        // answered does not fill his box with the same offer written over and over.
        GivenAWorldWhereTheRivalIsShopping();

        await TheSweep();

        var proposal = Assert.Single(_proposals.Where(p => p.PlayerId == _star.Id));

        Assert.Equal($"offer:{proposal.Id}", TheMessageAbout(TheStar).Reference);
    }

    [Fact]
    public async Task TheMessageCarriesTheBookValueAndNotOnlyTheOffer()
    {
        // A manager weighing a sale compares two numbers, and the one that flatters the deal is
        // the one being offered. The book value travels with it so the comparison is possible
        // without leaving the message.
        GivenAWorldWhereTheRivalIsShopping();
        var bookValue = PlayerValuation.MarketValue(_star, TheStateOf(_star));

        await TheSweep();

        Assert.Contains($"L$ {bookValue.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}"
            .Replace(",00", string.Empty), TheMessageAbout(TheStar).Body);
    }

    [Fact]
    public async Task TheMessageSaysThereIsNoClockRunningAgainstIt()
    {
        // Nothing here expires this offer behind the manager's back: a club that has not answered
        // has not refused. A message that said a deadline was running would have him holding off
        // for a clock that was never going to ring.
        GivenAWorldWhereTheRivalIsShopping();

        await TheSweep();

        var body = TheMessageAbout(TheStar).Body;

        Assert.DoesNotContain("prazo do mercado continua correndo", body);
        Assert.Contains("não há prazo correndo contra ela", body);
    }

    [Fact]
    public async Task TheMessageNamesTheThreeThingsAManagerReadsItFor()
    {
        // His club, the man, and the club that wants him — so all three names become links. A
        // name the message does not mention is a name the manager cannot click his way to.
        GivenAWorldWhereTheRivalIsShopping();

        await TheSweep();

        var mentions = InboxMentions.Parse(TheMessageAbout(TheStar).Mentions).ToList();

        Assert.Equal(3, mentions.Count);
        Assert.Contains(mentions, mention => mention.EntityId == _star.Id);
        Assert.Contains(mentions, mention => mention.EntityId == _rival.Id);
        Assert.Contains(mentions, mention => mention.EntityId == _managerClub.Id);
    }

    [Fact]
    public async Task AnOfferOfSomebodyElsesManIsNotSomethingIAmToldAbout()
    {
        // The other side of the same market. The rival's business is the rival's business, and a
        // box that reported it would be a box reporting thirty five clubs to a man who runs one.
        GivenAFullSquadFor(_rival);
        var somebodyElses = APlayer(TheStar, 21, strong: true);
        GivenAContractFor(somebodyElses, _rival, 3);

        await TheSweep();

        Assert.Empty(_written);
        Assert.Empty(_proposals.Where(proposal => proposal.SellingClubId == _managerClub.Id));
    }

    [Fact]
    public async Task APlayerNobodyOwnsIsNotAnOfferAboutMySquad()
    {
        // A free agent is somebody to sign rather than somebody to buy, so nothing about it is
        // news for the club that does not hold his contract.
        GivenAWorldWhereTheRivalIsShopping();
        var freeAgent = APlayer("Ninguem", 22, strong: false);
        _states.Add(PlayerSeasonState.CreateFreeAgent(freeAgent.Id, _season.Id, 100));

        await TheSweep();

        Assert.Empty(_proposals.Where(proposal => proposal.PlayerId == freeAgent.Id));
    }

    [Fact]
    public async Task AClubThatIsNotMyOwnIsNeverToldAnything()
    {
        // The rule lives in the inbox rather than in the caller, so a message aimed at a club
        // nobody is running is dropped there — the same way a prize for a club with no manager
        // is dropped.
        GivenAWorldWhereTheRivalIsShopping();
        _teams.Setup(repo => repo.GetAsync(_managerClub.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await TheSweep();

        Assert.Empty(_written);
        Assert.Single(_proposals.Where(proposal => proposal.PlayerId == _star.Id));
    }

    // --- Helpers ------------------------------------------------------------------

    private async Task TheSweep()
    {
        // The service is built first and the capture set up after it, because Moq answers a
        // call with the last setup registered for it and a callback registered first is
        // dropped without a word.
        var service = new TransferService(
            _transfers.Object, _teams.Object, _players.Object, _seasons.Object,
            _competitions.Object, _rounds.Object, _matches.Object, _finance.Object,
            InboxTestFactory.Create(_teams, _messages), new ManagedClubs(), _unitOfWork.Object,
            NullLogger<TransferService>.Instance);

        _messages.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _written.Add(message))
            .Returns(Task.CompletedTask);

        await service.CalculateNpcTransfersAsync(_season.Id);
    }

    private InboxMessage TheMessageAbout(string playerName) =>
        Assert.Single(_written.Where(message => message.Subject.Contains(playerName)));

    /// <summary>
    /// The world the feature actually lives in: two clubs above the minimum squad size and one
    /// man worth signing. A club below the minimum is sent after free agents and never looks at
    /// anybody's contract, and a squad of one man is a squad the rival may not want.
    /// </summary>
    private void GivenAWorldWhereTheRivalIsShopping()
    {
        GivenAFullSquadFor(_managerClub);
        GivenAFullSquadFor(_rival);

        _star = APlayer(TheStar, 21, strong: true);
        GivenAContractFor(_star, _managerClub, 3);
    }

    /// <summary>
    /// A full enough squad, made of men the rival would rather not sign. Their weakness is the
    /// point: the market ranks what it wants by stars and age, and a squad of twenty-four good
    /// men would put the rival's offers in an order this test has no reason to know.
    /// </summary>
    private void GivenAFullSquadFor(Team club)
    {
        for (var number = 1; number <= 24; number++)
        {
            var filler = APlayer($"Reserva {club.Name} {number}", 28, strong: false);
            GivenAContractFor(filler, club, 3);
        }
    }

    private void GivenAContractFor(Player player, Team club, int seasons)
    {
        _contracts.Add(TeamMembership.Create(
            player.Id, club.Id, _season.StartDate, seasons, 1, wage: 120_000m));
        _states.Add(PlayerSeasonState.Create(player.Id, _season.Id, club.Id, 100));
    }

    private Player APlayer(string name, int age, bool strong)
    {
        var player = Player.Create(
            name, age, Position.ATT,
            speed: strong ? 78 : 30, accuracy: strong ? 80 : 30, dribbling: strong ? 74 : 30,
            heading: strong ? 70 : 30, strength: strong ? 68 : 30,
            goalkeeperPower: 0, reflexes: 0, stamina: 70, potential: strong ? 92 : 40);

        _everybody.Add(player);

        return player;
    }

    private PlayerSeasonState TheStateOf(Player player) =>
        _states.First(state => state.PlayerId == player.Id);
}
