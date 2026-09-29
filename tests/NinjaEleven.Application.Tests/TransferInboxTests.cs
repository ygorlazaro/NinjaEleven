using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
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
/// The market answers a manager's bid, and the answer has to reach him.
///
/// Every sweep here settles the whole world — a deadline that passes expires every proposal
/// in the league, a window that opens completes every deal agreed months earlier — and a
/// proposal between two computer-controlled clubs is settled for the ledger alone. What is
/// tested here is the boundary: the same sweep, on the same rows, tells the manager's club and
/// stays silent about everybody else's.
/// </summary>
public class TransferInboxTests
{
    private readonly Mock<ITransferRepository> _transfers = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly Mock<IInboxMessageRepository> _inbox = new(MockBehavior.Loose);

    private readonly List<InboxMessage> _written = new();

    private readonly Team _managerClub = Team.Create("Ninja Eleven", "NIN", "#101820", "#38d39f", 70);
    private readonly Team _otherClub = Team.Create("Bairro Unido", "BAI", "#0a5", "#fff");
    private readonly Team _thirdClub = Team.Create("Porto Marítimo", "Porto", "#06c", "#fff");
    private readonly Season _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
    private readonly Player _player;

    public TransferInboxTests()
    {
        _managerClub.MarkAsManagerClub();
        _player = Player.Create(
            "Zé do Apito", 28, Position.ATT, 12, 12, 12, 12, 12, 0, 0);

        _seasons.Setup(repo => repo.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_season);
        _seasons.Setup(repo => repo.GetAsync(_season.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_season);
        _seasons.Setup(repo => repo.GetByNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(_season);

        _teams.Setup(repo => repo.GetManagerClubAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_managerClub);
        _teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, _) => Task.FromResult(
                id == _managerClub.Id ? _managerClub
                : id == _otherClub.Id ? _otherClub
                : _thirdClub));
        _teams.Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, CancellationToken>((ids, _) => Task.FromResult<IReadOnlyList<Team>>(
                ids.Select(id => id == _managerClub.Id ? _managerClub
                    : id == _otherClub.Id ? _otherClub
                    : _thirdClub).ToList()));
        _teams.Setup(repo => repo.GetPlayersAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, CancellationToken>((ids, _) => Task.FromResult(
                ids.Where(id => id == _player.Id)
                    .ToDictionary(id => id, _ => _player)));

        _matches.Setup(repo => repo.GetMatchDaysAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _finance.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _finance.Setup(repo => repo.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinanceMovement?)null);

        _inbox.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private TransferService Service() => new(
        _transfers.Object,
        _teams.Object,
        _players.Object,
        _seasons.Object,
        _competitions.Object,
        _rounds.Object,
        _matches.Object,
        _finance.Object,
        InboxTestFactory.Create(_teams, _inbox),
        _unitOfWork.Object,
        NullLogger<TransferService>.Instance);

    /// <summary>
    /// Starts keeping what the service writes. It runs after the service is built because Moq
    /// answers a call with the last setup registered for it, and a callback registered first
    /// is dropped without a word.
    /// </summary>
    private List<InboxMessage> Collect()
    {
        _inbox.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _written.Add(message))
            .Returns(Task.CompletedTask);

        return _written;
    }

    private Transfer ABid(Team buyer, Team seller) => Transfer.Propose(
        _player.Id,
        seller.Id,
        buyer.Id,
        _season.Id,
        arrivalSeasonNumber: 2,
        arrivalSeasonId: null,
        fee: 8_000_000m,
        proposedAt: new DateOnly(2026, 3, 1),
        arrivalRoundNumber: 2,
        proposalRoundNumber: 5,
        answerByRound: 7);

    [Fact]
    public async Task ABidThatRunsOutOfTimeIsToldToTheManagerWhoMadeIt()
    {
        var service = Service();
        var written = Collect();
        var bid = ABid(_managerClub, _otherClub);
        _transfers.Setup(repo => repo.ListExpiredAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { bid });

        await service.ExpirePendingProposalsAsync(7);

        var message = Assert.Single(written.Where(m => m.Category == InboxCategory.TransferOffer));
        Assert.Equal(_managerClub.Id, message.RecipientTeamId);
        Assert.Contains("expira", message.Subject);
        Assert.Contains("Bairro Unido", message.Body);
        Assert.Equal($"transfer:{bid.Id}:expired", message.Reference);
    }

    /// <summary>
    /// A bid the manager's club is selling is his own player being offered away, and the
    /// sentence is the other one: the deadline is not his, the offer is.
    /// </summary>
    [Fact]
    public async Task AnOfferForTheManagersOwnPlayerThatLapsesIsToldAsTheOtherSentence()
    {
        var service = Service();
        var written = Collect();
        _transfers.Setup(repo => repo.ListExpiredAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { ABid(_otherClub, _managerClub) });

        await service.ExpirePendingProposalsAsync(7);

        var message = Assert.Single(written.Where(m => m.Category == InboxCategory.TransferOffer));
        Assert.Contains("caduca", message.Subject);
    }

    /// <summary>
    /// The same sweep over a proposal between two clubs nobody is playing writes nothing. It
    /// still expires the row — the market has to move — but there is no box to write into.
    /// </summary>
    [Fact]
    public async Task AProposalBetweenTwoOtherClubsIsSettledInSilence()
    {
        var service = Service();
        var written = Collect();
        var bid = ABid(_otherClub, _thirdClub);
        _transfers.Setup(repo => repo.ListExpiredAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { bid });

        await service.ExpirePendingProposalsAsync(7);

        Assert.Equal(TransferStatus.Expired, bid.Status);
        Assert.Empty(written);
    }

    /// <summary>
    /// A proposal that was agreed and then could not be kept is a second fact, not a second
    /// copy of the first: the manager agreed to it, the money moved, and then the world
    /// changed underneath it. The box has to hold both, and in that order.
    /// </summary>
    [Fact]
    public async Task ADealThatCannotBeKeptIsToldApartFromTheDealThatWasAgreed()
    {
        var service = Service();
        var written = Collect();
        var bid = ABid(_managerClub, _otherClub);
        bid.Accept(new DateOnly(2026, 3, 2));

        _seasons.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);
        _transfers.Setup(repo => repo.ListAcceptedAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { bid });

        // The club that agreed to sell no longer holds the man: the deal dies at the window.
        _teams.Setup(repo => repo.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<IReadOnlyList<TeamMembership>>(
                id == _otherClub.Id
                    ? []
                    : Enumerable.Range(0, 24)
                        .Select(_ => TeamMembership.Create(
                            Guid.NewGuid(), _managerClub.Id, new DateOnly(2026, 1, 1),
                            FinanceRules.DefaultContractSeasons, 1))
                        .ToList()));
        _players.Setup(repo => repo.GetAsync(_player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_player);

        await service.CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        var message = Assert.Single(written.Where(m => m.Category == InboxCategory.TransferOffer));
        Assert.Equal($"transfer:{bid.Id}:called-off", message.Reference);
        Assert.Contains("cancelado", message.Subject);
    }
}
