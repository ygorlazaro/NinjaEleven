using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using Xunit;
using Moq;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The three rules the box lives by: only a club somebody is running is written to, every
/// message is written once, and the words are the ones the engine wrote.
///
/// The prose itself is not asserted word for word. It is a bank that is supposed to be
/// reworded, and a test that pinned it would break the day somebody improved a sentence. What
/// is held here is the shape of the news: that the numbers in a money message are the numbers
/// in the book, that a result says who it was against and where it is played next, and that
/// the two lines that state a balance rather than moving one are not news at all.
/// </summary>
public class InboxServiceTests
{
    private readonly Mock<IInboxMessageRepository> _messages = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    private readonly List<InboxMessage> _written = new();
    private readonly HashSet<string> _delivered = new(StringComparer.Ordinal);

    private readonly Guid _clubId = Guid.NewGuid();
    private readonly Team _club;

    public InboxServiceTests()
    {
        _club = Team.Create("Ninja Eleven", "NIN", "#101820", "#38d39f", 70);
        _club.MarkAsManagerClub();

        _teams.Setup(repo => repo.GetAsync(_clubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_club);

        _messages.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, string reference, CancellationToken _) => Task.FromResult(
                !_delivered.Add(reference)));

        _messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _written.Add(message))
            .Returns(Task.CompletedTask);
    }

    private InboxService Service() => new(
        _messages.Object,
        _teams.Object,
        _unitOfWork.Object,
        NullLogger<InboxService>.Instance);

    /// <summary>Asks for a club nobody is running, which is every other club in the world.</summary>
    private void GivenTheClubHasNoManager()
    {
        var npc = Team.Create("Bairro Unido", "BAI", "#222222", "#cccccc", 60);
        _teams.Setup(repo => repo.GetAsync(_clubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(npc);
    }

    private FinanceMovement AGateReceipt(string clubName) => FinanceMovement.Create(
        _clubId,
        Guid.NewGuid(),
        sequence: 1,
        matchDayNumber: 9,
        FinanceMovementKind.GateRevenue,
        $"Bilheteria contra {clubName}",
        amount: 24_000m,
        balanceBefore: 100_000m);

    [Fact]
    public async Task AMovementOfTheManagersClubArrivesInHisBox()
    {
        await Service().PostFinanceAsync(AGateReceipt("Nautico"));

        var message = Assert.Single(_written);
        Assert.Equal(InboxCategory.Finance, message.Category);
        Assert.Equal(_clubId, message.RecipientTeamId);
        Assert.Contains("24.000", message.Body);
        // The balance the line leaves behind is in the message, because "the club is richer"
        // is not a thing a manager can act on and "the club has this much" is.
        Assert.Contains("124.000", message.Body);
        Assert.Equal("/financeiro", message.LinkRoute);
    }

    /// <summary>
    /// The rule that keeps the box the manager's: a club with no manager has nobody to
    /// deliver to. Thirty five of them being told their own gate receipts would be thirty
    /// five clubs writing rows nobody ever asks for.
    /// </summary>
    [Fact]
    public async Task AMovementOfAClubNobodyIsRunningIsNotWritten()
    {
        GivenTheClubHasNoManager();

        await Service().PostFinanceAsync(AGateReceipt("Nautico"));

        Assert.Empty(_written);
    }

    /// <summary>
    /// A match settled twice — by the tick that blew the whistle and by one that arrives
    /// after it — leaves one line in the book and one message in the box. A box that repeats
    /// itself is a box a manager learns to distrust, which is worse than a quiet one.
    /// </summary>
    [Fact]
    public async Task TheSameMovementIsOnlyEverReportedOnce()
    {
        var service = Service();
        var line = AGateReceipt("Nautico");

        await service.PostFinanceAsync(line);
        await service.PostFinanceAsync(line);

        Assert.Single(_written);
    }

    /// <summary>
    /// The founding capital and the balance carried from last season state where the club
    /// stands rather than moving it, and a box that reported them would open the season with
    /// two messages about money the club already had.
    /// </summary>
    [Fact]
    public async Task TheTwoLinesThatStateABalanceAreNotNews()
    {
        var service = Service();

        await service.PostFinanceAsync(FinanceMovement.Seed(_clubId, Guid.NewGuid(), 1_000_000m));
        await service.PostFinanceAsync(FinanceMovement.OpenWithBalance(
            _clubId, Guid.NewGuid(), 2, FinanceMovementKind.CarryOver, "Saldo transportado", 900_000m));

        Assert.Empty(_written);
    }

    [Fact]
    public async Task AResultIsWrittenUpWithTheScoreTheOpponentAndTheNextCommitment()
    {
        var matchId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var nextOpponentId = Guid.NewGuid();
        var strikerId = Guid.NewGuid();

        await Service().PostMatchReportAsync(new MatchReportFacts
        {
            MatchId = matchId,
            RecipientTeamId = _clubId,
            ClubId = _clubId,
            ClubName = "Ninja Eleven",
            OpponentId = opponentId,
            OpponentName = "Bairro Unido",
            IsHome = true,
            CompetitionName = "1ª Divisão",
            PhaseName = "Rodada 9",
            MatchDayNumber = 9,
            ClubGoals = 2,
            OpponentGoals = 0,
            ClubFormation = "4-3-3",
            Lineup = [new InboxPersonDto { Id = strikerId, Name = "João da Silva", Kind = InboxMentionKind.Player }],
            GoalLines = ["GOL! João da Silva 0", "GOL! João da Silva 1"],
            GoalScorers = [new InboxPersonDto { Id = strikerId, Name = "João da Silva", Kind = InboxMentionKind.Player }],
            Booked = [new InboxPersonDto { Id = strikerId, Name = "João da Silva", Kind = InboxMentionKind.Player }],
            NextFixtureId = Guid.NewGuid(),
            NextOpponentId = nextOpponentId,
            NextOpponentName = "Nautico",
            NextMatchDayNumber = 10,
            NextCompetitionName = "1ª Divisão",
            NextStadiumName = "Estádio do Bairro",
            NextIsHome = false
        });

        var message = Assert.Single(_written);
        Assert.Equal(InboxCategory.MatchReport, message.Category);
        Assert.Contains("2 x 0", message.Body);
        Assert.Contains("Bairro Unido", message.Body);
        Assert.Contains("Rodada 9", message.Body);
        Assert.Contains("4-3-3", message.Body);
        Assert.Contains("João da Silva", message.Body);
        Assert.Contains("Nautico", message.Body);
        Assert.Contains("Estádio do Bairro", message.Body);
        Assert.Equal($"/match/{matchId}", message.LinkRoute);

        // The eleven, the man who scored and the man who was booked are all doors, or a
        // report is a document about people the manager cannot look up.
        var mentions = InboxMentions.Parse(message.Mentions);
        Assert.True(mentions.Any(mention => mention.Name == "João da Silva"));
        Assert.True(mentions.Any(mention => mention.EntityId == nextOpponentId));
    }

    /// <summary>
    /// A goalless match has no goal to quote, and "0 x 0" is not a story. Saying that nobody
    /// found the net is the truest thing there is to say about it.
    /// </summary>
    [Fact]
    public async Task AGoallessMatchSaysNobodyFoundTheNet()
    {
        var matchId = Guid.NewGuid();

        await Service().PostMatchReportAsync(new MatchReportFacts
        {
            MatchId = matchId,
            RecipientTeamId = _clubId,
            ClubId = _clubId,
            ClubName = "Ninja Eleven",
            OpponentId = Guid.NewGuid(),
            OpponentName = "Bairro Unido",
            CompetitionName = "Copa",
            PhaseName = "Oitavas de final",
            ClubGoals = 0,
            OpponentGoals = 0
        });

        var message = Assert.Single(_written);
        Assert.Contains("ninguém achou a rede", message.Body);
        Assert.Equal($"/match/{matchId}", message.LinkRoute);
    }

    /// <summary>
    /// The three places of an artilharia are three different sentences, and telling a manager
    /// his striker "é o artilheiro" when the striker finished third would be the one sentence
    /// in the box that is simply false.
    /// </summary>
    [Fact]
    public async Task AnArtilhariaIsAnnouncedWithThePlaceItActuallyTook()
    {
        var service = Service();

        await service.PostTitleAsync(new TitleFacts
        {
            RecipientTeamId = _clubId,
            ClubName = "Ninja Eleven",
            Kind = InboxTitleKind.Scorer,
            CompetitionName = "1ª Divisão",
            Position = 3,
            PrizeMoney = 900_000m,
            PlayerId = Guid.NewGuid(),
            PlayerName = "João da Silva",
            PlayerGoals = 14,
            Reference = "artilharia:edition:player"
        });

        var message = Assert.Single(_written);
        Assert.Contains("terceiro", message.Subject);
        Assert.DoesNotContain("é o artilheiro", message.Subject);
    }

    /// <summary>
    /// A message is read by the club that owns it and by nobody else. A manager who guessed
    /// his way to somebody else's line is told the line is not his, which is the only honest
    /// answer: there is no such thing as a global box.
    /// </summary>
    [Fact]
    public async Task AManagerCannotOpenAMessageAddressedToAnotherClub()
    {
        var message = InboxMessage.Create(
            Guid.NewGuid(),
            InboxCategory.Finance,
            "Alguém",
            "Tesouraria",
            "Conteúdo",
            "finance:xyz");
        _messages.Setup(repo => repo.GetAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var read = await Service().MarkReadAsync(_clubId, message.Id);

        Assert.Null(read);
    }

    /// <summary>
    /// A shirt deal ends by running out of matches, and a contract that quietly stops being
    /// paid is a shirt with nobody's name on it. The message is the only place the game says
    /// so, so it has to name the sponsor and say what stops.
    /// </summary>
    [Fact]
    public async Task ASponsorDealThatRunsOutIsAnnounced()
    {
        await Service().PostSponsorExpiryAsync(new SponsorExpiryFacts
        {
            RecipientTeamId = _clubId,
            ClubName = _club.Name,
            SponsorId = Guid.NewGuid(),
            SponsorName = "Loja do Bairro",
            ContractId = Guid.NewGuid(),
            PerMatchFee = 45_000m,
            ContractMatches = 10
        });

        var message = Assert.Single(_written);
        Assert.Equal(InboxCategory.Club, message.Category);
        Assert.Contains("Loja do Bairro", message.Subject);
        Assert.Contains("10", message.Body);
        Assert.Contains("L$ 45.000", message.Body);
        Assert.Equal("/estadio", message.LinkRoute);
    }

    /// <summary>
    /// The same deal expires once. A match settled twice — by the tick that blew the whistle and
    /// by one that arrives after it — must not leave two shirts in the box.
    /// </summary>
    [Fact]
    public async Task ASponsorDealThatEndsIsAnnouncedOnlyOnce()
    {
        var facts = new SponsorExpiryFacts
        {
            RecipientTeamId = _clubId,
            ClubName = _club.Name,
            SponsorId = Guid.NewGuid(),
            SponsorName = "Loja do Bairro",
            ContractId = Guid.NewGuid(),
            PerMatchFee = 45_000m,
            ContractMatches = 10
        };

        await Service().PostSponsorExpiryAsync(facts);
        await Service().PostSponsorExpiryAsync(facts);

        Assert.Single(_written);
    }

    [Theory]
    [InlineData(InboxDecision.RefusedOnPrice, "preço")]
    [InlineData(InboxDecision.RefusedOnThePlayer, "valor do jogador")]
    [InlineData(InboxDecision.RefusedOnTheSquad, "elenco")]
    public async Task ARefusalSaysWhyItWasARefusal(InboxDecision outcome, string because)
    {
        await Service().PostTransferDecisionAsync(ABid(outcome));

        var message = Assert.Single(_written);
        Assert.Contains("recusou", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(because, message.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Three refusals and one acceptance are four sentences, because a manager who was outbid
    /// and a manager who was outplayed do not have the same next bid. Collapsing them would
    /// leave him thinking the wrong thing about his own offer.
    /// </summary>
    [Fact]
    public async Task TheThreeRefusalsAreNotTheSameSentence()
    {
        var bodies = new List<string>();

        foreach (var outcome in new[]
                 {
                     InboxDecision.RefusedOnPrice,
                     InboxDecision.RefusedOnThePlayer,
                     InboxDecision.RefusedOnTheSquad
                 })
        {
            _written.Clear();
            _delivered.Clear();
            await Service().PostTransferDecisionAsync(ABid(outcome));
            bodies.Add(Assert.Single(_written).Body);
        }

        Assert.Equal(3, bodies.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AnAcceptedBidIsAnsweredAsTheBuyersOwnProposal()
    {
        await Service().PostTransferDecisionAsync(ABid(InboxDecision.Accepted));

        var message = Assert.Single(_written);
        Assert.Equal(InboxCategory.TransferOffer, message.Category);
        Assert.Contains(_club.Name, message.Subject);
        Assert.Contains("aceitou", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L$ 8.000.000", message.Body);
    }

    /// <summary>
    /// The same deal read from the two sides is two different sentences, and a subject that
    /// only carried the player's name would not say whether he left the club or arrived at it.
    /// </summary>
    [Fact]
    public async Task ASaleIsAnsweredAsTheSellersOwnDecision()
    {
        await Service().PostTransferDecisionAsync(ABid(InboxDecision.Accepted, managerIsSeller: true));

        var message = Assert.Single(_written);
        Assert.Contains("compra", message.Subject);
        Assert.Contains("fechou", message.Body);
    }

    /// <summary>
    /// A deal can be agreed and then fail to happen, and the manager has to be told that it is
    /// the second and not the first — the money is back and the player is not coming.
    /// </summary>
    [Fact]
    public async Task ADealCalledOffAfterBeingAgreedIsItsOwnMessage()
    {
        await Service().PostTransferDecisionAsync(ABid(InboxDecision.CalledOff));
        await Service().PostTransferDecisionAsync(ABid(InboxDecision.Accepted));

        Assert.Equal(2, _written.Count);
        Assert.Contains("cancelado", _written[0].Subject);
        Assert.Contains("aceita", _written[1].Subject);
    }

    [Fact]
    public async Task AProposalNobodyAnsweredIsReportedAsTheMarketStopping()
    {
        await Service().PostTransferDecisionAsync(ABid(InboxDecision.Expired));

        var message = Assert.Single(_written);
        Assert.Contains("expirou", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/transfer", message.LinkRoute);
    }

    /// <summary>The three names a decision quotes are three doors on the screen.</summary>
    [Fact]
    public async Task ADecisionNamesTheClubThePlayerAndTheOtherClub()
    {
        var bid = ABid(InboxDecision.Accepted);

        await Service().PostTransferDecisionAsync(bid);

        var mentions = InboxMentions.Parse(Assert.Single(_written).Mentions);
        Assert.Equal(3, mentions.Count);
        Assert.Contains(mentions, mention => mention.EntityId == bid.PlayerId);
        Assert.Contains(mentions, mention => mention.EntityId == bid.OtherClubId);
    }

    private TransferDecisionFacts ABid(InboxDecision outcome, bool managerIsSeller = false) => new()
    {
        RecipientTeamId = _clubId,
        ClubName = _club.Name,
        PlayerId = Guid.NewGuid(),
        PlayerName = "Zé do Apito",
        OtherClubId = Guid.NewGuid(),
        OtherClubName = "Bairro Unido",
        Fee = 8_000_000m,
        Outcome = outcome,
        ManagerIsSeller = managerIsSeller,
        Reference = $"transfer:{Guid.NewGuid()}"
    };
}
