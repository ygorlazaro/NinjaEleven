using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Transfers;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// The intake rule: a season opens with a fixed number of young free agents, and they are
/// free agents in the sense the market reads — a season state with no club and no contract,
/// so a club signs one rather than buying him.
///
/// It is the rule a ratio could not express. A ratio needs the season's retirements to divide
/// by and the first day of a season has none, so the market opened its first season with
/// nobody in it at all: every name on it belonged to somebody, and a manager looking for a
/// young man to sign found no such thing.
/// </summary>
public class YouthIntakeTests : IDisposable
{
    private readonly NinjaElevenDbContext _dbContext;
    private readonly DatabaseSeeder _seeder;

    public YouthIntakeTests()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"YouthIntake-{Guid.NewGuid()}")
            .Options;

        _dbContext = new NinjaElevenDbContext(options);
        _seeder = new DatabaseSeeder(
            _dbContext,
            Options.Create(new DatabaseSeedOptions { RandomSeed = 2026 }),
            NullLogger<DatabaseSeeder>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ASeasonOpensWithTheRulesOwnNumberOfYoungFreeAgents()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        await _dbContext.Seasons.AddAsync(season);
        await _dbContext.SaveChangesAsync();

        await _seeder.SeedYoungFreeAgentsAsync(
            YouthIntakeRules.FreeAgentsPerSeason, season.Id);

        var states = await _dbContext.PlayerSeasonStates
            .Where(state => state.SeasonId == season.Id)
            .ToListAsync();

        Assert.Equal(YouthIntakeRules.FreeAgentsPerSeason, states.Count);

        // A young player is somebody a club signs, not somebody a club buys: there is nobody to
        // charge, so every one of them arrives with no club at all.
        Assert.All(states, state => Assert.Null(state.TeamId));

        var players = await _dbContext.Players.ToListAsync();
        var seasonStates = states.Select(state => state.PlayerId).ToHashSet();

        var intake = players.Where(player => seasonStates.Contains(player.Id)).ToList();

        Assert.All(intake, player =>
        {
            // The rule is about the age the player carries on the day the season opens, which
            // is the day a manager meets him: a birth date picked by year alone put fifteen-
            // year-olds into an intake the rule calls sixteen to nineteen.
            Assert.InRange(player.Age, YouthIntakeRules.YoungestAge, YouthIntakeRules.OldestAge);
        });

        // The intake is drawn from the same pool the squads are drawn from, so it is not
        // ninety midfielders: a market of one position is not a market.
        var goalkeepers = intake.Count(player => player.Position == Domain.Enums.Position.GK);
        Assert.InRange(goalkeepers, 5, 25);
    }

    [Fact]
    public async Task TheBackfillDealsTheSameIntakeTheSeasonOpeningDeals()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        await _dbContext.Seasons.AddAsync(season);
        await _dbContext.SaveChangesAsync();

        var created = await _seeder.SeedYoungFreeAgentsAsync(
            YouthIntakeRules.FreeAgentsPerSeason, season.Id);

        Assert.Equal(YouthIntakeRules.FreeAgentsPerSeason, created);

        var free = await _dbContext.PlayerSeasonStates
            .CountAsync(state => state.SeasonId == season.Id && state.TeamId == null);

        Assert.Equal(YouthIntakeRules.FreeAgentsPerSeason, free);
    }
}
