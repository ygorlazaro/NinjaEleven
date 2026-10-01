using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The two messages that are written to everybody rather than to one club: a cup round, and the
/// season that has ended.
/// </summary>
/// <remarks>
/// <para>
/// They are the two exceptions in the box, and the exception is the point. Every other message
/// names one club because it is about one club — this club's title, this club's player, this
/// club's match. A cup round and a season's last day are the country's football: the round of
/// sixteen is where the season's biggest clubs start falling out, and a season ends by
/// rearranging all four divisions at once. A manager in the 4ª Divisão watched two clubs leave
/// his table and two arrive, and he is owed that news even though neither of them is his.
/// </para>
///
/// <para>
/// They are still delivered only to a club with a person behind it, which is the same rule every
/// writer goes through — and the rule is asked of the world rather than of the request, so a
/// scheduler walking the cup at three in the morning tells the same managers a hand pressing the
/// button would. A world of nobody writes nothing at all, and these hold that answer too: the
/// alternative is a box that a test passes by accident because its mock had managers in it.
/// </para>
/// </remarks>
public class InboxFanOutTests
{
    private readonly Mock<IInboxMessageRepository> _messages = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    private readonly List<InboxMessage> _written = [];

    private readonly Team _first = ManagedClub("Esporte Clube Riachuelo");
    private readonly Team _second = ManagedClub("Porto Marítimo");
    private readonly Team _third = ManagedClub("Grêmio Ferroviário do Sul");

