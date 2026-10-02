using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using Xunit;
using Moq;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
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
        new ManagedClubs(),
        _unitOfWork.Object,
        NullLogger<InboxService>.Instance);

    /// <summary>Asks for a club nobody is running, which is every other club in the world.</summary>
    private void GivenTheClubHasNoManager()
    {
        var npc = Team.Create("Bairro Unido", "BAI", "#222222", "#cccccc", 60);
        _teams.Setup(repo => repo.GetAsync(_clubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(npc);
    }

    private FinanceMovement APrize(string description, decimal amount = 24_000m) =>
        FinanceMovement.Create(
            _clubId,
            Guid.NewGuid(),
            sequence: 1,
            matchDayNumber: null,
            FinanceMovementKind.PrizeMoney,
            description,
            amount,
            balanceBefore: 100_000m,
            reference: $"championship:{Guid.NewGuid()}:3");

    /// <summary>
    /// A prize is the one line of a club's book the box interrupts the manager for, and the
    /// message carries the purse and the balance it left behind: "the club is richer" is not
    /// a thing a manager can act on and "the club has this much" is.
    /// </summary>
    [Fact]
    public async Task APrizeArrivesInHisBoxWithTheMoneyAndTheBalanceItLeft()
    {
        await Service().PostPrizeAsync(APrize("3ª da 1ª Divisão"));

        var message = Assert.Single(_written);
        Assert.Equal(InboxCategory.Finance, message.Category);
        Assert.Equal(_clubId, message.RecipientTeamId);
        Assert.Contains("24.000", message.Subject);
        Assert.Contains("24.000", message.Body);
        Assert.Contains("124.000", message.Body);
        Assert.Equal(InboxLink.Financeiro, message.LinkRoute);
    }

    /// <summary>
    /// The rule that keeps the box the manager's: a club with no manager has nobody to
    /// deliver to. Thirty five of them being told their own prizes would be thirty five
    /// clubs writing rows nobody ever asks for.
    /// </summary>
    [Fact]
    public async Task APrizeForAClubNobodyIsRunningIsNotWritten()
    {
        GivenTheClubHasNoManager();

        await Service().PostPrizeAsync(APrize("3ª da 1ª Divisão"));

        Assert.Empty(_written);
    }

    /// <summary>
    /// A prize asked about twice — a season closed again, a cup tie settled twice — is one
    /// purse. A box that repeats itself is a box a manager learns to distrust, which is worse
    /// than a quiet one.
    /// </summary>
    [Fact]
    public async Task TheSamePrizeIsOnlyEverWrittenOnce()
    {
        var service = Service();
        var line = APrize("3ª da 1ª Divisão");

        await service.PostPrizeAsync(line);
        await service.PostPrizeAsync(line);

        Assert.Single(_written);
    }

    /// <summary>
    /// The week, in one message, in the four buckets a manager thinks in.
    /// </summary>
    [Fact]
    public async Task AWeekIsOneMessageThatAddsUp()
    {
        await Service().PostStatementAsync(new StatementFacts
        {
            RecipientTeamId = _clubId,
            ClubName = "Portuguesa",
            FromMatchDay = 8,
            ToMatchDay = 14,
            GateRevenue = 40_000m,
            Signings = 0m,
            Sales = 0m,
            OtherIncome = 0m,
            Wages = 30_000m,
            Training = 2_000m,
            OtherExpenses = 0m,
            OpeningBalance = 100_000m,
            ClosingBalance = 108_000m,
            HomeMatches = 1,
            Reference = "statement:8"
        });

        var message = Assert.Single(_written);
        Assert.Equal(InboxCategory.Finance, message.Category);
        // 40.000 in, 32.000 out.
        Assert.Contains("40.000", message.Body);
        Assert.Contains("32.000", message.Body);
        Assert.Contains("108.000", message.Body);
    }

    /// <summary>
    /// A bucket that did not move is not printed. Four empty lines make the two real ones
    /// harder to find on a screen a manager reads on a phone.
    /// </summary>
    [Fact]
    public async Task AStatementDoesNotPrintTheBucketsThatStayedAtZero()
    {
        await Service().PostStatementAsync(new StatementFacts
        {
            RecipientTeamId = _clubId,
            ClubName = "Portuguesa",
            FromMatchDay = 8,
            ToMatchDay = 14,
            GateRevenue = 40_000m,
            Signings = 0m,
            Sales = 0m,
            OtherIncome = 0m,
            Wages = 30_000m,
            Training = 0m,
            OtherExpenses = 0m,
            OpeningBalance = 100_000m,
            ClosingBalance = 110_000m,
            HomeMatches = 1,
            Reference = "statement:8"
        });

        var message = Assert.Single(_written);
        Assert.DoesNotContain("Treinos", message.Body);
        Assert.DoesNotContain("Vendas", message.Body);
        Assert.Contains("Bilheteria", message.Body);
        Assert.Contains("Salários", message.Body);
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
    /// The filter is the server's and not the client's.
    ///
    /// A page filtered in the browser would be twenty lines picked out of a page of everything,
    /// and "how much of my mail is this" would be answered with whatever the screen happened
    /// to be holding. So the same category narrows the lines, the count and the paging in one
    /// go, and the answer says which category it applied so the buttons cannot claim a filter
    /// the server did not make.
    /// </summary>
    [Fact]
    public async Task AFilteredPageIsTheServersOwnPageOfThatCategory()
    {
        var title = AStoredMessage("title:1", InboxCategory.Title);
        var older = AStoredMessage("title:2", InboxCategory.Title);
        _messages.Setup(repo => repo.CountAsync(_clubId, InboxCategory.Title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _messages.Setup(repo => repo.ListAsync(
                _clubId, 0, 2, InboxCategory.Title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InboxMessage> { title, older });

        var box = await Service().GetBoxAsync(_clubId, page: 1, pageSize: 2, InboxCategory.Title);

        Assert.Equal(InboxCategory.Title, box.Category);
        Assert.Equal(3, box.TotalItems);
        Assert.Equal(2, box.TotalPages);
        Assert.Equal(2, box.Messages.Count);
        _messages.Verify(repo => repo.ListAsync(
            _clubId, 0, 2, InboxCategory.Title, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The filter column is labelled out of the whole box, and it is labelled while a filter is
    /// on.
    ///
    /// A tally that shrank to the slice being read would renumber itself every time the manager
    /// pressed a button, and a column of numbers that cannot be compared to each other cannot
    /// answer the question it is there for: which of these is worth pressing.
    /// </summary>
    [Fact]
    public async Task TheFilterColumnIsTalliedOverTheWholeBoxAndNotOverTheSlice()
    {
        _messages.Setup(repo => repo.CountAsync(_clubId, InboxCategory.Title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _messages.Setup(repo => repo.ListAsync(
                _clubId, 0, 20, InboxCategory.Title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InboxMessage> { AStoredMessage("title:1", InboxCategory.Title) });
        _messages.Setup(repo => repo.TallyCategoriesAsync(_clubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InboxCategoryTally>
            {
                new(InboxCategory.Finance, 12),
                new(InboxCategory.Title, 1)
            });

        var box = await Service().GetBoxAsync(_clubId, page: 1, pageSize: 20, InboxCategory.Title);

        Assert.Equal(
            [(InboxCategory.Finance, 12), (InboxCategory.Title, 1)],
            box.Categories.Select(row => (row.Category, row.Count)).ToArray());
    }

    /// <summary>
    /// No filter is the whole box, and the answer says so.
    ///
    /// The category travels back on the page precisely so that the screen's buttons are lit by
    /// what the server did rather than by what it remembers asking for.
    /// </summary>
    [Fact]
    public async Task ABoxWithNoFilterIsTheWholeBoxAndSaysSo()
    {
        _messages.Setup(repo => repo.CountAsync(_clubId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _messages.Setup(repo => repo.ListAsync(_clubId, 0, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InboxMessage> { AStoredMessage("finance:1", InboxCategory.Finance) });

        var box = await Service().GetBoxAsync(_clubId, page: 1, pageSize: 20);

        Assert.Null(box.Category);
        Assert.Equal(1, box.TotalItems);
        Assert.Equal(1, box.TotalPages);
    }

    /// <summary>
    /// A page past the end of what the manager asked for answers with the last page of it.
    ///
    /// Filtering is what makes this reachable: page four of the whole box is the fourth page of
    /// plenty, and page four of a category with one page in it is a request for something that
    /// does not exist. It answers with the page that does, rather than with nothing.
    /// </summary>
    [Fact]
    public async Task APagePastTheEndOfAFilteredBoxIsTheLastPageOfIt()
    {
        _messages.Setup(repo => repo.CountAsync(_clubId, InboxCategory.CupRound, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _messages.Setup(repo => repo.ListAsync(
                _clubId, 0, 20, InboxCategory.CupRound, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InboxMessage> { AStoredMessage("cup:1", InboxCategory.CupRound) });

        var box = await Service().GetBoxAsync(_clubId, page: 9, pageSize: 20, InboxCategory.CupRound);

        Assert.Equal(1, box.Page);
        Assert.Equal(1, box.TotalPages);
    }

    /// <summary>A message already in the box, for the reads that are about pages of one.</summary>
    private InboxMessage AStoredMessage(string reference, InboxCategory category) =>
        InboxMessage.Create(
            _clubId,
            category,
            $"Assunto {reference}",
            "Ninja Eleven",
            "Conteúdo.",
            reference);

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
        // The door is the sponsors screen and not the ground: the button says "Ver os
        // patrocinadores" and a contract that ran out is renewed by signing another one, which
        // is the one thing the stadium screen cannot do.
        Assert.Equal(InboxLink.Sponsors, message.LinkRoute);
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
        Assert.Equal(InboxLink.Transfer, message.LinkRoute);
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
    /// <summary>
    /// A result is not only a score: a manager opening the report wants to know what kind of
    /// result it was. A turnaround that leaves the club where it was and a turnaround that
    /// lifts it two places are the same football and different news, and this holds that the
    /// subject line says which one it was — because the subject line is the only line of a
    /// message a manager is certain to read.
    /// </summary>
    [Fact]
    public async Task ASportsPageLeadsWithTheShapeAndNotWithTheScore()
    {
        await Service().PostMatchReportAsync(Facts(shape: MatchShape.Comeback, clubGoals: 2, opponentGoals: 1));

        var message = Assert.Single(_written);

        Assert.Contains("Virada", message.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 x 1", message.Subject);
    }

    [Fact]
    public async Task AWhitewashIsCalledAGoleadaRatherThanAVictoryByFive()
    {
        await Service().PostMatchReportAsync(Facts(shape: MatchShape.Whitewash, clubGoals: 4, opponentGoals: 0));

        var message = Assert.Single(_written);

        Assert.Contains("Goleada", message.Subject, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A club two goals up that ends level has thrown a result away, and "noite de gols" would
    /// have told the manager his team had a good evening. This is the sentence that stops that
    /// from happening.
    /// </summary>
    [Fact]
    public async Task ASquanderedLeadIsNotReportedAsAGoodEvening()
    {
        await Service().PostMatchReportAsync(Facts(shape: MatchShape.Squandered, clubGoals: 2, opponentGoals: 2));

        var message = Assert.Single(_written);

        Assert.Contains("tinha dois ou mais gols na mão", message.Subject);
        Assert.DoesNotContain("Noite de gols", message.Subject, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The table move is the half of the news the score cannot carry, and it belongs on the
    /// subject as well as in the body: a manager who only reads subjects still learns that the
    /// result was worth two places.
    /// </summary>
    [Fact]
    public async Task TheSubjectSaysWhereTheResultLeftTheClub()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 1,
            opponentGoals: 0,
            competition: League(positionBefore: 7, positionAfter: 5, points: 45)));

        var message = Assert.Single(_written);

        Assert.Contains("sobe", message.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tabela", message.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AClubThatDidNotMoveIsNotToldItMoved()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            competition: League(positionBefore: 5, positionAfter: 5, points: 40)));

        var message = Assert.Single(_written);

        Assert.Contains("continua em 5º", message.Body);
        Assert.DoesNotContain("sobe", message.Subject, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// "Três pontos da classificação" has to mean three points off the *last* club that goes
    /// up, because that is the club a manager is actually racing.
    /// </summary>
    [Fact]
    public async Task TheGapToThePromotionPlacesIsSaidInPoints()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            competition: League(
                positionBefore: 9,
                positionAfter: 9,
                points: 39,
                toPromotion: 3,
                toRelegation: 8)));

        var message = Assert.Single(_written);

        Assert.Contains("3 pontos", message.Body);
        Assert.Contains("classificação", message.Body);
        Assert.Contains("rebaixamento", message.Body);
    }

    [Fact]
    public async Task TheRoundsLeftAreSaidAndTheLastOneIsCalledTheLast()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            competition: League(roundsRemaining: 0, positionBefore: 3, positionAfter: 3, points: 60)));

        var message = Assert.Single(_written);

        Assert.Contains("última rodada", message.Body);

        _written.Clear();

        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            competition: League(roundsRemaining: 1, positionBefore: 3, positionAfter: 3, points: 60)));

        Assert.Contains("Falta uma rodada", Assert.Single(_written).Body);
    }

    /// <summary>
    /// A cup tie has no table and no position, and a report that printed a position about a
    /// quarter-final would be inventing a league the competition does not have.
    /// </summary>
    [Fact]
    public async Task ACupTieIsNotGivenAPositionItDoesNotHave()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 1,
            opponentGoals: 0,
            competition: new CompetitionContext
            {
                Kind = CompetitionType.Cup,
                HasTable = false,
                RoundNumber = 2,
                RoundsRemaining = 1,
                Advanced = true
            }));

        var message = Assert.Single(_written);

        Assert.DoesNotContain("lugar na tabela", message.Body);
        Assert.Contains("vaga", message.Body);
    }

    /// <summary>
    /// A tie that is not decided yet is not a qualification. A manager planning the last five
    /// rounds from a 1 x 0 away leg would be planning for a match that is still to be played.
    /// </summary>
    [Fact]
    public async Task ATieThatIsNotFinishedIsNotCalledAQualification()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            competition: new CompetitionContext
            {
                Kind = CompetitionType.Cup,
                HasTable = false,
                RoundNumber = 1,
                RoundsRemaining = 1,
                Advanced = null
            }));

        var message = Assert.Single(_written);

        Assert.DoesNotContain("vaga é conquistada", message.Body);
    }

    [Fact]
    public async Task AnEliminationSaysTheCampaignIsOver()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 0,
            opponentGoals: 1,
            competition: new CompetitionContext
            {
                Kind = CompetitionType.Cup,
                HasTable = false,
                RoundNumber = 2,
                RoundsRemaining = 1,
                Advanced = false
            }));

        var message = Assert.Single(_written);

        Assert.Contains("fora", message.Body);
        Assert.Contains("fim da campanha", message.Body);
    }

    /// <summary>
    /// A suspension is the absence a manager can do nothing about, and a replacement does not
    /// fix it — so it is the one that has to be on the page.
    /// </summary>
    [Fact]
    public async Task TheMenMissingNextWeekAreNamedWithTheirReason()
    {
        var suspended = Guid.NewGuid();
        var knocked = Guid.NewGuid();

        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            absence: new MatchAbsence
            {
                Players =
                [
                    new PlayerAbsence { PlayerId = suspended, Cause = AbsenceCause.RedCard, Matches = 1 },
                    new PlayerAbsence { PlayerId = knocked, Cause = AbsenceCause.Injury, Matches = 3 }
                ]
            },
            squad: [
                new InboxPersonDto { Id = suspended, Name = "Zé da Silva", Kind = InboxMentionKind.Player },
                new InboxPersonDto { Id = knocked, Name = "Ana Souza", Kind = InboxMentionKind.Player }
            ]));

        var body = Assert.Single(_written).Body;

        Assert.Contains("Zé da Silva", body);
        Assert.Contains("expulsão", body);
        Assert.Contains("Ana Souza", body);
        Assert.Contains("3 jogos", body);
        Assert.Contains("lesão", body);
    }

    [Fact]
    public async Task ThreeYellowsAreSuspensionAndAreNotCalledAKnock()
    {
        var player = Guid.NewGuid();

        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            absence: new MatchAbsence
            {
                Players = [new PlayerAbsence { PlayerId = player, Cause = AbsenceCause.AccumulatedYellows, Matches = 1 }]
            },
            squad: [new InboxPersonDto { Id = player, Name = "Bola", Kind = InboxMentionKind.Player }]));

        var body = Assert.Single(_written).Body;

        Assert.Contains("três amarelos", body);
        Assert.DoesNotContain("lesão", body);
    }

    [Fact]
    public async Task AMatchWithNoAbsencesDoesNotInventASuspensionList()
    {
        await Service().PostMatchReportAsync(Facts(shape: MatchShape.Ordinary));

        var body = Assert.Single(_written).Body;

        Assert.DoesNotContain("Fora da próxima", body);
    }

    /// <summary>
    /// The goals are quoted in the words the match used, not reworded at report time: a match
    /// that said "GOL! João 0" is a match that said it, and a report that reworded the same
    /// afternoon would be the one place in the game where a fixture could be told two ways.
    /// </summary>
    [Fact]
    public async Task TheGoalsAreQuotedInTheWordsTheMatchItselfUsed()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 1,
            opponentGoals: 0,
            goalLines: ["GOL! João da Silva 34'"],
            goals: [new MatchGoal(Guid.NewGuid(), Guid.NewGuid(), "João da Silva", 34, GoalKind.Rebound)]));

        var body = Assert.Single(_written).Body;

        Assert.Contains("GOL! João da Silva 34'", body);
    }

    /// <summary>
    /// The narration is the report's first source, but a match replayed from a row whose
    /// events carry no description has none to quote, and a report that then said "nenhum" for
    /// a match with a goal in it would be wrong in a worse way than one that words it itself.
    /// </summary>
    [Fact]
    public async Task AMatchWithNoNarrationIsStillAccountedFor()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 1,
            opponentGoals: 0,
            goals: [new MatchGoal(Guid.NewGuid(), Guid.NewGuid(), "João da Silva", 34, GoalKind.Penalty)]));

        var body = Assert.Single(_written).Body;

        Assert.Contains("pênalti", body);
        Assert.Contains("34'", body);
    }

    [Fact]
    public async Task APenaltyIsSaidToHaveComeFromElevenMetres()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 1,
            opponentGoals: 0,
            goals: [new MatchGoal(Guid.NewGuid(), Guid.NewGuid(), "João da Silva", 34, GoalKind.Penalty)]));

        Assert.Contains("de pênalti", Assert.Single(_written).Body);
    }

    [Fact]
    public async Task AnOwnGoalIsNeverCreditedToTheManWhoGotItInHisOwnNet()
    {
        await Service().PostMatchReportAsync(Facts(
            shape: MatchShape.Ordinary,
            clubGoals: 0,
            opponentGoals: 1,
            goals: [new MatchGoal(Guid.NewGuid(), Guid.NewGuid(), "Zé da Silva", 12, GoalKind.OwnGoal)]));

        var body = Assert.Single(_written).Body;

        Assert.Contains("errada", body);
        Assert.Contains("Zé da Silva", body);
    }

    [Fact]
    public async Task TheElevenIsListedWithTheShapeItMadeOfThePitch()
    {
        await Service().PostMatchReportAsync(Facts(shape: MatchShape.Ordinary, formation: "4-3-3"));

        var body = Assert.Single(_written).Body;

        Assert.Contains("4-3-3", body);
        Assert.Contains("Escalação", body);
    }

    private static CompetitionContext League(
        int positionBefore,
        int positionAfter,
        int points,
        int? toPromotion = null,
        int? toRelegation = null,
        int? roundsRemaining = 5) => new()
        {
            Kind = CompetitionType.League,
            HasTable = true,
            PositionBefore = positionBefore,
            PositionAfter = positionAfter,
            PositionChange = positionBefore - positionAfter,
            Points = points,
            PointsToPromotion = toPromotion,
            PointsToRelegation = toRelegation,
            RoundNumber = 9,
            RoundsRemaining = roundsRemaining
        };

    private MatchReportFacts Facts(
        MatchShape shape,
        int clubGoals = 0,
        int opponentGoals = 0,
        string formation = "4-3-3",
        CompetitionContext? competition = null,
        MatchAbsence? absence = null,
        IReadOnlyList<MatchGoal>? goals = null,
        IReadOnlyList<string>? goalLines = null,
        IReadOnlyList<InboxPersonDto>? squad = null) => new()
        {
            MatchId = Guid.NewGuid(),
            RecipientTeamId = _clubId,
            ClubId = _clubId,
            ClubName = "Ninja Eleven",
            OpponentId = Guid.NewGuid(),
            OpponentName = "Bairro Unido",
            IsHome = true,
            CompetitionName = "1ª Divisão",
            PhaseName = "Rodada 9",
            MatchDayNumber = 9,
            ClubGoals = clubGoals,
            OpponentGoals = opponentGoals,
            ClubFormation = formation,
            Shape = shape,
            Goals = goals ?? [],
            GoalLines = goalLines ?? [],
            Lineup = squad ?? [new InboxPersonDto { Id = Guid.NewGuid(), Name = "Titular", Kind = InboxMentionKind.Player }],
            Absence = absence,
            Competition = competition
        };
}
