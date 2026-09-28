using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// Verifies the sponsor EF Core queries translate to SQL and the active-deal filter
/// works against a real query pipeline (in-memory provider, not mocked repositories).
/// </summary>
public class SponsorContractRepositoryTests : IDisposable
{
    private readonly NinjaElevenDbContext _dbContext;
    private readonly SponsorContractRepository _repository;

    public SponsorContractRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"SponsorTest-{Guid.NewGuid()}")
            .Options;

        _dbContext = new NinjaElevenDbContext(options);
        _repository = new SponsorContractRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetActiveByTeamAsync_TranslatesQueryAndFiltersByContractMatches()
    {
        // Reproduces the original crash: a query that used the computed MatchesLeft
        // property could not be translated to SQL. The fix inlines ContractMatches >
        // MatchesPlayed, which EF Core can translate.
        var sponsor = Sponsor.Create("TestCorp", "Tech", "#0000FF");
        await _dbContext.Sponsors.AddAsync(sponsor);

        var team = Team.Create("Aurora", "CAU", "#E07B00", "#2B2B2B");
        await _dbContext.Teams.AddAsync(team);

        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await _dbContext.Seasons.AddAsync(season);
        await _dbContext.SaveChangesAsync();

        // A contract that was active but is now paid in full: no matches left.
        var spentDeal = SponsorContract.Sign(sponsor.Id, team.Id, season.Id, 50_000m, 5);
        for (var i = 0; i < 5; i++)
            spentDeal.RecordMatchPlayed();

        // A second contract that is still active with matches left.
        var liveDeal = SponsorContract.Sign(sponsor.Id, team.Id, season.Id, 60_000m, 3);

        await _dbContext.SponsorContracts.AddRangeAsync(spentDeal, liveDeal);
        await _dbContext.SaveChangesAsync();

        var active = await _repository.GetActiveByTeamAsync(team.Id);

        Assert.NotNull(active);
        Assert.Equal(liveDeal.Id, active!.Id);
    }

    [Fact]
    public async Task GetActiveByTeamAsync_ReturnsNullWhenTeamIsFree()
    {
        var team = Team.Create("Free Club", "FREE", "#000", "#FFF");
        await _dbContext.Teams.AddAsync(team);
        await _dbContext.SaveChangesAsync();

        var active = await _repository.GetActiveByTeamAsync(team.Id);

        Assert.Null(active);
    }
}
