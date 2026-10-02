using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Inbox;
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
    private Mock<ICompetitionRepository> _competitions;
    private SponsorClubFactsService _facts;
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

        // The club is a first-division one with a table line, a form and a crowd behind it:
        // the states a price is read from, rather than a club that has not kicked off yet.
        var world = SponsorFactsTestFactory.Create(_seasonId, _teamId);
        _competitions = world.Competitions;
        _facts = world.Facts;

        SeedSponsors();
        SetupRepositories();
    }

    private void SeedSponsors()
    {
        for (var i = 0; i < SponsorRules.CandidateOffers * 2; i++)
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
        // The sponsor's own book, answered by the list of contracts in it rather than by
        // nothing: which clubs each company already has is one of the four reasons it will
        // refuse this one.
        _contracts.Setup(repo => repo.ListActiveBySponsorIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contractsInDb
                .Where(contract => contract.IsActive)
                .GroupBy(contract => contract.SponsorId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<SponsorContract>)group.ToList()));
        _finance.Setup(repo => repo.AddAsync(It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Callback<FinanceMovement, CancellationToken>((movement, _) => _book.Add(movement))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Starts keeping the messages the service under test writes.
    ///
    /// It has to run *after* the service is built: the factory sets its own <c>AddAsync</c> on
    /// the mock, and Moq answers a call with the last setup registered for it — so a callback
    /// set up first is silently dropped and the test would read an empty list and conclude
    /// that nothing was written.
    /// </summary>
    private static List<InboxMessage> CollectTheMessages(Mock<IInboxMessageRepository> messages)
    {
        var written = new List<InboxMessage>();

        messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => written.Add(message))
            .Returns(Task.CompletedTask);

        return written;
    }

    private SponsorOfferService CreateService() => CreateService(null);

    /// <summary>
    /// A service over a world built to order, for the rules that are about the world rather
    /// than about the ledger: which division the club is in, who else is on its shirt, and
    /// who else the company has already signed.
    /// </summary>
    private SponsorOfferService CreateServiceOver(int tier, params Guid[] alsoInDivision)
    {
        var world = SponsorFactsTestFactory.Create(_seasonId, _teamId, tier: tier, alsoInDivision: alsoInDivision);

        _competitions = world.Competitions;
        _facts = world.Facts;

        return CreateService();
    }

    private SponsorOfferService CreateService(Mock<IInboxMessageRepository>? messages) => new(
        _sponsors.Object,
        _contracts.Object,
        _teams.Object,
        _finance.Object,
        _seasons.Object,
        _competitions.Object,
        _facts,
        InboxTestFactory.Create(_teams, messages ?? new Mock<IInboxMessageRepository>(MockBehavior.Loose)),
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
        Assert.True(book.Candidates.Count <= SponsorRules.CandidateOffers);
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

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);

        // The length is the sponsor's to decide, so it is read back off the deal rather than
        // handed to it: a test that chose the length would be testing a deal that cannot happen.
        var length = _contractsInDb[0].MatchesLeft;

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

    /// <summary>
    /// A deal that runs out is a shirt with nobody's name on it, and nothing else in the game
    /// says so: the last instalment is in the ledger and the contract simply stops coming
    /// back. The manager is told on the match that ends it, and not one match before.
    /// </summary>
    [Fact]
    public async Task PayPerMatch_TellsTheManagerWhenTheDealRunsOut()
    {
        _team.MarkAsManagerClub();

        var messages = new Mock<IInboxMessageRepository>(MockBehavior.Loose);
        var service = CreateService(messages);
        var written = CollectTheMessages(messages);

        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);
        var length = _contractsInDb[0].MatchesLeft;

        // Signing is one of the two moments the box carries about a shirt deal: the money only
        // arrives a match at a time, so a deal that was never announced would be a deal the
        // manager finds out about from a statement with a name on his shirt he cannot place.
        var signing = Assert.Single(written);
        Assert.Equal(InboxCategory.Club, signing.Category);
        Assert.Contains(sponsor.Name, signing.Subject);
        Assert.Equal($"sponsor:{_contractsInDb.First().Id}:signed", signing.Reference);

        for (var match = 1; match < length; match++)
        {
            await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, match);
        }

        // The instalments are the statement's, not the box's: a message per matchday saying a
        // number the manager signed up for is the redundancy the weekly statement removed.
        Assert.Single(written);

        await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, length);

        var expiry = written.Single(message => message.Category == InboxCategory.Club
            && message.Reference.EndsWith(":expired", StringComparison.Ordinal));
        Assert.Contains(sponsor.Name, expiry.Subject);
        Assert.Equal($"sponsor:{_contractsInDb.First().Id}:expired", expiry.Reference);
    }

    /// <summary>
    /// A club nobody is running has nobody to deliver the news to, so the deal still expires
    /// in the book and the box still has nothing in it.
    /// </summary>
    [Fact]
    public async Task PayPerMatch_SaysNothingWhenNobodyIsRunningTheClub()
    {
        var messages = new Mock<IInboxMessageRepository>(MockBehavior.Loose);
        var service = CreateService(messages);
        var written = CollectTheMessages(messages);

        await service.SignAsync(_teamId, _seasonId, _sponsorCatalog.First().Id);
        await service.PayPerMatchAsync(_teamId, Guid.NewGuid(), _seasonId, 1);

        Assert.Empty(written);
    }

    [Fact]
    public async Task PayPerMatch_AfterDealExpires_PaysNothing()    {
        var service = CreateService();
        var sponsor = _sponsorCatalog.First();

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);
        var length = _contractsInDb[0].MatchesLeft;

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

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);
        var length = _contractsInDb[0].MatchesLeft;

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

        await service.SignAsync(_teamId, _seasonId, sponsor.Id);

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

    /// <summary>
    /// The price on the list is the price in the contract.
    ///
    /// <para>
    /// A manager chooses a company and a company quotes a number; if signing could arrive at
    /// a different one, the number on the screen would be a number nobody had agreed to. The
    /// fee the list showed and the fee the book carries are the same calculation, so this is
    /// a test of the seam rather than of arithmetic.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Sign_PaysThePriceTheListQuoted()
    {
        var service = CreateService();
        var book = await service.GetBookAsync(_teamId, _seasonId);
        var quoted = book.Candidates[0];

        await service.SignAsync(_teamId, _seasonId, quoted.SponsorId);

        Assert.Equal(quoted.PerMatchFee, _contractsInDb[0].PerMatchFee);
        Assert.Equal(quoted.ContractMatches, _contractsInDb[0].ContractMatches);
    }

    /// <summary>
    /// The shortlist is the same every time it is read, all week. A list that redrew itself on
    /// every visit would be three different boards of three different prices for the same
    /// shirt, and a decision taken on one of them would not be a decision at all.
    /// </summary>
    [Fact]
    public async Task GetBook_SaysTheSameThingTwice()
    {
        var service = CreateService();

        var first = await service.GetBookAsync(_teamId, _seasonId);
        var second = await service.GetBookAsync(_teamId, _seasonId);

        Assert.Equal(
            first.Candidates.Select(offer => (offer.SponsorId, offer.PerMatchFee, offer.ContractMatches)),
            second.Candidates.Select(offer => (offer.SponsorId, offer.PerMatchFee, offer.ContractMatches)));
    }

    /// <summary>
    /// A brand does not put two shirts in the same championship, so a company already on a
    /// club of this division is not offered this one.
    /// </summary>
    [Fact]
    public async Task ACompanyAlreadyOnAClubOfThisDivisionIsNotOffered()
    {
        var rival = Guid.NewGuid();
        var service = CreateServiceOver(tier: 1, alsoInDivision: rival);

        var taken = _sponsorCatalog[0];

        _contractsInDb.Add(SponsorContract.Sign(taken.Id, rival, _seasonId, 10_000m, 5));

        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.DoesNotContain(book.Candidates, offer => offer.SponsorId == taken.Id);
        Assert.NotEmpty(book.Candidates);
    }

    /// <summary>
    /// A company whose slate is full is a company that would be hanging its name on a shirt it
    /// does not need, so it is not asked.
    /// </summary>
    [Fact]
    public async Task ACompanyWithAFullSlateIsNotOffered()
    {
        var service = CreateService();

        var full = _sponsorCatalog[0];

        for (var club = 0; club < full.MaxClubs; club++)
        {
            _contractsInDb.Add(SponsorContract.Sign(
                full.Id, Guid.NewGuid(), _seasonId, 10_000m, 5));
        }

        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.DoesNotContain(book.Candidates, offer => offer.SponsorId == full.Id);
    }

    /// <summary>
    /// A company that will not approach below the second division is not offered a
    /// fourth-division club, however well that club is doing. This is the rule that keeps the
    /// pyramid worth climbing: a shirt that follows the money up the divisions is a reason to
    /// be at the top, and one that follows it everywhere is not.
    /// </summary>
    [Fact]
    public async Task ACompanyThatDoesNotWorkBelowItsTierIsNotOffered()
    {
        var service = CreateServiceOver(tier: 4);

        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.All(
            book.Candidates,
            offer => Assert.True(
                _sponsorCatalog.Single(s => s.Id == offer.SponsorId).MaxTier >= 4,
                $"{offer.Name} does not work below the fourth division"));
    }

    /// <summary>
    /// A national company is only shown to a club worth its name, so a fourth-division club
    /// with nothing going for it is offered the local shops and not the national brands.
    /// </summary>
    [Fact]
    public async Task AClubNobodyWantsIsOfferedTheSmallCompaniesOnly()
    {
        _sponsorCatalog.Add(Sponsor.Create("Grande Marca", "Bancário", "#003366", SponsorSize.National));

        _sponsors.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sponsorCatalog);

        // A fourth-division club at the bottom of its table, with a crowd of nobody's: the
        // club a national brand passes over and a local shop signs.
        var world = SponsorFactsTestFactory.Create(_seasonId, _teamId, tier: 4, position: 16);

        _competitions = world.Competitions;
        _facts = world.Facts;

        var service = CreateService();
        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.DoesNotContain(
            book.Candidates,
            offer => _sponsorCatalog.Single(s => s.Id == offer.SponsorId).Weight == 3);
    }

    /// <summary>
    /// A club in a division of the season cannot be sold a shirt at all, so the book comes
    /// back empty rather than priced at something invented.
    /// </summary>
    [Fact]
    public async Task AClubInNoDivisionIsOfferedNothing()
    {
        _competitions
            .Setup(repo => repo.GetDivisionSeasonForTeamAsync(
                _teamId, _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompetitionSeasonView?)null);

        var service = CreateService();
        var book = await service.GetBookAsync(_teamId, _seasonId);

        Assert.Empty(book.Candidates);
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
