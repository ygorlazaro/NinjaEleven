using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The books, and the moment they change.
///
/// A purchase is a decision and a decision shows what it cost on the day it is taken: the fee
/// leaves the buyer's book when the deal is agreed, and the window — eleven rounds later, or in
/// the next season — is what moves the player and not the money. A test that only watched the
/// window would pass against either rule, because at the window the money has been moved
/// exactly once in both of them.
/// </summary>
public class TransferServiceMoneyTests
{
    private readonly Mock<ITransferRepository> _transfers = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<ISeasonRepository> _seasons = new();
    private readonly Mock<ICompetitionRepository> _competitions = new();
    private readonly Mock<IRoundRepository> _rounds = new();
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<IFinanceRepository> _finance = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Team _seller = Team.Create("Esporte Clube Riachuelo", "Riachuelo", "#0a5", "#fff");
    private readonly Team _buyer = Team.Create("Porto Marítimo", "Porto", "#06c", "#fff");
    private readonly Season _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    private readonly List<FinanceMovement> _movements = new();

    public TransferServiceMoneyTests()
    {
        _seasons.Setup(repo => repo.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);
        _seasons.Setup(repo => repo.GetAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);
        _seasons.Setup(repo => repo.GetByNumberAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);

        _teams.Setup(repo => repo.GetAsync(_seller.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_seller);
        _teams.Setup(repo => repo.GetAsync(_buyer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_buyer);

        // A club with a full squad can afford the checks the service makes of it.
        _teams.Setup(repo => repo.GetLiveContractsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 24)
                .Select(_ => TeamMembership.Create(
                    Guid.NewGuid(), _buyer.Id, new DateOnly(2026, 1, 1),
                    FinanceRules.DefaultContractSeasons, 1))
                .ToList());

        // The window is read off the calendar: a season in which no round has been played is a
        // season in the first matchday, which is where a manager making an offer starts.
        _competitions.Setup(repo => repo.ListSeasonViewsAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompetitionSeasonView>());
        _matches.Setup(repo => repo.GetMatchDaysAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay>());

        _finance.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _finance.Setup(repo => repo.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinanceMovement?)null);
        _finance.Setup(repo => repo.AddAsync(It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Callback((FinanceMovement movement, CancellationToken _) => _movements.Add(movement))
            .Returns(Task.CompletedTask);
    }

    private TransferService CreateService() => new(
        _transfers.Object,
        _teams.Object,
        _players.Object,
        _seasons.Object,
        _competitions.Object,
        _rounds.Object,
        _matches.Object,
        _finance.Object,
        InboxTestFactory.Create(_teams),
        _unitOfWork.Object,
        NullLogger<TransferService>.Instance);

    private static Player CreatePlayer(Guid? teamId = null) => Player.Create(
        "Valter Cassini",
        23,
        Position.ATT,
        speed: 14,
        accuracy: 13,
        dribbling: 13,
        heading: 12,
        strength: 13,
        goalkeeperPower: 0,
        reflexes: 0);

    private Transfer PendingOffer(Player player, decimal fee)
    {
        _players.Setup(repo => repo.GetAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _players.Setup(repo => repo.GetSeasonStateForUpdateAsync(
                player.Id, _season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerSeasonState.Create(player.Id, _season.Id, _seller.Id, 100));

        var offer = Transfer.Propose(
            player.Id, _seller.Id, _buyer.Id, _season.Id,
            arrivalSeasonNumber: 1, arrivalSeasonId: _season.Id,
            fee, new DateOnly(2026, 3, 1), arrivalRoundNumber: 11);

        _transfers.Setup(repo => repo.GetAsync(offer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _transfers.Setup(repo => repo.ListAcceptedAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer>());

        return offer;
    }

    [Fact]
    public async Task AnAcceptedOfferIsPaidForOnTheDayItIsAgreed()
    {
        var player = CreatePlayer();
        var offer = PendingOffer(player, 900_000m);

        await CreateService().AnswerTransferAsync(offer.Id, _seller.Id, accept: true);

        var purchase = Assert.Single(_movements.Where(movement => movement.TeamId == _buyer.Id));
        var sale = Assert.Single(_movements.Where(movement => movement.TeamId == _seller.Id));

        Assert.Equal(FinanceMovementKind.TransferOut, purchase.Kind);
        Assert.Equal(-900_000m, purchase.Amount);
        Assert.Equal(FinanceMovementKind.TransferIn, sale.Kind);
        Assert.Equal(900_000m, sale.Amount);
    }

    [Fact]
    public async Task ARefusedOfferCostsNobodyAnything()
    {
        var player = CreatePlayer();
        var offer = PendingOffer(player, 900_000m);

        await CreateService().AnswerTransferAsync(offer.Id, _seller.Id, accept: false);

        Assert.Empty(_movements);
    }

    [Fact]
    public async Task AFreeAgentIsNotFree()
    {
        var player = CreatePlayer(teamId: null);
        _players.Setup(repo => repo.GetAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _players.Setup(repo => repo.GetSeasonStateForUpdateAsync(
                player.Id, _season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerSeasonState.CreateFreeAgent(player.Id, _season.Id, 100));
        _transfers.Setup(repo => repo.ExistsAcceptedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Transfer? signed = null;
        _transfers.Setup(repo => repo.AddAsync(It.IsAny<Transfer>(), It.IsAny<CancellationToken>()))
            .Callback((Transfer transfer, CancellationToken _) => signed = transfer)
            .Returns(Task.CompletedTask);

        await CreateService().ProposeTransferAsync(player.Id, _buyer.Id);

        var marketValue = PlayerValuation.MarketValue(player, PlayerSeasonState.CreateFreeAgent(player.Id, _season.Id, 100));
        var expected = decimal.Round(marketValue * 0.2m, 2, MidpointRounding.AwayFromZero);

        Assert.NotNull(signed);
        Assert.Null(signed!.SellingClubId);
        Assert.Equal(expected, signed.Fee);

        // The cost is the buyer's and nobody's else: a signing has no club on the other side of
        // it to be paid.
        var charge = Assert.Single(_movements);
        Assert.Equal(_buyer.Id, charge.TeamId);
        Assert.Equal(FinanceMovementKind.TransferOut, charge.Kind);
        Assert.Equal(-expected, charge.Amount);
    }
}
