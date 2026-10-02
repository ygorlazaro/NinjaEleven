using Moq;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// A club's own page, and the one thing it is not allowed to do.
///
/// <para>
/// The screen this serves used to read a stand-in that drew its history out of the club's own
/// id, which had exactly the property that made it dangerous: it looked correct forever and was
/// wrong from the first season, and a manager had no way to tell it from a page that worked.
/// So the tests below are mostly about where each fact comes from — a movement is two rows of
/// the pyramid compared rather than a row somebody wrote, a shelf medal is named with the
/// division the club was in <em>then</em>, and a decision the world keeps nothing about is a
/// row written when it was made.
/// </para>
/// </summary>
public class ClubProfileServiceTests
{
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<ITrophyRepository> _trophies = new(MockBehavior.Loose);
    private readonly Mock<IClubEventRepository> _events = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);

    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Team _team = Team.Create("Náutico", "NAU", "#0a3d62", "#f2d34f");

    private readonly List<Season> _world = new();
    private readonly List<ClubDivisionSeason> _divisions = new();
    private readonly List<CompetitionSeasonView> _views = new();

    public ClubProfileServiceTests()
    {
        _teams.Setup(repository => repository.GetAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_team);
        _teams.Setup(repository => repository.ListDivisionSeasonsAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _divisions);

        _seasons.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _world);
        _seasons.Setup(repository => repository.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _world.OrderBy(season => season.Number).LastOrDefault());

        _competitions.Setup(repository => repository.ListSeasonViewsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
            {
                var wanted = ids.ToHashSet();
                return _world
                    .Where(season => wanted.Contains(season.Id))
                    .ToDictionary(
                        season => season.Id,
                        season => (IReadOnlyList<CompetitionSeasonView>)_views
                            .Where(view => view.SeasonId == season.Id)
                            .ToList());
            });

        _trophies.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TrophyAward>());
        _events.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ClubEvent>());
        _teams.Setup(repository => repository.GetSquadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TeamMembership>());
        _players.Setup(repository => repository.ListEditionScorerLinesForEditionsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EditionScorerLine>());
    }

    private ClubProfileService Service() => new(
        _teams.Object, _players.Object, _seasons.Object, _competitions.Object,
        _trophies.Object, _events.Object, _finance.Object);

    // --- The world the tests are played in ---------------------------------------------

    /// <summary>
    /// One season of the world, and the division the club was in it.
    /// </summary>
    private Season GivenSeason(int number, int? tier = null)
    {
        var season = Season.Create(number, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        _world.Add(season);

        if (tier is null)
        {
            return season;
        }

        var edition = Guid.NewGuid();
        _views.Add(new CompetitionSeasonView
        {
            Id = edition,
            CompetitionId = Guid.NewGuid(),
            SeasonId = season.Id,
            DivisionId = Guid.NewGuid(),
            Tier = tier,
            CompetitionName = "Campeonato Brasileiro",
            Type = CompetitionType.League
        });

        _divisions.Add(new ClubDivisionSeason
        {
            SeasonId = season.Id,
            CompetitionSeasonId = edition,
            DivisionId = _views[^1].DivisionId!.Value,
            Tier = tier.Value
        });

        return season;
    }

    // --- The founding -------------------------------------------------------------------

    [Fact]
    public async Task AClubWithNoCareerHasNoFoundingAndNoShelf()
    {
        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Empty(profile.History);
        Assert.Empty(profile.Trophies);
        Assert.Equal(0, profile.Promotions);
        Assert.Equal(0, profile.Relegations);
    }

    [Fact]
    public async Task TheFirstSeasonIsDerivedFromTheClubsEarliestParticipation()
    {
        GivenSeason(1, tier: 3);
        GivenSeason(2, tier: 3);
        GivenSeason(3, tier: 2);

        var profile = await Service().GetProfileAsync(_teamId);

        // One founding, ever. A club that played three seasons has one beginning and not three,
        // and the beginning is the earliest season it was actually in the pyramid for.
        var founding = Assert.Single(
            profile.History, entry => entry.Kind == ClubHistoryKind.FirstSeason);

        Assert.Equal(1, founding.SeasonNumber);
        Assert.Equal("Primeira temporada na pirâmide, na 3ª Divisão.", founding.Description);
    }

    /// <summary>
    /// A club created into the world late has a history that begins late.
    /// </summary>
    /// <remarks>
    /// The founding is the club's earliest participation and not the world's first season. A
    /// club drawn into the second division in season five has not been in the first, and a
    /// founding dated to the world's opening day would be a moment that never happened — with
    /// the added harm that every club in the pyramid would then share the same founding date
    /// and the page's one real claim would say nothing about any of them.
    /// </remarks>
    [Fact]
    public async Task AClubThatJoinsTheWorldLateIsFoundedWhenItJoined()
    {
        GivenSeason(1);
        GivenSeason(2);
        GivenSeason(5, tier: 2);

        var profile = await Service().GetProfileAsync(_teamId);

        var founding = Assert.Single(
            profile.History, entry => entry.Kind == ClubHistoryKind.FirstSeason);

        Assert.Equal(5, founding.SeasonNumber);
        Assert.Equal("Temporada V", founding.SeasonName);
        Assert.Contains("2ª Divisão", founding.Description);
    }

    // --- Promotions and relegations ------------------------------------------------------

    [Fact]
    public async Task AClubThatClimbsAndFallsHasEachMovementSaidOnceAndInItsOwnSeason()
    {
        GivenSeason(1, tier: 3);
        GivenSeason(2, tier: 2);
        GivenSeason(3, tier: 1);
        GivenSeason(4, tier: 2);

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Equal(2, profile.Promotions);
        Assert.Equal(1, profile.Relegations);

        // Ordered newest first, and the movement is dated to the season the club ARRIVED in —
        // a promotion is what happened in season three, not in the season it was promoted from.
        var moves = profile.History
            .Where(entry => entry.Kind is ClubHistoryKind.Promotion or ClubHistoryKind.Relegation)
            .ToList();

        Assert.Equal(new int?[] { 4, 3, 2 }, moves.Select(move => move.SeasonNumber).ToArray());
        Assert.Equal(
            new[] { ClubHistoryKind.Relegation, ClubHistoryKind.Promotion, ClubHistoryKind.Promotion },
            moves.Select(move => move.Kind).ToArray());
    }

    [Fact]
    public async Task AClubThatStayedPutHasNoMovementAtAll()
    {
        GivenSeason(1, tier: 2);
        GivenSeason(2, tier: 2);
        GivenSeason(3, tier: 2);

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Equal(0, profile.Promotions);
        Assert.Equal(0, profile.Relegations);
        Assert.DoesNotContain(profile.History, entry => entry.Kind == ClubHistoryKind.Promotion);
    }

    /// <summary>
    /// A skipped season is not a relegation.
    /// </summary>
    /// <remarks>
    /// The pyramid is drawn one season at a time out of the season before it, so a club that
    /// was not in the world for a season was not climbing past a division it never played in.
    /// Comparing the two seasons either side of the gap would invent a promotion on the way out
    /// and a relegation on the way back in, and the club's page would show both — which is a
    /// club that went down to the fourth division and came back up again without playing a game.
    /// </remarks>
    [Fact]
    public async Task ASkippedSeasonIsNotAMovement()
    {
        GivenSeason(1, tier: 1);
        GivenSeason(3, tier: 2);

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Equal(0, profile.Promotions);
        Assert.Equal(0, profile.Relegations);
    }

    /// <summary>
    /// A movement's id carries both of the seasons it sits between, and reads the same twice.
    /// </summary>
    /// <remarks>
    /// A promotion is not a row anywhere — it is two rows of the pyramid compared — so its
    /// identity has to be built out of the two seasons it joins. Built out of one, a club that
    /// went up and came back down over two seasons would produce the same id twice, and a
    /// client holding on to it across two reads of the page would see one line change places
    /// under the manager who is reading it. The stability is the whole contract: reading the
    /// page twice has to give the same ids in the same order.
    /// </remarks>
    [Fact]
    public async Task AMovementIsIdentifiedByTheTwoSeasonsItSitsBetween()
    {
        var first = GivenSeason(1, tier: 3);
        var second = GivenSeason(2, tier: 1);

        var profile = await Service().GetProfileAsync(_teamId);

        var promotion = Assert.Single(
            profile.History, entry => entry.Kind == ClubHistoryKind.Promotion);

        Assert.Contains(first.Id.ToString(), promotion.Id);
        Assert.Contains(second.Id.ToString(), promotion.Id);

        // Read the page again: the same movement has to arrive with the same identity, because a
        // client keys its animation and its scroll position off it.
        var again = await Service().GetProfileAsync(_teamId);

        Assert.Equal(
            promotion.Id,
            Assert.Single(
                again.History, entry => entry.Kind == ClubHistoryKind.Promotion).Id);
    }

    /// <summary>
    /// A club that goes up and comes back down says so twice, and the two are told apart.
    /// </summary>
    [Fact]
    public async Task GoingUpAndComingBackDownIsOnePromotionAndOneRelegation()
    {
        GivenSeason(1, tier: 2);
        GivenSeason(2, tier: 1);
        GivenSeason(3, tier: 3);

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Equal(1, profile.Promotions);
        Assert.Equal(1, profile.Relegations);

        var moves = profile.History
            .Where(entry => entry.Kind is ClubHistoryKind.Promotion or ClubHistoryKind.Relegation)
            .ToList();

        // Two movements, two ids. An identity built from the arriving season alone would be
        // unique here too, but one built from the arriving season and its direction would still
        // collide the moment a club was promoted and relegated in the same pair of seasons —
        // which is a thing the pyramid does.
        Assert.Equal(2, moves.Select(move => move.Id).Distinct().Count());
    }

    // --- The shelf -----------------------------------------------------------------------

    [Fact]
    public async Task AMedalIsNamedWithTheDivisionTheClubWasInWhenItWonIt()
    {
        var season = GivenSeason(1, tier: 2);
        var edition = _divisions[0].CompetitionSeasonId;

        _trophies.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                TrophyAward.Create(_teamId, season.Id, edition, _divisions[0].DivisionId, TrophyKind.Champion)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var medal = Assert.Single(profile.Trophies);

        Assert.Equal("2ª Divisão", medal.DivisionName);
        Assert.Equal(2, medal.DivisionTier);
        Assert.Equal("2ª Divisão", medal.Competition);
    }

    /// <summary>
    /// A club relegated out of the division it won keeps the medal it won.
    /// </summary>
    /// <remarks>
    /// This is the whole reason the shelf reads the tier off the club's participation in that
    /// season rather than off where the club plays now. Copying today's tier onto last season's
    /// medal would quietly move the club's own history down a division every time it dropped,
    /// and a shelf of titles that changes its mind is a worse page than no shelf at all.
    /// </remarks>
    [Fact]
    public async Task AClubRelegatedOutOfTheDivisionItWonStillHoldsThatDivisionMedal()
    {
        var first = GivenSeason(1, tier: 1);
        GivenSeason(2, tier: 2);

        _trophies.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                TrophyAward.Create(
                    _teamId, first.Id, _divisions[0].CompetitionSeasonId,
                    _divisions[0].DivisionId, TrophyKind.Champion)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var medal = Assert.Single(profile.Trophies);

        Assert.Equal("1ª Divisão", medal.DivisionName);
        Assert.Equal(1, medal.DivisionTier);
    }

    [Fact]
    public async Task ACupMedalIsNamedByItsCompetitionAndHasNoDivision()
    {
        var season = GivenSeason(1, tier: 2);
        var cup = new CompetitionSeasonView
        {
            Id = Guid.NewGuid(),
            CompetitionId = Guid.NewGuid(),
            SeasonId = season.Id,
            DivisionId = null,
            Tier = null,
            CompetitionName = "Copa do Brasil",
            Type = CompetitionType.Cup
        };
        _views.Add(cup);

        _trophies.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                TrophyAward.Create(_teamId, season.Id, cup.Id, null, TrophyKind.Champion)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var medal = Assert.Single(profile.Trophies);

        Assert.Equal("Copa do Brasil", medal.Competition);
        Assert.Null(medal.DivisionName);
        Assert.Null(medal.DivisionTier);
    }

    [Fact]
    public async Task ARunnerUpIsOnTheShelfAndNotInTheHistory()
    {
        var season = GivenSeason(1, tier: 1);

        _trophies.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                TrophyAward.Create(
                    _teamId, season.Id, _divisions[0].CompetitionSeasonId,
                    _divisions[0].DivisionId, TrophyKind.RunnerUp)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Single(profile.Trophies);
        Assert.DoesNotContain(profile.History, entry => entry.Kind == ClubHistoryKind.Title);
    }

    // --- Titles -------------------------------------------------------------------------

    [Fact]
    public async Task ATitleIsSaidWithTheCompetitionItWasWonIn()
    {
        var season = GivenSeason(1, tier: 1);

        _trophies.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                TrophyAward.Create(
                    _teamId, season.Id, _divisions[0].CompetitionSeasonId,
                    _divisions[0].DivisionId, TrophyKind.Champion)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var title = Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.Title);

        Assert.Contains("1ª Divisão", title.Description);
        Assert.Equal(season.Id, title.SeasonId);
    }

    // --- The artilharia ------------------------------------------------------------------

    [Fact]
    public async Task AStrikerWhoToppedHisEditionIsInTheHistory()
    {
        var season = GivenSeason(1, tier: 1);
        var striker = Player.Create("Zé Ramalho", 26, Position.ATT, 15, 15, 14, 13, 13, goalkeeperPower: 1, reflexes: 1);

        var rival = Player.Create("Silvio Santos", 24, Position.ATT, 15, 15, 14, 13, 13, goalkeeperPower: 1, reflexes: 1);
        var other = Guid.NewGuid();

        _players.Setup(repository => repository.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { striker, rival });

        _players.Setup(repository => repository.ListEditionScorerLinesForEditionsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Line(_divisions[0].CompetitionSeasonId, striker.Id, _teamId, goals: 18),
                Line(_divisions[0].CompetitionSeasonId, rival.Id, other, goals: 11)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var scored = Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.TopScorer);

        Assert.Contains("Zé Ramalho", scored.Description);
        Assert.Equal(18, scored.Value);
        Assert.Equal(season.Id, scored.SeasonId);
    }

    /// <summary>
    /// The chart is the edition's and not the club's.
    /// </summary>
    /// <remarks>
    /// A striker who outscored everybody in his own club has not necessarily topped anything:
    /// somebody in another division may have scored more in his own edition. Ranking the club's
    /// men against each other would give a club's page a top scorer that the game never
    /// crowned, a prize list and a club's own history would disagree about, and the manager
    /// would be told his striker led a chart he never led.
    /// </remarks>
    [Fact]
    public async Task AStrikerWhoOnlyLedHisOwnClubIsNotTheArtilleryOfAnything()
    {
        GivenSeason(1, tier: 3);
        var betterElsewhere = Player.Create("Zé Ramalho", 26, Position.ATT, 15, 15, 14, 13, 13, goalkeeperPower: 1, reflexes: 1);
        var ourMan = Player.Create("Nivaldo", 27, Position.ATT, 15, 15, 14, 13, 13, goalkeeperPower: 1, reflexes: 1);
        var topClub = Guid.NewGuid();

        _players.Setup(repository => repository.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { betterElsewhere, ourMan });

        _players.Setup(repository => repository.ListEditionScorerLinesForEditionsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Line(_divisions[0].CompetitionSeasonId, betterElsewhere.Id, topClub, goals: 22),
                Line(_divisions[0].CompetitionSeasonId, ourMan.Id, _teamId, goals: 9)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.DoesNotContain(profile.History, entry => entry.Kind == ClubHistoryKind.TopScorer);
    }

    /// <summary>
    /// The whole career's competitions are read as one set.
    /// </summary>
    /// <remarks>
    /// A club in the pyramid for five seasons is enrolled in five divisions' editions and five
    /// cups' — twenty editions, each one a walk through a season's match lines. Asked one at a
    /// time that is a page which reads in half a second per competition and a hundred round
    /// trips underneath, and the reader that answers a set is the difference between the club's
    /// own page being instant and being a spinner.
    /// </remarks>
    [Fact]
    public async Task EveryEditionOfTheClubsCareerIsAskedForInOneRead()
    {
        for (var number = 1; number <= 5; number++)
        {
            GivenSeason(number, tier: 1);
        }

        await Service().GetProfileAsync(_teamId);

        _players.Verify(
            repository => repository.ListEditionScorerLinesForEditionsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- What was recorded ----------------------------------------------------------------

    [Fact]
    public async Task ARenameIsSaidFromTheTwoNamesItMovedBetween()
    {
        var season = GivenSeason(1, tier: 1);

        _events.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ClubEvent.Create(
                    _teamId, ClubEventKind.NameChange, season.Id,
                    "Náutico de Guanabara", "Náutico")
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var rename = Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.NameChange);

        Assert.Contains("Náutico de Guanabara", rename.Description);
        Assert.Contains("Náutico", rename.Description);
        Assert.Equal(season.Id, rename.SeasonId);
    }

    [Fact]
    public async Task ACrestTakenAwayIsNotSaidAsANewOne()
    {
        GivenSeason(1, tier: 1);

        _events.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ClubEvent.Create(_teamId, ClubEventKind.CrestChange)
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var crest = Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.CrestChange);

        Assert.Contains("retirado", crest.Description);
    }

    /// <summary>
    /// A rename between seasons is kept and dated by when it happened.
    /// </summary>
    /// <remarks>
    /// A manager renames a club between seasons quite often, and a history that threw those
    /// moments away would be a history of the football only — which is the half a manager is
    /// already looking at when he opens the page.
    /// </remarks>
    [Fact]
    public async Task AMomentWithNoSeasonIsStillOnThePage()
    {
        GivenSeason(1, tier: 1);

        _events.Setup(repository => repository.ListByTeamAsync(
                _teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ClubEvent.Create(_teamId, ClubEventKind.NameChange, null, null, "Náutico")
            });

        var profile = await Service().GetProfileAsync(_teamId);

        var rename = Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.NameChange);

        Assert.Null(rename.SeasonNumber);
        Assert.Contains("Náutico", rename.Description);
    }

    // --- The club itself -------------------------------------------------------------------

    [Fact]
    public async Task AClubNobodyIsRunningHasNoManagerRatherThanAFictionalOne()
    {
        GivenSeason(1, tier: 1);

        var profile = await Service().GetProfileAsync(_teamId);

        // Empty, not a name. An NPC club has no manager, and filling the gap with somebody would
        // be the game inventing a man who does not exist.
        Assert.Equal(string.Empty, profile.CoachName);
    }

    [Fact]
    public async Task TheBalanceIsTheLedgersLastLineAndNotASumOfIt()
    {
        GivenSeason(1, tier: 1);

        _finance.Setup(repository => repository.GetLastAsync(_teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FinanceMovement.Create(
                _teamId, Guid.NewGuid(), 1, null, FinanceMovementKind.GateRevenue,
                "Bilheteria", 1_000m, 0m));

        var profile = await Service().GetProfileAsync(_teamId);

        Assert.Equal(1_000m, profile.Balance);
    }

    [Fact]
    public async Task AClubThatDoesNotExistIsRefusedRatherThanDrawnEmpty()
    {
        var missing = Guid.NewGuid();
        _teams.Setup(repository => repository.GetAsync(missing, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Service().GetProfileAsync(missing));
    }

    private static EditionScorerLine Line(
        Guid editionId, Guid playerId, Guid teamId, int goals) => new()
    {
        CompetitionSeasonId = editionId,
        PlayerId = playerId,
        TeamId = teamId,
        Goals = goals,
        Started = 20,
        OwnGoals = 0,
        YellowCards = 1,
        RedCards = 0
    };
}