using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Seasons;
using Moq;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The sponsor system: offer generation, contract signing, per-match payment, and the
/// one active deal rule. These tests drive the SponsorOfferService over mocked repositories
/// to confirm the domain rules hold without a database.
/// </summary>
public class SponsorOfferServiceTests
{
    private readonly Mock<ISponsorRepository> _sponsors = new(MockBehavior.Loose);
    private readonly Mock<ISponsorContractRepository> _contracts = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);
    private readonly List<Sponsor> _sponsorCatalog = [];
    private readonly List<SponsorContract> _contractsInDb = [];
    private readonly List<FinanceMovement> _book = [];

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Team _team;
    private readonly Season _season;

    public SponsorOfferServiceTests()
    {
        _team = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B");
        _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        _season.Start();

        SeedSponsors();
        SetupRepositories();
    }

    private void SeedSponsors()
    {
        for (var i = 0; i < SponsorRules.MaxCandidateOffers * 2; i++)
        {
            _sponsorCatalog.Add(Sponsor.Create(
                $"Sponsor {i}", "Test Industry", "#0066CC"));
        }

        _sponsors.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sponsorCatalog);
        _sponsors.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, _) =>
                Task.FromResult(_sponsorCatalog.FirstOrDefault(s => s.Id == id)));
    }

    private void SetupRepositories()
    {
        _teams.Setup(repo => repo.GetAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_team);
        _seasons.Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);

        _finance.Setup(repo => repo.GetLastAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _book.OrderByDescending(m => m.Sequence).FirstOrDefault());
        _finance.Setup(repo => repo.ExistsForMatchAsync(
                _teamId, It.IsAny<Guid>(), FinanceMovementKind.Sponsorship, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _contracts.Setup(repo => repo.GetActiveByTeamAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contractsInDb.FirstOrDefault(
                c => c.TeamId == _teamId && c.Status == SponsorContractStatus.Active && c.MatchesLeft > 0));
        _contracts.Setup(repo => repo.AddAsync(It.IsAny<SponsorContract>(), It.IsAny<CancellationToken>()))
            .Callback<SponsorContract, CancellationToken>((contract, _) => _contractsInDb.Add(contract))
            .Returns(Task.CompletedTask);
        _contracts.Setup(repo => repo.Update(It.IsAny<SponsorContract>()))
            .Callback<SponsorContract>(contract =>
            {
                var existing = _contractsInDb.FirstOrDefault(c => c.Id == contract.Id);
                if (existing is not null)
                {
                    _contractsInDb.Remove(existing);
                }
                _contractsInDb.Add(contract);
            });
        _finance.Setup(repo => repo.AddAsync(It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Callback<FinanceMovement, CancellationToken>((movement, _) => _book.Add(movement))
            .Returns(Task.CompletedTask);
    }

    private SponsorOfferService CreateService() => new(
        _sponsors.Object,
        _contracts.Object,
        _teams.Object,
        _finance.Object,
        _seasons.Object,
        _unitOfWork.Object,
        NullLogger<SponsorOfferService>.Instance);

    [Fact]
    public async Task GetBook_ForTeamWithoutContract_ReturnsCandidates()
    {
        var service = CreateService();
        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.NotNull(book);
        Assert.Equal(_teamId, book.TeamId);
        Assert.Null(book.Current);
        Assert.Empty(_contractsInDb);
        Assert.NotEmpty(book.Candidates);
        Assert.True(book.Candidates.Count <= SponsorRules.MaxCandidateOffers);
    }

    [Fact]
    public async Task Sign_ForTeamWithoutContract_CreatesContractAndReturnsBook()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        var book = await service.SignAsync(_teamId, _seasonId, sponsor.Id);

        Assert.NotNull(book.Current);
        Assert.Equal(sponsor.Name, book.Current!.SponsorName);
        Assert.Single(_contractsInDb);
        Assert.True(_contractsInDb[0].IsActive);
    }

    [Fact]
    public async Task Sign_ForTeamWithActiveContract_Throws()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);

        var sponsor2 = _sponsorCatalog[1];
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => service.SignAsync(_teamId, _seasonId, sponsor2.Id));

        Assert.Equal("TeamHasActiveSponsor", ex.Code);
    }

    [Fact]
    public async Task PayPerMatch_WithNoContract_ReturnsNull()
    {
        var service = CreateService();
        var result = await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, 1);

        Assert.Null(result);
        Assert.Empty(_book);
    }

    [Fact]
    public async Task PayPerMatch_WithActiveContract_PaysFeeAndAdvancesDeal()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);

        var matchId = Guid.NewGuid();
        var payment = await service.PayPerMatchAsync(_teamId, matchId, _seasonId, 5);

        Assert.NotNull(payment);
        Assert.Equal(sponsor.Name, payment!.SponsorName);
        Assert.False(payment.IsDealExpired);
        Assert.Single(_book);
        Assert.Equal(FinanceMovementKind.Sponsorship, _book[0].Kind);
        Assert.True(_book[0].Amount > 0);
    }

    [Fact]
    public async Task PayPerMatch_ForSameMatchTwice_WritesOnlyOnePayment()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);

        var matchId = Guid.NewGuid();
        await service.PayPerMatchAsync(_teamId, matchId, _seasonId, 1);

        _finance.Setup(repo => repo.ExistsForMatchAsync(
                _teamId, matchId, FinanceMovementKind.Sponsorship, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await service.PayPerMatchAsync(_teamId, matchId, _seasonId, 1);

        Assert.Single(_book);
    }

    [Fact]
    public async Task PayPerMatch_ExpiresDealOnLastMatch()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();
        var length = 3;

        await service.SignAsync(_teamId, _seasonId, sponsor.Id, contractMatches: length);

        for (var match = 0; match < length - 1; match++)
        {
            var payment = await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, match + 1);
            Assert.False(payment!.IsDealExpired);
        }

        var finalPayment = await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, length);
        Assert.True(finalPayment!.IsDealExpired);

        var sponsor2 = _sponsorCatalog[1];
        var book = await service.SignAsync(_teamId, _seasonId, sponsor2.Id);

        Assert.NotNull(book.Current);
        Assert.Equal(sponsor2.Name, book.Current!.SponsorName);
    }

    [Fact]
    public async Task PayPerMatch_AfterDealExpires_PaysNothing()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();
        var length = 3;

        await service.SignAsync(_teamId, _seasonId, sponsor.Id, contractMatches: length);

        for (var match = 0; match < length; match++)
        {
            await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, match + 1);
        }

        var payment = await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, length + 1);
        Assert.Null(payment);
    }

    [Fact]
    public async Task PayPerMatch_WritesFinanceMovementWithSponsorReference()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);
        var contract = _contractsInDb.First();

        var matchId = Guid.NewGuid();
        await service.PayPerMatchAsync(_teamId, matchId, _seasonId, 3);

        var movement = Assert.Single(_book);
        Assert.Equal(FinanceMovementKind.Sponsorship, movement.Kind);
        Assert.Equal($"sponsor:{contract.Id}", movement.Reference);
        Assert.Equal(matchId, movement.MatchId);
    }

    [Fact]
    public async Task GetBook_ForTeamWithExpiringContract_ShowsCandidates()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();
        var length = 3;

        await service.SignAsync(_teamId, _seasonId, sponsor.Id, contractMatches: length);

        for (var match = 0; match < length - 1; match++)
        {
            await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, match + 1);
        }

        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.NotNull(book.Current);
        Assert.NotEmpty(book.Candidates);
    }

    [Fact]
    public async Task GetBook_ForTeamWithPlentyLeft_HidesCandidates()
    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id, contractMatches: 10);

        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.NotNull(book.Current);
        Assert.Empty(book.Candidates);
    }

    [Fact]
    public async Task GetBook_ForUnknownTeam_ThrowsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.GetBookAsync(Guid.NewGuid(), _seasonId));
    }

    [Fact]
    public async Task GetBook_ForUnknownSeason_ThrowsNotFound()
    {
        var service = CreateService();
        _seasons.Setup(repo => repo.GetAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Season?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.GetBookAsync(_teamId, Guid.NewGuid()));
    }

    [Fact]
    public void SponsorCreate_RequiresName()
    {
        Assert.Throws<ArgumentException>(() => Sponsor.Create("", "Industry", "#000"));
    }

    [Fact]
    public void SponsorCreate_RequiresIndustry()
    {
        Assert.Throws<ArgumentException>(() => Sponsor.Create("Name", "   ", "#000"));
    }

    [Fact]
    public void SponsorContract_RecordMatchPlayed_AdvancesAndExposesMatchesLeft()
    {
        var sponsor = _sponsorCatalog.First();
        var contract = SponsorContract.Sign(sponsor.Id, _teamId, _seasonId, 50_000m, 5);

        Assert.Equal(5, contract.MatchesLeft);
        Assert.True(contract.IsActive);

        contract.RecordMatchPlayed();
        Assert.Equal(4, contract.MatchesLeft);
        Assert.True(contract.IsActive);

        contract.RecordMatchPlayed();
        contract.RecordMatchPlayed();
        contract.RecordMatchPlayed();
        var expired = contract.RecordMatchPlayed();

        Assert.True(expired);
        Assert.Equal(0, contract.MatchesLeft);
        Assert.False(contract.IsActive);
        Assert.Equal(SponsorContractStatus.Expired, contract.Status);
    }

    [Fact]
    public void SponsorContract_Sign_RejectsNegativeFee()
    {
        var sponsor = _sponsorCatalog.First();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SponsorContract.Sign(sponsor.Id, _teamId, _seasonId, -1m, 5));
    }

    [Fact]
    public void SponsorContract_Sign_RejectsZeroLength()
    {
        var sponsor = _sponsorCatalog.First();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SponsorContract.Sign(sponsor.Id, _teamId, _seasonId, 10_000m, 0));
    }

    [Fact]
    public void Team_SignSponsorContract_RejectsSecondActiveDeal()
    {
        var sponsor = _sponsorCatalog.First();
        var contract = SponsorContract.Sign(sponsor.Id, _teamId, _seasonId, 50_000m, 5);

        _team.SignSponsorContract(contract);

        var sponsor2 = _sponsorCatalog[1];
        var contract2 = SponsorContract.Sign(sponsor2.Id, _teamId, _seasonId, 60_000m, 3);

        Assert.Throws<InvalidOperationException>(() => _team.SignSponsorContract(contract2));
    }

    [Fact]
    public void SponsorContract_Terminate_ExpiresEarly()
    {
        var sponsor = _sponsorCatalog.First();
        var contract = SponsorContract.Sign(sponsor.Id, _teamId, _seasonId, 50_000m, 5);

        contract.Terminate();

        Assert.Equal(SponsorContractStatus.Terminated, contract.Status);
        Assert.False(contract.IsActive);
    }

    [Fact]
    public void SponsorContract_CannotRecordMatchPlayedWhenTerminated()
    {
        var sponsor = _sponsorCatalog.First();
        var contract = SponsorContract.Sign(sponsor.Id, _teamId, _seasonId, 50_000m, 5);

        contract.Terminate();
        var result = contract.RecordMatchPlayed();

        Assert.False(result);
        Assert.Equal(5, contract.MatchesLeft);
    }
}
