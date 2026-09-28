using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// What a competition pays its artilharia, and who is paid.
///
/// The money is the whole point, and it has two shapes. A division's three top scorers are paid
/// out of that division's own title, so a third-division striker's prize is a share of a much
/// smaller cheque. A cup has no title of its own to be a share of, so each of its scorers is
/// paid out of the title of the division his club is in — and the answer says whose title that
/// was, because a cup paid out of the first division's purse would hand a third-division
/// forward a first-division cheque for the same goals.
///
/// Nothing here is filtered by club: a club can take two of the three prizes, and a cup scorer
/// whose club is in no division at all is reported with no prize rather than with a price the
/// service made up.
/// </summary>
public class ScorerPrizeServiceTests
{
    private const int ClubsPerDivision = 12;

    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IPlayerRepository> _players = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<IFixtureRepository> _fixtures = new(MockBehavior.Loose);
    private readonly Mock<IRoundRepository> _rounds = new(MockBehavior.Loose);
    private readonly Mock<IMatchDayRepository> _matchDays = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    /// <summary>
    /// The cheques the club's book was given, in the order they were written, and the set of
    /// references already on it — which is what makes a second ask pay nothing.
    /// </summary>
    private readonly List<FinanceMovement> _theBook = new();
    private readonly HashSet<string> _thePaidReferences = new();

    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Guid _championshipId = Guid.NewGuid();
    private readonly Guid _cupId = Guid.NewGuid();

