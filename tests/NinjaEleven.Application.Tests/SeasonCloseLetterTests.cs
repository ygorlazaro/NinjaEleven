using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A season that ends is told to everybody, in one letter.
///
/// <para>
/// The close is the one moment the whole country changes shape: four tables stop, sixteen clubs
/// move, and a manager in the 4ª Divisão watched two clubs leave his own table and two arrive.
/// The letter is written from the tables the close has already read and from
/// <c>DivisionMovement</c> — the same rule that rearranges the pyramid an instant later — so the
/// letter and the calendar cannot tell two different endings of the same season.
/// </para>
/// </summary>
public class SeasonCloseLetterTests
{
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IDivisionRepository> _divisions = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<ITrophyRepository> _trophies = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IMatchRepository> _matches = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ITransferRepository> _transferRows = new(MockBehavior.Loose);
    private readonly Mock<IInboxMessageRepository> _messages = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    /// <summary>Every message written into a manager's box by the close.</summary>
    private readonly List<InboxMessage> _sent = new();

    private Guid _seasonId;
    private static readonly Guid _nextSeasonId = Guid.NewGuid();

    /// <summary>Four divisions, and a club with a manager behind it in each of the first two.</summary>
    private readonly List<CompetitionSeasonView> _editions = new();
    private readonly Dictionary<Guid, Team> _clubs = new();

