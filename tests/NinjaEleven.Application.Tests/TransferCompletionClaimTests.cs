using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A window completes its deals once, and once only.
///
/// <para>
/// A deal is completed by the matchday sweep when a window closes and by
/// <c>POST /transfer/complete</c> when a hand asks for it. Both read the same accepted list
/// untracked, and the membership is written before the status is — so without a claim the
/// second caller signed the player again and the club held one man twice with a single deal
/// row insisting he had arrived once.
/// </para>
///
/// <para>
/// The claim is what decides, and these hold both of its answers: a caller that took the deal
/// signs the man, and a caller that did not writes nothing at all. The second is the one worth
/// naming, because it is the whole fix — a refusal to sign is the correct behaviour of a
/// service asked twice, and a test that only checked the happy path would not have noticed the
/// claim being removed and put back.
/// </para>
/// </summary>
public class TransferCompletionClaimTests
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

    private readonly List<TeamMembership> _added = [];

    private readonly Team _buyer = Team.Create("Renascença", "REN", "#101820", "#38d39f", 70);
    private readonly Season _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
    private readonly Player _player;

    public TransferCompletionClaimTests()
    {
        _player = Player.Create(
            "Leonardo Vidal de Oliveira", 27, Position.GK, 12, 10, 9, 9, 14, 78, 74);

        _seasons.Setup(repo => repo.GetAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);

        _teams.Setup(repo => repo.GetAsync(_buyer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_buyer);
        _teams.Setup(repo => repo.GetManagerClubAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_buyer);

        // A club of twenty-three, so there is room to sign one more.
        _teams.Setup(repo => repo.GetLiveContractsAsync(_buyer.Id, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<TeamMembership>>(
                Enumerable.Range(0, 23)
                    .Select(_ => TeamMembership.Create(
                        Guid.NewGuid(), _buyer.Id, new DateOnly(2026, 1, 1), shirtNumber: 2 + _))
                    .ToList()));

        _players.Setup(repo => repo.GetAsync(_player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_player);

        // A deal called off gives the fee back, and the refund is written with a reference
        // built from the matchday the window fell in — so a calendar with no days in it is a
        // loose mock handing back null where a list is expected.
        _matches.Setup(repo => repo.GetMatchDaysAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay>().AsReadOnly());

        // A deal called off is reported to the manager whose club it involved, and the report
        // reads the clubs it names — a loose mock answers null where a list of teams is read.
        _teams.Setup(repo => repo.ListByIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());

        _teams.Setup(repo => repo.AddMembershipAsync(It.IsAny<TeamMembership>(), It.IsAny<CancellationToken>()))
            .Callback((TeamMembership membership, CancellationToken _) => _added.Add(membership))
            .Returns(Task.CompletedTask);
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
        new ManagedClubs(),
        _unitOfWork.Object,
        NullLogger<TransferService>.Instance);

    private Transfer AnAcceptedSigning()
    {
        var deal = Transfer.Propose(
            _player.Id, sellingClubId: null, _buyer.Id, _season.Id,
            // The window asks for the deals of the arrival season's *number*, and the fixture's
            // season is the first — so the deal has to name that number or the list comes back
            // empty and the test passes for the wrong reason.
            arrivalSeasonNumber: _season.Number, arrivalSeasonId: null,
            fee: 1000m, new DateOnly(2026, 10, 1), arrivalRoundNumber: 1);

        deal.Accept(new DateOnly(2026, 10, 1));

        _transfers.Setup(repo => repo.ListAcceptedAsync(
                _season.Number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { deal });

        return deal;
    }

    private void TheClaimIsLost() =>
        _transfers.Setup(repo => repo.TryClaimForCompletionAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

    private void TheClaimIsWon() =>
        _transfers.Setup(repo => repo.TryClaimForCompletionAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    /// <summary>The window signs the man once, and he arrives with a shirt.</summary>
    [Fact]
    public async Task AClaimedDealSignsThePlayerOnce()
    {
        AnAcceptedSigning();
        TheClaimIsWon();

        var completed = await Service().CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Equal(1, completed);

        var membership = Assert.Single(_added);
        Assert.Equal(_player.Id, membership.PlayerId);
        Assert.Equal(_buyer.Id, membership.TeamId);
        Assert.True(membership.HasShirtNumber);
    }

    /// <summary>
    /// The fix itself: the caller that lost the race writes nothing. Not a second membership,
    /// not a shirt dealt to nobody — a deal another caller already signed.
    /// </summary>
    [Fact]
    public async Task ADealSomebodyElseClaimedIsNotSignedAgain()
    {
        AnAcceptedSigning();
        TheClaimIsLost();

        var completed = await Service().CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Equal(0, completed);
        Assert.Empty(_added);
    }

    /// <summary>
    /// A club does not hold the same man twice, whoever asks. This is the invariant rather than
    /// the race: the claim settles which of two callers writes, and this settles what a
    /// membership is. A deal whose buyer already holds him died between being agreed and being
    /// completed, and a club of twenty-four with one man twice is not a squad.
    /// </summary>
    [Fact]
    public async Task AClubThatAlreadyHoldsTheManRefusesTheSigning()
    {
        AnAcceptedSigning();
        TheClaimIsWon();

        _teams.Setup(repo => repo.GetLiveContractsAsync(_buyer.Id, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<TeamMembership>>(
                Enumerable.Range(0, 23)
                    .Select(index => TeamMembership.Create(
                        index == 0 ? _player.Id : Guid.NewGuid(),
                        _buyer.Id, new DateOnly(2026, 1, 1), shirtNumber: 2 + index))
                    .ToList()));

        var completed = await Service().CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Equal(0, completed);
        Assert.Empty(_added);
    }

    /// <summary>
    /// A club that is full leaves the deal exactly as it found it. It is not claimed, because a
    /// claim is a promise that this caller is about to sign the player, and a deal marked
    /// completed whose player never arrived is a deal nobody signs again.
    /// </summary>
    [Fact]
    public async Task AFullClubLeavesTheDealWaitingAndDoesNotClaimIt()
    {
        AnAcceptedSigning();

        _teams.Setup(repo => repo.GetLiveContractsAsync(_buyer.Id, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<TeamMembership>>(
                Enumerable.Range(0, 40)
                    .Select(index => TeamMembership.Create(
                        Guid.NewGuid(), _buyer.Id, new DateOnly(2026, 1, 1), shirtNumber: 2 + index))
                    .ToList()));

        var completed = await Service().CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Equal(0, completed);
        Assert.Empty(_added);

        _transfers.Verify(
            repository => repository.TryClaimForCompletionAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
