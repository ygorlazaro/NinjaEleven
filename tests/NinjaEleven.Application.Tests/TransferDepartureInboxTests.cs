using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A rival signing one of a manager's men is told to the manager, and only when the rival is in
/// his own division.
/// </summary>
/// <remarks>
/// <para>
/// The message exists because the manager did not sell and is owed nothing: the bid was
/// somebody else's and it was never addressed to him. What he is owed is the news that the
/// forward he has been planning a season around is now a rival — and the only version of that
/// news worth opening is the version where the rival is on his own calendar, because a club two
/// divisions down taking a player is arithmetic the box need not carry.
/// </para>
///
/// <para>
/// So the boundary these hold is the division, and it is held from both sides: a rival inside
/// the division is told, and a rival outside it is not. A test that only asserted the first
/// would also pass if the rule had been "tell everybody", which is a box nobody reads.
/// </para>
///
/// <para>
/// The seller is not told about his own signing either. The manager who sold already has the
/// transaction, the cheque and the decision; a second message about it is the same news in a
/// different category, and a free agent signing a contract is nobody's departure at all.
/// </para>
/// </remarks>
public class TransferDepartureInboxTests
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
    private readonly Mock<IInboxMessageRepository> _inbox = new(MockBehavior.Loose);

    private readonly List<InboxMessage> _written = [];
    private readonly List<PlayerSeasonState> _states = [];

    private InboxService _box = null!;

    private readonly Team _seller = Team.Create("Esporte Clube Riachuelo", "Riachuelo", "#0a5", "#fff");
    private readonly Team _rival = Team.Create("Porto Marítimo", "Porto", "#06c", "#fff");

    private readonly Season _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    private readonly Player _star = Player.Create(
        TheStar, 26, Position.ATT, 14, 15, 13, 12, 11, 79, 76);

    public TransferDepartureInboxTests()
    {
        // The box is delivered only to a club somebody is running, which is asked of the team
        // repository: a message addressed to a club with no manager behind it is a message the
        // world has no reason to keep.
        _seller.MarkAsManagerClub();

        _seasons.Setup(repo => repo.GetAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);

        _teams.Setup(repo => repo.GetAsync(_seller.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_seller);
        _teams.Setup(repo => repo.GetAsync(_rival.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_rival);

        // The club being managed is the one with a person behind it, which is asked of the
        // world rather than of the request — the sweep walks a season on nobody's behalf.
        _teams.Setup(repo => repo.GetManagerClubAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_seller);

        // A seller of twenty-three men — twenty-two besides the one being sold — and a buyer with
        // room for him, so the deal completes and the question the message answers is the only
        // one left in it.
        _teams.Setup(repo => repo.GetLiveContractsAsync(_seller.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ContractsOf(_seller.Id, _star.Id));
        _teams.Setup(repo => repo.GetLiveContractsAsync(_rival.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ContractsOf(_rival.Id));

        _teams.Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, CancellationToken>((ids, _) => Task.FromResult<IReadOnlyList<Team>>(
                ids.Where(id => id == _seller.Id || id == _rival.Id)
                    .Select(id => id == _seller.Id ? _seller : _rival)
                    .ToList()));

        _players.Setup(repo => repo.GetAsync(_star.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_star);

        // The state the man arrives into: a season he has not played, so the message quotes the
        // fee and not a book value worked out of a season nobody is going to see.
        _players.Setup(repo => repo.GetSeasonStateForUpdateAsync(
                _star.Id, _season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var state = PlayerSeasonState.Create(_star.Id, _season.Id, _rival.Id, 100);
                _states.Add(state);
                return state;
            });
        _players.Setup(repo => repo.AddSeasonStateAsync(It.IsAny<PlayerSeasonState>(), It.IsAny<CancellationToken>()))
            .Callback((PlayerSeasonState state, CancellationToken _) => _states.Add(state))
            .Returns(Task.CompletedTask);

        _teams.Setup(repo => repo.AddMembershipAsync(It.IsAny<TeamMembership>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // The window that found nothing to do does not walk the calendar on its way out, and a
        // loose mock answers null where a list is read.
        _matches.Setup(repo => repo.GetMatchDaysAsync(_season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchDay>());

        // The box is built first, so the capture below is the setup that survives: a mock takes
        // the last answer it was given for a call, and the factory sets up the same AddAsync
        // this test has to watch.
        _box = InboxTestFactory.Create(_teams, _inbox);

        _inbox.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _inbox.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _written.Add(message))
            .Returns(Task.CompletedTask);
    }

    /// <summary>Twenty-three men, one of them the star when his id is named.</summary>
    private static IReadOnlyList<TeamMembership> ContractsOf(Guid teamId, Guid? starId = null) =>
        Enumerable.Range(0, 23)
            .Select(index => TeamMembership.Create(
                index == 0 && starId is { } held ? held : Guid.NewGuid(),
                teamId,
                new DateOnly(2026, 1, 1),
                shirtNumber: 2 + index))
            .ToList();

    private TransferService Service(params Guid[] managedClubs) => new(
        _transfers.Object,
        _teams.Object,
        _players.Object,
        _seasons.Object,
        _competitions.Object,
        _rounds.Object,
        _matches.Object,
        _finance.Object,
        _box,
        new ManagedClubs(managedClubs),
        _unitOfWork.Object,
        NullLogger<TransferService>.Instance);

    /// <summary>
    /// Puts a division in the pyramid, adding to whatever is already there: the season has four
    /// of them, so the two editions a test builds are both real, and which club is in which is
    /// the whole question the message turns on.
    /// </summary>
    private readonly Dictionary<int, List<Guid>> _tiers = [];

    private void TheSeasonPutsTheseClubsInTier(int tier, params Guid[] clubIds)
    {
        if (!_tiers.TryGetValue(tier, out var enrolled))
        {
            _tiers[tier] = enrolled = [];

            _competitions.Setup(repo => repo.ListSeasonViewsAsync(_season.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _tiers
                    .Select(entry => new CompetitionSeasonView
                    {
                        Id = EditionIdOf(entry.Key),
                        CompetitionId = EditionIdOf(entry.Key),
                        SeasonId = _season.Id,
                        Tier = entry.Key,
                        CompetitionName = CompetitionRules.DivisionName(entry.Key),
                        Type = CompetitionType.League
                    })
                    .ToList());
        }

        enrolled.AddRange(clubIds);

        var editionId = EditionIdOf(tier);
        _competitions.Setup(repo => repo.ListParticipantsAsync(editionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => enrolled
                .Select(clubId => CompetitionParticipant.Create(editionId, clubId))
                .ToList());
    }

    /// <summary>
    /// The id of a division's edition, worked from the tier so that a test naming a tier twice
    /// names the same edition rather than enrolling clubs in two divisions that never existed.
    /// </summary>
    private static Guid EditionIdOf(int tier) =>
        Guid.TryParse($"00000000-0000-0000-0000-{tier:D12}", out var id) ? id : Guid.NewGuid();

    /// <summary>An agreed deal of the star from the seller to the rival, waiting for its window.</summary>
    private Transfer AnAgreedSale(decimal fee)
    {
        var deal = Transfer.Propose(
            _star.Id, _seller.Id, _rival.Id, _season.Id,
            arrivalSeasonNumber: _season.Number,
            arrivalSeasonId: null,
            fee: fee,
            new DateOnly(2026, 10, 1),
            arrivalRoundNumber: 1);

        deal.Accept(new DateOnly(2026, 10, 1));

        _transfers.Setup(repo => repo.ListAcceptedAsync(_season.Number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { deal });
        _transfers.Setup(repo => repo.TryClaimForCompletionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return deal;
    }

    /// <summary>
    /// The rival in the same division: the manager is told, and the message names the man, the
    /// club that took him and the division they share.
    /// </summary>
    [Fact]
    public async Task ARivalInTheSameDivisionIsToldToTheManager()
    {
        TheSeasonPutsTheseClubsInTier(2, _seller.Id, _rival.Id);
        AnAgreedSale(fee: 4_500_000m);

        var completed = await Service(_seller.Id).CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Equal(1, completed);

        var message = Assert.Single(_written);
        Assert.Equal(_seller.Id, message.RecipientTeamId);
        Assert.Equal(InboxCategory.TransferOffer, message.Category);
        Assert.Contains(TheStar, message.Body, StringComparison.Ordinal);
        Assert.Contains(_rival.Name, message.Body, StringComparison.Ordinal);
        Assert.Contains("2ª Divisão", message.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same deal, a rival in another division: no message. This is the side of the rule
    /// that keeps the box worth opening — a box that told a manager about all ninety-six other
    /// clubs is a box he learns to swipe away, and then the one that matters goes with them.
    /// </summary>
    [Fact]
    public async Task ARivalInAnotherDivisionIsNotWorthAMessage()
    {
        TheSeasonPutsTheseClubsInTier(2, _seller.Id);
        TheSeasonPutsTheseClubsInTier(3, _rival.Id);
        AnAgreedSale(fee: 4_500_000m);

        await Service(_seller.Id).CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Empty(_written);
    }

    /// <summary>
    /// A world of nobody is told nothing. The message is owed to a person, so a sweep walking a
    /// season with no manager behind it does not write to anybody's box — which is also what
    /// makes it safe to leave the reporting in the window rather than in a scheduler.
    /// </summary>
    [Fact]
    public async Task AWorldWithoutAManagerIsToldNothing()
    {
        TheSeasonPutsTheseClubsInTier(2, _seller.Id, _rival.Id);
        AnAgreedSale(fee: 4_500_000m);

        await Service().CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Empty(_written);
    }

    /// <summary>
    /// A free agent signing is nobody's departure. There is no club he left, so there is nobody
    /// whose squad changed shape and nobody who is owed the news.
    /// </summary>
    [Fact]
    public async Task ASigningWithNoSellerIsNobodyDeparture()
    {
        TheSeasonPutsTheseClubsInTier(2, _seller.Id, _rival.Id);

        var deal = Transfer.Propose(
            _star.Id, sellingClubId: null, _rival.Id, _season.Id,
            arrivalSeasonNumber: _season.Number,
            arrivalSeasonId: null,
            fee: 0m,
            new DateOnly(2026, 10, 1),
            arrivalRoundNumber: 1);

        deal.Accept(new DateOnly(2026, 10, 1));

        _transfers.Setup(repo => repo.ListAcceptedAsync(_season.Number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transfer> { deal });
        _transfers.Setup(repo => repo.TryClaimForCompletionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Service(_seller.Id).CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Empty(_written);
    }

    /// <summary>
    /// The message is keyed on the deal, so a window that is closed twice — once by the last
    /// match of the day and once by a process that was down over the weekend — tells one
    /// departure once.
    /// </summary>
    [Fact]
    public async Task TheMessageIsKeyedOnTheDeal()
    {
        TheSeasonPutsTheseClubsInTier(2, _seller.Id, _rival.Id);
        var deal = AnAgreedSale(fee: 4_500_000m);

        await Service(_seller.Id).CompletePendingTransfersAsync(_season.Id, arrivalRound: 2);

        Assert.Equal($"departure:{deal.Id}", _written[0].Reference);
    }
}