    public SeasonCloseLetterTests()
    {
        // The season is the one the whole test is about, and its own id is the id every setup
        // below and every reference the letter carries: a season whose row and whose name in
        // the fixtures were two different things would be a season that cannot be closed.
        _season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        _season.Start();
        _seasonId = _season.Id;

        for (var tier = 1; tier <= 4; tier++)
        {
            var edition = new CompetitionSeasonView
            {
                Id = Guid.NewGuid(),
                CompetitionId = Guid.NewGuid(),
                SeasonId = _seasonId,
                CompetitionName = CompetitionRules.DivisionName(tier),
                Type = CompetitionType.League,
                Tier = tier
            };

            _editions.Add(edition);

            var members = new List<CompetitionParticipant>();
            for (var seat = 0; seat < 4; seat++)
            {
                var club = Team.Create(
                    $"Clube {tier}-{seat}",
                    $"C{tier}{seat}",
                    "#0a5",
                    "#fff");

                // A manager behind two of the sixteen clubs, one in the 1ª and one in the 2ª: the
                // letter is written for the managers of the world, not for the two champions,
                // and a champion is a separate message with its own purse.
                if (tier == 1 && seat == 3 || tier == 2 && seat == 3)
                {
                    club.MarkAsManagerClub();
                }

                _clubs[club.Id] = club;
                members.Add(CompetitionParticipant.Create(edition.Id, club.Id));
            }

            _competitions.Setup(repo => repo.ListParticipantsAsync(edition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(members);
        }

        // One season instance for the whole test, because the close is handed back the season
        // it has just finished: a repository that answered a new season on every read would be
        // a world whose season is never the same season twice.
        _seasons.Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_season);
        _seasons.Setup(repo => repo.GetAsync(_nextSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season.Create(2, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)));

        _competitions.Setup(repo => repo.ListSeasonViewsAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_editions);

        // A division that has not been drawn has no rounds, and a table with no rounds is a
        // table of its sixteen clubs with no games in it — which is what a close reads.
        _rounds.Setup(repo => repo.ListByCompetitionSeasonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        // No trophy has been written yet, so the podium is: the guard is answered "nothing on
        // the shelf", which is what a first close of a season looks like.
        _trophies.Setup(repo => repo.ListBySeasonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _trophies.Setup(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<TrophyAward>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult(_clubs.GetValueOrDefault(id)));
        _teams.Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, CancellationToken>((ids, _) =>
                Task.FromResult<IReadOnlyList<Team>>(ids
                    .Where(_clubs.ContainsKey)
                    .Select(id => _clubs[id])
                    .ToList()));

        // A table of clubs with no players in them still has an order, and a close must not
        // need a season's worth of squad rows to read it.
        _teams.Setup(repo => repo.GetSquadsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, Guid, CancellationToken>((ids, _, _) =>
                Task.FromResult(ids.ToDictionary(
                    id => id,
                    _ => (IReadOnlyList<TeamMembership>)Array.Empty<TeamMembership>())));

        // Every prize is paid, which is what a test about the letter wants: it counts what is in
        // the box, and a book that has already been paid under the same reference would say so.
        _finance.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _finance.Setup(repo => repo.GetLastAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinanceMovement?)null);
        _finance.Setup(repo => repo.AddAsync(It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var box = InboxTestFactory.Create(
            _teams,
            _messages,
            new ManagedClubs(_clubs.Values.Where(club => club.IsManagerClub).Select(club => club.Id).ToArray()));

        // Registered after the box exists, for the reason the cup's test gives: the box sets up
        // its own writes when it is built, and a callback registered before that is replaced.
        _messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _sent.Add(message))
            .Returns(Task.CompletedTask);

        _box = box;
    }

    private readonly InboxService _box;

    /// <summary>The season being closed, and the same row the close is handed back.</summary>
    private readonly Season _season = null!;

    /// <summary>The letters the close wrote, which is a different question from every message.</summary>
    private IReadOnlyList<InboxMessage> Letters =>
        _sent.Where(message => message.Category == InboxCategory.SeasonSummary).ToList();

    private SeasonCloseService CreateService()
    {
        var standings = new StandingsService(
            _rounds.Object,
            _fixtures.Object,
            _matches.Object,
            _competitions.Object,
            _teams.Object);

        var finance = new FinanceService(
            _finance.Object,
            _teams.Object,
            _players.Object,
            _fixtures.Object,
            _rounds.Object,
            _matchDays.Object,
            _seasons.Object,
            _box,
            _unitOfWork.Object,
            NullLogger<FinanceService>.Instance);

        var transfers = new TransferService(
            _transferRows.Object,
            _teams.Object,
            _players.Object,
            _seasons.Object,
            _competitions.Object,
            _rounds.Object,
            _matches.Object,
            _finance.Object,
            _box,
            new ManagedClubs(_clubs.Values.Where(club => club.IsManagerClub).Select(club => club.Id).ToArray()),
            _unitOfWork.Object,
            NullLogger<TransferService>.Instance);

        return new SeasonCloseService(
            _seasons.Object,
            _competitions.Object,
            _divisions.Object,
            _rounds.Object,
            _matchDays.Object,
            _fixtures.Object,
            _trophies.Object,
            _teams.Object,
            standings,
            new SeasonCalendarService(
                _seasons.Object,
                _matchDays.Object,
                _rounds.Object,
                _fixtures.Object,
                new Mock<ICupTieRepository>().Object,
                _competitions.Object,
                _matches.Object,
                _teams.Object,
                _trophies.Object,
                standings,
                _unitOfWork.Object),
            finance,
            new Mock<IDataSeeder>().Object,
            new RosterService(
                _players.Object,
                _teams.Object,
                _transferRows.Object,
                _unitOfWork.Object,
                _box,
                NullLogger<RosterService>.Instance),
            transfers,
            _box,
            _unitOfWork.Object,
            NullLogger<SeasonCloseService>.Instance);
    }

    /// <summary>
    /// One letter to every manager, keyed on the season, and carrying all four tables.
    /// </summary>
    [Fact]
    public async Task AClosedSeasonIsTold_to_every_manager_in_one_letter()
    {
        var result = await CreateService().CloseAsync(_seasonId, openTheNextSeason: false);

        Assert.Equal(_seasonId, result.SeasonId);

        var managed = _clubs.Values.Where(club => club.IsManagerClub).Select(club => club.Id).ToArray();
        Assert.Equal(2, managed.Length);
        Assert.Equal(managed.Length, Letters.Count);
        Assert.All(Letters, letter => Assert.Equal($"season-summary:{_seasonId}", letter.Reference));
        Assert.Equal(managed.OrderBy(id => id), Letters.Select(letter => letter.RecipientTeamId).OrderBy(id => id));

        // Every division is in it: the letter is the country changing shape, and a 4ª that was
        // left out is a season in which a whole tier was never told where it finished.
        foreach (var tier in Enumerable.Range(1, 4))
        {
            Assert.Contains(
                CompetitionRules.DivisionName(tier),
                Letters[0].Body,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Two managers, two letters, and each one opens with his own club's place — the letter is
    /// the same story read by two different men, not one document in two envelopes.
    /// </summary>
    [Fact]
    public async Task EachLetterOpensWithThePlaceOfItsOwnClub()
    {
        await CreateService().CloseAsync(_seasonId, openTheNextSeason: false);

        Assert.Equal(2, Letters.Count);

        foreach (var letter in Letters)
        {
            var club = _clubs[letter.RecipientTeamId];
            Assert.StartsWith($"Para {club.Name}", letter.Body, StringComparison.Ordinal);
        }

        // The same facts about the same country, written for two different readers.
        Assert.NotEqual(Letters[0].Body, Letters[1].Body);
    }

    /// <summary>
    /// A close that runs twice — by the window that finished the season and by a process that
    /// was down over the weekend — is one letter, not two.
    ///
    /// <para>
    /// The letter is keyed on the season rather than on the close, and a season that has already
    /// been finished says so instead of being told about again. Both guards are asked here
    /// through the same door: the second call finds the season closed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ASeasonIsNotSummarisedTwice()
    {
        await CreateService().CloseAsync(_seasonId, openTheNextSeason: false);

        // The reference is the guard, and a repository that has the message in it says so.
        _messages.Setup(repo => repo.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);


        var again = await CreateService().CloseAsync(_seasonId, openTheNextSeason: false);

        Assert.Equal(_seasonId, again.SeasonId);
        Assert.Equal(
            _clubs.Values.Count(club => club.IsManagerClub),
            Letters.Count);
    }
}