    public InboxFanOutTests()
    {
        var everyClub = new[] { _first, _second, _third };

        _teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, _) => Task.FromResult(
                everyClub.FirstOrDefault(club => club.Id == id)));

        _teams.Setup(repo => repo.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<Guid>, CancellationToken>((ids, _) =>
                Task.FromResult<IReadOnlyList<Team>>(ids
                    .Select(id => everyClub.FirstOrDefault(club => club.Id == id))
                    .Where(club => club is not null)
                    .Select(club => club!)
                    .ToList()));

        // The three clubs above are the ones with a person behind them, and the box is built over
        // that world rather than over a default one: these two messages are the fan-out, and a
        // test of the fan-out that ran in a world of nobody would pass by writing nothing.
        _box = InboxTestFactory.Create(_teams, _messages, new ManagedClubs(_first.Id, _second.Id, _third.Id));

        // After the box, so this is the answer the call gets: a mock takes the last setup it was
        // given for a call, and the factory sets up the same AddAsync this test has to watch.
        _messages.Setup(repo => repo.AddAsync(It.IsAny<InboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback((InboxMessage message, CancellationToken _) => _written.Add(message))
            .Returns(Task.CompletedTask);
    }

    private InboxService _box = null!;

    private static Team ManagedClub(string name)
    {
        var club = Team.Create(name, name[..3], "#0a5", "#fff");
        club.MarkAsManagerClub();

        return club;
    }

    /// <summary>
    /// One round of the cup: three ties, one of them decided on penalties.
    ///
    /// The three shapes are there because each is written differently and a test that only had
    /// the ordinary one would not notice a line that said "no agregado" about a tie that went to
    /// penalties — where the aggregate was level and the kicks decided it.
    /// </summary>
    private CupRoundFacts ARound() => new()
    {
        CompetitionSeasonId = Guid.NewGuid(),
        CupName = "Copa do Brasil",
        SeasonName = "Temporada II",
        RoundNumber = 4,
        Ties =
        [
            new CupRoundTieFacts
            {
                HomeTeamId = Guid.NewGuid(),
                HomeClubName = "Esporte Clube Riachuelo",
                HomeGoals = 2,
                AwayTeamId = Guid.NewGuid(),
                AwayClubName = "Porto Marítimo",
                AwayGoals = 0,
                WinnerTeamId = null,
                IsSecondLeg = false,
                WentToPenalties = false
            },
            new CupRoundTieFacts
            {
                HomeTeamId = Guid.NewGuid(),
                HomeClubName = "Clube Atlético Raio",
                HomeGoals = 3,
                AwayTeamId = Guid.NewGuid(),
                AwayClubName = "Sociedade Esportiva Diamante",
                AwayGoals = 1,
                WinnerTeamId = Guid.NewGuid(),
                IsSecondLeg = true,
                WentToPenalties = false
            },
            new CupRoundTieFacts
            {
                HomeTeamId = Guid.NewGuid(),
                HomeClubName = "Grêmio Esportivo Andorinha",
                HomeGoals = 1,
                AwayTeamId = Guid.NewGuid(),
                AwayClubName = "Esporte Clube Laranjeiras",
                AwayGoals = 1,
                WinnerTeamId = Guid.NewGuid(),
                IsSecondLeg = true,
                WentToPenalties = true
            }
        ]
    };

    /// <summary>
    /// One season over: two divisions, three clubs, one staying, one down and one up.
    ///
    /// The lines carry the managed clubs' own ids rather than invented ones, because the letter
    /// opens with the reader's own place and a line whose team nobody manages would leave every
    /// copy saying the same thing — which is the one thing this letter must never do.
    /// </summary>
    private SeasonSummaryFacts ASeason() => new()
    {
        SeasonName = "Temporada I",
        SeasonId = Guid.NewGuid(),
        Divisions =
        [
            new SeasonSummaryDivision
            {
                Tier = 2,
                DivisionName = "2ª Divisão",
                Lines =
                [
                    new SeasonSummaryLine
                    {
                        TeamId = _first.Id,
                        ClubName = _first.Name,
                        Position = 1,
                        Points = 58,
                        Played = 30,
                        Wins = 18,
                        Draws = 4,
                        Losses = 8,
                        GoalsFor = 44,
                        GoalsAgainst = 26,
                        Movement = SeasonMovementKind.Stays
                    },
                    new SeasonSummaryLine
                    {
                        TeamId = _second.Id,
                        ClubName = _second.Name,
                        Position = 16,
                        Points = 22,
                        Played = 30,
                        Wins = 6,
                        Draws = 4,
                        Losses = 20,
                        GoalsFor = 21,
                        GoalsAgainst = 49,
                        Movement = SeasonMovementKind.Relegated
                    }
                ]
            },
            new SeasonSummaryDivision
            {
                Tier = 3,
                DivisionName = "3ª Divisão",
                Lines =
                [
                    new SeasonSummaryLine
                    {
                        TeamId = _third.Id,
                        ClubName = _third.Name,
                        Position = 1,
                        Points = 61,
                        Played = 30,
                        Wins = 19,
                        Draws = 4,
                        Losses = 7,
                        GoalsFor = 47,
                        GoalsAgainst = 24,
                        Movement = SeasonMovementKind.Promoted
                    }
                ]
            }
        ]
    };

    /// <summary>
    /// A cup round reaches every manager, and each copy carries his own club as the recipient.
    ///
    /// The recipient is the load-bearing part: a row addressed to nobody is a row no box can
    /// list and no unread count can count, so a fan-out that stamped the address once would
    /// write one message into a void.
    /// </summary>
    [Fact]
    public async Task ACupRoundReachesEveryManager()
    {
        var round = ARound();

        await _box.PostCupRoundAsync(round);

        Assert.Equal(3, _written.Count);
        Assert.Equal(
            new[] { _first.Id, _second.Id, _third.Id }.OrderBy(id => id),
            _written.Select(message => message.RecipientTeamId).OrderBy(id => id));
    }

    /// <summary>
    /// The tie is written with what decided it. A second leg on penalties is the case a manager
    /// gets wrong reading a scoreline: 1 x 1 with eleven kicks behind it is not a draw, and the
    /// line has to say so or a box is telling him two clubs are still playing.
    /// </summary>
    [Fact]
    public async Task ATieSaysWhatDecidedIt()
    {
        await _box.PostCupRoundAsync(ARound());

        var message = _written[0];

        Assert.Contains("Esporte Clube Riachuelo 2 x 0 Porto Marítimo", message.Body);
        Assert.Contains("Clube Atlético Raio 3 x 1 Sociedade Esportiva Diamante", message.Body);
        Assert.Contains("no agregado", message.Body);
        Assert.Contains("nos pênaltis", message.Body);
    }

    /// <summary>
    /// The round is what makes it once. A round closed twice — by the run that played its last
    /// tie and by a process that was down over the weekend — is one letter, not two.
    /// </summary>
    [Fact]
    public async Task ACupRoundIsKeyedOnTheEditionAndTheRound()
    {
        var round = ARound();

        await _box.PostCupRoundAsync(round);

        Assert.Equal($"cup-round:{round.CompetitionSeasonId}:4", _written[0].Reference);
    }

    /// <summary>
    /// A season's end reaches every manager, and each copy opens with his own club's place.
    ///
    /// That first line is the whole reason the message is per manager: the four tables are the
    /// same letter to everybody, and the sentence above them is not.
    /// </summary>
    [Fact]
    public async Task ASeasonSummaryReachesEveryManagerAndNamesHisOwnPlace()
    {
        var season = ASeason();
        var lines = season.Divisions[0].Lines;
        var club = lines[0];

        await _box.PostSeasonSummaryAsync(season);

        Assert.Equal(3, _written.Count);

        // The copy that went to Riachuelo opens with Riachuelo's own place and points; the
        // other club of his table is in the table below it, which is where every other club in
        // the country is.
        var mine = _written.Single(message => message.RecipientTeamId == _first.Id);
        Assert.Contains($"O {club.ClubName} terminou em 1º na 2ª Divisão", mine.Body);
        Assert.Contains("58 pontos", mine.Body);
        Assert.Contains($"16º {lines[1].ClubName} — 22 pts", mine.Body);
    }

    /// <summary>The four tables are in the letter, movement marks and all.</summary>
    [Fact]
    public async Task ASeasonSummaryCarriesTheFourTablesAndTheMovement()
    {
        await _box.PostSeasonSummaryAsync(ASeason());

        var message = _written[0];

        Assert.Contains("2ª Divisão", message.Body);
        Assert.Contains("3ª Divisão", message.Body);
        Assert.Contains("Caíram: Porto Marítimo", message.Body);
        Assert.Contains("Subiram: Grêmio Ferroviário do Sul", message.Body);
        Assert.Contains("↓ cai", message.Body);
        Assert.Contains("↑ sobe", message.Body);
    }

    /// <summary>
    /// The season is what makes it once, because a season is closed when its last window is
    /// played and a window can be closed twice.
    /// </summary>
    [Fact]
    public async Task ASeasonSummaryIsKeyedOnTheSeason()
    {
        var season = ASeason();

        await _box.PostSeasonSummaryAsync(season);

        Assert.Equal($"season-summary:{season.SeasonId}", _written[0].Reference);
    }

    /// <summary>
    /// A world of nobody is told nothing. Both of these messages are written to the managers of
    /// the world, so a world with no managers writes no messages and pays no queries — which is
    /// also what makes it safe to leave the fan-out in the walk rather than in a screen.
    /// </summary>
    [Fact]
    public async Task AWorldWithoutAManagersIsToldNothing()
    {
        var box = InboxTestFactory.Create(_teams, _messages, new ManagedClubs());

        await box.PostCupRoundAsync(ARound());
        await box.PostSeasonSummaryAsync(ASeason());

        Assert.Empty(_written);
    }

    /// <summary>
    /// A round with no ties in it is not news. A bracket that has not been drawn yet has no
    /// ties to report, and a letter saying so would be a letter about nothing.
    /// </summary>
    [Fact]
    public async Task ARoundWithNothingInItSaysNothing()
    {
        var round = ARound();

        await _box.PostCupRoundAsync(new CupRoundFacts
        {
            CompetitionSeasonId = round.CompetitionSeasonId,
            CupName = round.CupName,
            SeasonName = round.SeasonName,
            RoundNumber = round.RoundNumber,
            Ties = []
        });

        Assert.Empty(_written);
    }
}