    private readonly Team _top = Team.Create("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 82);
    private readonly Team _middle = Team.Create("Estrela do Norte", "EDN", "#0F5132", "#FFD700", 71);
    private readonly Team _bottom = Team.Create("Grêmio do Sul", "GDS", "#123A8C", "#EEEEEE", 60);

    private readonly List<Player> _theSquads = new();
    private readonly List<ClubScorerLine> _theGoals = new();
    private readonly List<CompetitionSeasonView> _theEditions = new();

    private ScorerPrizeService Service() => new(
        _competitions.Object,
        _players.Object,
        _teams.Object,
        new FinanceService(
            _finance.Object,
            _teams.Object,
            _players.Object,
            _fixtures.Object,
            _rounds.Object,
            _matchDays.Object,
            _seasons.Object,
            _unitOfWork.Object,
            NullLogger<FinanceService>.Instance),
        NullLogger<ScorerPrizeService>.Instance);

    private void GivenTheBookRefusesAPrizeItAlreadyHolds()
    {
        _finance.Setup(repository => repository.ExistsWithReferenceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<FinanceMovementKind>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, FinanceMovementKind _, string reference, CancellationToken _) =>
                _thePaidReferences.Contains(reference));

        _finance.Setup(repository => repository.AddAsync(
                It.IsAny<FinanceMovement>(), It.IsAny<CancellationToken>()))
            .Callback((FinanceMovement movement, CancellationToken _) =>
            {
                _theBook.Add(movement);
                _thePaidReferences.Add(movement.Reference!);
            })
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ADivisionsArtilhariaIsTenFiveAndThreePerCentOfItsOwnTitle()
    {
        var division = GivenADivision(1, _top);
        GivenAStriker("Zé do Göl", _top.Id, goals: 18);
        GivenAStriker("Bilu", _top.Id, goals: 14);
        GivenAStriker("Careca", _top.Id, goals: 9);
        GivenAStriker("Zé do Canto", _middle.Id, goals: 4);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(division.Id);

        var champion = PrizeRules.ChampionshipPrize(1, ClubsPerDivision, PrizeRules.PurseForTier(1));
        Assert.Equal("1ª Divisão", list.CompetitionName);
        Assert.Equal(1, list.Tier);
        Assert.Equal(champion, list.BaseAmount);
        Assert.Equal(3, list.Winners.Count);
        Assert.Equal(decimal.Round(champion * 0.10m, 2), list.Winners[0].Amount);
        Assert.Equal(decimal.Round(champion * 0.05m, 2), list.Winners[1].Amount);
        Assert.Equal(decimal.Round(champion * 0.03m, 2), list.Winners[2].Amount);
    }

    [Fact]
    public async Task AFirstDivisionStrikerPlayingInTheThirdDivisionTableIsPaidTheThirdDivisionsMoney()
    {
        var division = GivenADivision(3, _bottom);
        GivenAStriker("Pé de Chumbo", _bottom.Id, goals: 16);
        GivenAStriker("Canhoto", _top.Id, goals: 15);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(division.Id);

        var thirdChampion = PrizeRules.ChampionshipPrize(
            1, ClubsPerDivision, PrizeRules.PurseForTier(3));
        Assert.Equal(thirdChampion, list.BaseAmount);
        Assert.Equal(decimal.Round(thirdChampion * 0.10m, 2), list.Winners[0].Amount);
        Assert.Equal(decimal.Round(thirdChampion * 0.05m, 2), list.Winners[1].Amount);
    }

    [Fact]
    public async Task ACupsArtilhariaIsTenFiveAndThreePerCentOfTheCupsOwnTitle()
    {
        GivenADivision(1, _top);
        GivenADivision(2, _middle);
        GivenADivision(3, _bottom);
        var cup = GivenTheCup();
        GivenAStriker("Astronauta", _top.Id, goals: 7, appearances: 6);
        GivenAStriker("Meia-Noite", _middle.Id, goals: 6, appearances: 6);
        GivenAStriker("Pedrinho", _bottom.Id, goals: 5, appearances: 6);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(cup.Id);

        Assert.Equal("Copa do Brasil", list.CompetitionName);
        Assert.Null(list.Tier);
        // The cup pays its champion one cheque that is nobody's share of anything, and that
        // cheque is what the artilharia is a share of.
        Assert.Equal(PrizeRules.CupChampionPrize, list.BaseAmount);
        Assert.Equal(3, list.Winners.Count);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.10m, 2), list.Winners[0].Amount);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.05m, 2), list.Winners[1].Amount);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.03m, 2), list.Winners[2].Amount);
    }

    [Fact]
    public async Task ACupStrikerIsPaidTheCupsMoneyWhicheverDivisionHisClubIsIn()
    {
        // The cup runs across the pyramid, so the shirt a man scored in is not what he is paid
        // for scoring in it: a third-division forward at the top of the cup's scoring is paid
        // what a first-division one is paid, because the prize is the cup's.
        GivenADivision(1, _top);
        GivenADivision(3, _bottom);
        var cup = GivenTheCup();
        GivenAStriker("Pé de Chumbo", _bottom.Id, goals: 9, appearances: 6);
        GivenAStriker("Zé do Göl", _top.Id, goals: 8, appearances: 6);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(cup.Id);

        var first = decimal.Round(PrizeRules.CupChampionPrize * 0.10m, 2);
        var second = decimal.Round(PrizeRules.CupChampionPrize * 0.05m, 2);
        Assert.Equal(_bottom.Id, list.Winners[0].TeamId);
        Assert.Equal(first, list.Winners[0].Amount);
        Assert.Equal(_top.Id, list.Winners[1].TeamId);
        Assert.Equal(second, list.Winners[1].Amount);
    }

    [Fact]
    public async Task AClubThatWinsTheCupWithItsOwnTopScorerIsPaidBothPrizes()
    {
        // The money is new and comes out of nobody's share, so a club that won the title and
        // has the artilheiro on the sheet takes five million and a tenth of it as well.
        GivenADivision(1, _top);
        var cup = GivenTheCup();
        GivenAStriker("Astronauta", _top.Id, goals: 9, appearances: 6);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(cup.Id);

        Assert.Equal(PrizeRules.CupChampionPrize, list.BaseAmount);
        Assert.Equal(
            decimal.Round(PrizeRules.CupChampionPrize * 0.10m, 2),
            list.Winners[0].Amount);
    }

    [Fact]
    public async Task AClubCanTakeTwoPrizesInTheSameArtilharia()
    {
        var division = GivenADivision(1, _top);
        GivenAStriker("Dupla do Méier", _top.Id, goals: 18);
        GivenAStriker("Irmão do Dupla", _top.Id, goals: 14);
        GivenAStriker("Terceiro", _middle.Id, goals: 9);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(division.Id);

        Assert.Equal(3, list.Winners.Count);
        Assert.Equal(_top.Id, list.Winners[0].TeamId);
        Assert.Equal(_top.Id, list.Winners[1].TeamId);
        Assert.All(list.Winners.Where(winner => winner.TeamId == _top.Id), winner =>
            Assert.True(winner.Amount > 0));
    }

    [Fact]
    public async Task ALevelPairIsBothSecondBothPaidTheSecondPrizeAndTheThirdIsPaidToNobody()
    {
        var division = GivenADivision(1, _top);
        GivenAStriker("Um", _top.Id, goals: 18);
        GivenAStriker("Dois", _middle.Id, goals: 12, yellow: 3);
        GivenAStriker("Três", _bottom.Id, goals: 12, red: 1);
        GivenAStriker("Quatro", _top.Id, goals: 5);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(division.Id);

        // Three yellows and one red weigh the same: a yellow is one point and a red three. The
        // two men are level on the whole chain, so they are both second and both take the second
        // prize, and the third prize is then paid to nobody — the pair have taken the place
        // below them as well as their own.
        var champion = Assert.Single(list.Winners.Where(winner => winner.PrizeSlot == 1));
        var level = list.Winners.Where(winner => winner.PrizeSlot == 2).ToList();

        Assert.Equal(3, list.Winners.Count);
        Assert.Equal(18, champion.Goals);
        Assert.Equal(2, level.Count);
        Assert.All(level, winner =>
        {
            Assert.Equal(2, winner.Position);
            Assert.Equal(1, winner.TiedWith);
            Assert.Equal(
                PrizeRules.TopScorerPrize(2, ClubsPerDivision, PrizeRules.PurseForTier(1)),
                winner.Amount);
        });
        Assert.DoesNotContain(list.Winners, winner => winner.PrizeSlot == 3);
    }

    [Fact]
    public async Task ACupScorerOfAClubOutsideThePyramidIsStillPaidTheCupsPrize()
    {
        // The cup's purse is the cup's, so there is no division to be in and no division that
        // can take the prize away: a man who scored in a knockout does not need a league table
        // to be paid for it.
        GivenADivision(1, _top);
        var cup = GivenTheCup();
        var homeless = Team.Create("Sem Divisão", "SD", "#333333", "#999999", 55);
        GivenAStriker("Sem Clube", homeless.Id, goals: 8, appearances: 5);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(cup.Id);

        var winner = Assert.Single(list.Winners);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.10m, 2), winner.Amount);
    }

    [Fact]
    public async Task ACompetitionNobodyHasScoredInStillSaysWhatAPrizeIsWorth()
    {
        var division = GivenADivision(2, _middle);
        GivenTheSeasonHasBeenPlayed();

        var list = await Service().GetAsync(division.Id);

        Assert.Empty(list.Winners);
        Assert.Equal(3, list.Rates.Count);
        Assert.Equal(0.10m, list.Rates[0].Rate);
        Assert.Equal(0.05m, list.Rates[1].Rate);
        Assert.Equal(0.03m, list.Rates[2].Rate);
        Assert.Equal(
            PrizeRules.ChampionshipPrize(1, ClubsPerDivision, PrizeRules.PurseForTier(2)),
            list.BaseAmount);
    }

    [Fact]
    public async Task PayingADivisionsArtilhariaWritesOneChequePerPlaceToTheClubTheGoalsWereScoredFor()
    {
        var division = GivenADivision(1, _top);
        var best = GivenAStriker("Zé do Göl", _top.Id, goals: 18);
        var second = GivenAStriker("Bilu", _top.Id, goals: 14);
        var third = GivenAStriker("Careca", _top.Id, goals: 9);
        GivenTheSeasonHasBeenPlayed();
        GivenTheBookRefusesAPrizeItAlreadyHolds();

        var paid = await Service().PayAsync(division.Id);

        var champion = PrizeRules.ChampionshipPrize(1, ClubsPerDivision, PrizeRules.PurseForTier(1));
        Assert.Equal(3, paid);
        Assert.Equal(3, _theBook.Count);
        Assert.All(_theBook, line => Assert.Equal(_top.Id, line.TeamId));
        Assert.All(_theBook, line => Assert.Equal(FinanceMovementKind.PrizeMoney, line.Kind));
        Assert.All(_theBook, line => Assert.Equal(_seasonId, line.SeasonId));
        Assert.Equal(decimal.Round(champion * 0.10m, 2), _theBook[0].Amount);
        Assert.Equal(decimal.Round(champion * 0.05m, 2), _theBook[1].Amount);
        Assert.Equal(decimal.Round(champion * 0.03m, 2), _theBook[2].Amount);
        Assert.Equal(
            new[] { $"artilharia:{division.Id}:{best.Id}", $"artilharia:{division.Id}:{second.Id}", $"artilharia:{division.Id}:{third.Id}" },
            _theBook.Select(line => line.Reference));
    }

    [Fact]
    public async Task PayingTheSameArtilhariaTwicePaysItOnce()
    {
        // A window is closed twice in a season's life: once when its last match ends, and once
        // by a process that was down over the weekend and comes back to a calendar that
        // disagrees with the fixtures under it. A striker is paid his season's prize once.
        var division = GivenADivision(1, _top);
        GivenAStriker("Zé do Göl", _top.Id, goals: 18);
        GivenAStriker("Bilu", _top.Id, goals: 14);
        GivenAStriker("Careca", _top.Id, goals: 9);
        GivenTheSeasonHasBeenPlayed();
        GivenTheBookRefusesAPrizeItAlreadyHolds();

        var service = Service();
        var first = await service.PayAsync(division.Id);
        var second = await service.PayAsync(division.Id);

        Assert.Equal(3, first);
        Assert.Equal(0, second);
        Assert.Equal(3, _theBook.Count);
    }

    [Fact]
    public async Task ACupPaysItsThreePlacesOutOfTheCupsOwnTitle()
    {
        GivenADivision(1, _top);
        GivenADivision(2, _middle);
        GivenADivision(3, _bottom);
        var cup = GivenTheCup();
        GivenAStriker("Astronauta", _top.Id, goals: 7);
        GivenAStriker("Meia-Noite", _middle.Id, goals: 6);
        GivenAStriker("Pedrinho", _bottom.Id, goals: 5);
        GivenTheSeasonHasBeenPlayed();
        GivenTheBookRefusesAPrizeItAlreadyHolds();

        var paid = await Service().PayAsync(cup.Id);

        // The three clubs are in three different divisions and it makes no difference: the cup
        // has a title of its own, and the three places are shares of that one cheque.
        Assert.Equal(3, paid);
        Assert.Equal(_top.Id, _theBook[0].TeamId);
        Assert.Equal(_middle.Id, _theBook[1].TeamId);
        Assert.Equal(_bottom.Id, _theBook[2].TeamId);
        Assert.All(_theBook, line => Assert.Equal(FinanceMovementKind.PrizeMoney, line.Kind));
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.10m, 2), _theBook[0].Amount);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.05m, 2), _theBook[1].Amount);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.03m, 2), _theBook[2].Amount);
    }

    [Fact]
    public async Task AClubThatWonTheCupAndHasItsTopScorerIsPaidTheTitleAndTheArtilharia()
    {
        // Two cheques, not one: winning the cup and scoring in it are two things the club did,
        // and the artilharia is new money rather than a slice of the five million.
        GivenADivision(1, _top);
        var cup = GivenTheCup();
        GivenAStriker("Astronauta", _top.Id, goals: 9);
        GivenTheSeasonHasBeenPlayed();
        GivenTheBookRefusesAPrizeItAlreadyHolds();

        var paid = await Service().PayAsync(cup.Id);

        Assert.Equal(1, paid);
        var line = Assert.Single(_theBook);
        Assert.Equal(_top.Id, line.TeamId);
        Assert.Equal(decimal.Round(PrizeRules.CupChampionPrize * 0.10m, 2), line.Amount);
    }

    [Fact]
    public async Task AnEditionNobodyHasScoredInIsPaidNothing()
    {
        var division = GivenADivision(1, _top);
        GivenTheSeasonHasBeenPlayed();
        GivenTheBookRefusesAPrizeItAlreadyHolds();

        var paid = await Service().PayAsync(division.Id);

        Assert.Equal(0, paid);
        Assert.Empty(_theBook);
    }

    /// <summary>One division of the pyramid, with the twelve clubs of it enrolled.</summary>
    private CompetitionSeasonView GivenADivision(int tier, Team oneOf)
    {
        var view = new CompetitionSeasonView
        {
            Id = Guid.NewGuid(),
            CompetitionId = _championshipId,
            SeasonId = _seasonId,
            DivisionId = Guid.NewGuid(),
            Tier = tier,
            CompetitionName = "Campeonato Brasileiro",
            Type = CompetitionType.League
        };

        var clubs = new List<Team> { oneOf };
        clubs.AddRange(Enumerable.Range(1, ClubsPerDivision - 1).Select(index =>
            Team.Create($"Clube {tier}-{index}", $"C{index}", "#224466", "#cccccc", 65)));

        _theEditions.Add(view);
        _competitions.Setup(repository => repository.ListSeasonViewsAsync(
                _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_theEditions);
        _competitions.Setup(repository => repository.GetSeasonViewByIdAsync(
                view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        _competitions.Setup(repository => repository.ListParticipantsAsync(
                view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clubs.Select(club => CompetitionParticipant.Create(view.Id, club.Id)).ToList());

        return view;
    }

    private CompetitionSeasonView GivenTheCup()
    {
        var view = new CompetitionSeasonView
        {
            Id = Guid.NewGuid(),
            CompetitionId = _cupId,
            SeasonId = _seasonId,
            CompetitionName = "Copa do Brasil",
            Type = CompetitionType.Cup
        };

        _theEditions.Add(view);
        _competitions.Setup(repository => repository.ListSeasonViewsAsync(
                _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_theEditions);
        _competitions.Setup(repository => repository.GetSeasonViewByIdAsync(
                view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        _competitions.Setup(repository => repository.ListParticipantsAsync(
                view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompetitionParticipant>());

        return view;
    }

    private Player GivenAStriker(
        string name,
        Guid teamId,
        int goals,
        int appearances = 20,
        int yellow = 0,
        int red = 0)
    {
        var player = Player.Create(
            name,
            new DateOnly(1994, 5, 20),
            Position.ATT,
            speed: 14,
            accuracy: 14,
            dribbling: 14,
            heading: 12,
            strength: 12,
            goalkeeperPower: 0,
            reflexes: 6);

        _theSquads.Add(player);
        _theGoals.Add(new ClubScorerLine
        {
            PlayerId = player.Id,
            TeamId = teamId,
            Goals = goals,
            Started = appearances,
            CameOn = 0,
            YellowCards = yellow,
            RedCards = red
        });

        return player;
    }

    private void GivenTheSeasonHasBeenPlayed()
    {
        _players.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_theSquads);
        _players.Setup(repository => repository.ListEditionScorerLinesAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_theGoals);
        _teams.Setup(repository => repository.ListByIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { _top, _middle, _bottom });
    }
}
