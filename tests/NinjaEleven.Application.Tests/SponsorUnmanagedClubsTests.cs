using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The pass that puts a shirt on every club nobody is running.
///
/// <para>
/// It exists because sixty-three of the sixty-four clubs in a pyramid are run by nobody, and a
/// world where only the one club a person happens to be in has a sponsor is a world whose
/// scoreboards are bare everywhere else. So what is asserted here is not that a company was
/// chosen but that the choice is the one a manager would have been given: the same shortlist,
/// the same price, the same length, and no re-deal of a club that is already wearing something.
/// </para>
/// </summary>
public class SponsorUnmanagedClubsTests
{
    private readonly Guid _seasonId = Guid.NewGuid();
    private readonly Mock<ISponsorRepository> _sponsors = new(MockBehavior.Loose);
    private readonly Mock<ISponsorContractRepository> _contracts = new(MockBehavior.Loose);
    private readonly Mock<ITeamRepository> _teams = new(MockBehavior.Loose);
    private readonly Mock<ISeasonRepository> _seasons = new(MockBehavior.Loose);
    private readonly Mock<ICompetitionRepository> _competitions = new(MockBehavior.Loose);
    private readonly Mock<IFinanceRepository> _finance = new(MockBehavior.Loose);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Loose);

    private readonly List<SponsorContract> _signed = [];
    private readonly List<Team> _unmanaged = [];
    private readonly Dictionary<Guid, int> _divisions = [];

    private SponsorOfferService CreateService() => new(
        _sponsors.Object,
        _contracts.Object,
        _teams.Object,
        _finance.Object,
        _seasons.Object,
        _competitions.Object,
        SponsorFactsTestFactory.Create(_seasonId, Guid.NewGuid()).Facts,
        InboxTestFactory.Create(_teams),
        _unitOfWork.Object,
        NullLogger<SponsorOfferService>.Instance);

    public SponsorUnmanagedClubsTests()
    {
        _sponsors.Setup(repo => repo.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Sponsor>
            {
                Sponsor.Create("Metalúrgica do Vale", "Metal", "#c0392b"),
                Sponsor.Create("Banco do Litoral", "Financeiro", "#1f6f3f")
            });

        _contracts.Setup(repo => repo.ListActiveByTeamIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _signed
                    .Where(contract => contract.IsActive && ids.Contains(contract.TeamId))
                    .GroupBy(contract => contract.TeamId)
                    .ToDictionary(group => group.Key, group => (SponsorContract)group.First()));

        _contracts.Setup(repo => repo.ListActiveBySponsorIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _signed
                    .Where(contract => contract.IsActive && ids.Contains(contract.SponsorId))
                    .GroupBy(contract => contract.SponsorId)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<SponsorContract>)group.ToList()));

        _contracts.Setup(repo => repo.AddAsync(It.IsAny<SponsorContract>(), It.IsAny<CancellationToken>()))
            .Callback<SponsorContract, CancellationToken>((contract, _) => _signed.Add(contract))
            .Returns(Task.CompletedTask);

        _teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _unmanaged.FirstOrDefault(club => club.Id == id));

        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        _seasons.Setup(repo => repo.GetAsync(_seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(season);

        // The tier of the division a club is in, which is the one fact a price is read from. It
        // is the same view the manager's shortlist asks for, and it is answered here from the
        // same tier table the pass reads, so the two paths cannot disagree about what a club is.
        _competitions.Setup(repo => repo.GetDivisionSeasonForTeamAsync(
                It.IsAny<Guid>(), _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid teamId, Guid season, CancellationToken _) =>
                _divisions.TryGetValue(teamId, out var tier)
                    ? new CompetitionSeasonView
                    {
                        Id = Guid.NewGuid(),
                        CompetitionId = Guid.NewGuid(),
                        SeasonId = season,
                        DivisionId = Guid.NewGuid(),
                        Tier = tier,
                        CompetitionName = CompetitionRules.DivisionName(tier),
                        Type = CompetitionType.League
                    }
                    : null);

        _teams.Setup(repo => repo.ListClubsWithoutManagerInSeasonAsync(
                _seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IReadOnlyList<Team>)_unmanaged);

        _teams.Setup(repo => repo.GetTeamDivisionsAsync(
                _seasonId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid season, IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Where(id => _divisions.ContainsKey(id))
                    .ToDictionary(id => id, id => _divisions[id]));

        _unitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private Team AddUnmanagedClub(string name, int tier)
    {
        var club = Team.Create(name, name[..3].ToUpperInvariant(), "#1a1a1a", "#ffffff");
        _unmanaged.Add(club);
        _divisions[club.Id] = tier;
        return club;
    }

    [Fact]
    public async Task AClubNobodyRunsEndsTheRoundWithAShirtOnIt()
    {
        var club = AddUnmanagedClub("Clube Sem Gente", tier: 1);

        var signed = await CreateService().SignTheUnmanagedClubsAsync(_seasonId);

        Assert.Equal(1, signed);
        var contract = Assert.Single(_signed);
        Assert.Equal(club.Id, contract.TeamId);
    }

    [Fact]
    public async Task AClubAlreadyWearingACompanyIsLeftAlone()
    {
        // The pass fills bare shirts; it does not re-deal a club that chose. A board that reopened
        // every club's contract on every window would be a market nobody could plan a season in.
        var club = AddUnmanagedClub("Clube Com Gente", tier: 1);
        var incumbent = Sponsor.Create("Metalúrgica do Vale", "Metal", "#c0392b");
        _signed.Add(SponsorContract.Sign(
            incumbent.Id, club.Id, _seasonId, 4_000m, 10));

        var signed = await CreateService().SignTheUnmanagedClubsAsync(_seasonId);

        Assert.Equal(0, signed);
        Assert.Single(_signed);
        Assert.Equal(incumbent.Id, _signed[0].SponsorId);
    }

    [Fact]
    public async Task AClubNoDivisionPutsOnNoShirt()
    {
        // A cup's entrant has no division behind it, and there is nothing a company would be
        // quoting for. This is the same absence the manager's own shortlist reports as empty.
        var club = Team.Create("Clube da Copa", "COP", "#1a1a1a", "#ffffff");
        _unmanaged.Add(club);

        var signed = await CreateService().SignTheUnmanagedClubsAsync(_seasonId);

        Assert.Equal(0, signed);
        Assert.Empty(_signed);
    }

    [Fact]
    public async Task AClubWithNoCompanyWillingToTakeItKeepsABareShirtRatherThanAPaidOne()
    {
        // Every rule a company has for refusing a club applies here as it does to a manager's
        // shortlist. A pass that invented a deal for a club nobody wanted would be money out of
        // a club's books for a name it could not sell.
        var club = AddUnmanagedClub("Clube Sem Interesse", tier: 4);

        var signed = await CreateService().SignTheUnmanagedClubsAsync(_seasonId);

        Assert.Equal(0, signed);
        Assert.Empty(_signed);
    }

    [Fact]
    public async Task ACompanyTakesTwoClubsOfOneDivisionAndIsNotHandedTheThird()
    {
        // Two per division, and the ceiling is the whole rule: a brand that will not put a third
        // name in the same championship still holds two, which is what took a third of the
        // pyramid off the market when the rule was one. So the third club of that division has
        // to go to the other company — or to nobody, and a bare shirt is the honest answer.
        var first = AddUnmanagedClub("Clube do Litoral Um", tier: 2);
        var second = AddUnmanagedClub("Clube do Litoral Dois", tier: 2);
        var third = AddUnmanagedClub("Clube do Litoral Tres", tier: 2);

        await CreateService().SignTheUnmanagedClubsAsync(_seasonId);

        Assert.Equal(3, _signed.Count);
        Assert.Equal(
            new[] { first.Id, second.Id, third.Id }.OrderBy(id => id),
            _signed.Select(contract => contract.TeamId).OrderBy(id => id));

        // The ceiling, asked about every company the pass signed rather than about one of them.
        foreach (var sponsorId in _signed.Select(contract => contract.SponsorId).Distinct())
        {
            var inThisDivision = _signed.Count(contract =>
                contract.SponsorId == sponsorId && _divisions[contract.TeamId] == 2);

            Assert.True(
                inThisDivision <= SponsorRules.MaxClubsPerDivision,
                $"{sponsorId} was handed {inThisDivision} clubs of the 2ª Divisão, over the ceiling of {SponsorRules.MaxClubsPerDivision}.");
        }
    }

    [Fact]
    public async Task ACompanyFreeInTheDivisionIsNotPassedOverForOneThatIsFull()
    {
        // The ceiling is per company, not per division. Two clubs already wearing the first brand
        // must not stop the third from being offered the second — that is a rule that would leave
        // a shirt bare because of somebody else's portfolio.
        for (var club = 0; club < 4; club++)
        {
            AddUnmanagedClub($"Clube da Divisão {club}", tier: 2);
        }

        await CreateService().SignTheUnmanagedClubsAsync(_seasonId);

        var perSponsor = _signed
            .GroupBy(contract => contract.SponsorId)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(4, _signed.Count);
        Assert.All(perSponsor.Values, count => Assert.True(count <= SponsorRules.MaxClubsPerDivision));
    }

    [Fact]
    public async Task AClubIsPaidThePriceItsOwnShortlistWouldHaveQuoted()
    {
        // The point of using the same rules rather than a cheaper path for the clubs nobody is
        // watching: a company that would not pay this price for this shirt does not get it for
        // free, and one that would pays exactly what it would have paid a manager. The shortlist
        // is read first because a club that has already signed is, correctly, no longer being
        // offered that company — so the comparison has to be made against the book as it stood
        // when the club was bare.
        var club = AddUnmanagedClub("Clube da Primera", tier: 1);
        var service = CreateService();

        var quoted = await service.GetBookAsync(club.Id, _seasonId);
        await service.SignTheUnmanagedClubsAsync(_seasonId);

        var contract = Assert.Single(_signed);
        var offer = Assert.Single(quoted.Candidates
            .Where(candidate => candidate.SponsorId == contract.SponsorId));

        Assert.Equal(quoted.Candidates[0].SponsorId, contract.SponsorId);
        Assert.Equal(offer.PerMatchFee, contract.PerMatchFee);
        Assert.Equal(offer.ContractMatches, contract.ContractMatches);
    }

    [Fact]
    public async Task ASecondWalkOfTheWorldSignsNothingBecauseEveryShirtIsAlreadySold()
    {
        // Idempotence is what makes it safe to hook the pass to the world's heartbeat: a window
        // closed twice — once by its last match and once by a process that was down over the
        // weekend — has to leave one deal per club and not two.
        AddUnmanagedClub("Clube Estavel", tier: 1);

        var service = CreateService();
        await service.SignTheUnmanagedClubsAsync(_seasonId);
        var second = await service.SignTheUnmanagedClubsAsync(_seasonId);

        Assert.Equal(0, second);
        Assert.Single(_signed);
    }
}