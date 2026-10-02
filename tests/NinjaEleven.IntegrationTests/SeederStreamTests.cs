using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Transfers;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// The seeder's private streams, and the rule they exist for.
///
/// <para>
/// A seeded world is reproducible only while the list of things drawn from its one stream is
/// frozen, and it was never frozen: every attribute added to a player added a draw. The draws
/// that are not the world's own — stamina, potential — therefore come from streams of their
/// own, and this file is the guard on that, because the failure it prevents is invisible from
/// the code and only shows up as a rule quietly broken somewhere else.
/// </para>
///
/// <para>
/// The first symptom was a season's intake arriving with twenty-seven per cent of its men in
/// goal against a fifteen per cent rule. Nothing in the rule had changed, no keeper roll had
/// been touched, and the assertion that caught it was a test about young free agents. That is
/// the shape of this bug: it is never reported by the thing that caused it.
/// </para>
/// </summary>
public class SeederStreamTests
{
    private const int Seed = 2026;

    [Fact]
    public async Task ASquadIsDrawnTheSameWayWhateverElseIsDrawnFromIt()
    {
        // The two draws are the world's own, and they come out of the shared stream. If any
        // other draw were taken from it between the position and the attributes, the squad
        // would move and the rest of the world would move with it.
        var first = await BuildAWorldAsync();
        var second = await BuildAWorldAsync();

        Assert.Equal(
            first.Select(player => $"{player.Position}:{player.Speed}:{player.Accuracy}:{player.Stamina}:{player.Potential}"),
            second.Select(player => $"{player.Position}:{player.Speed}:{player.Accuracy}:{player.Stamina}:{player.Potential}"));
    }

    [Fact]
    public async Task TheWorldAlreadySeededIsTheSameOneEveryTime()
    {
        // Reproducibility is the property the private streams buy, and it has to survive a
        // second run over the same seed rather than holding only within one process. The
        // identities are not compared: an id is a Guid the domain mints, not a draw from the
        // seed, and a seeded world is the same football rather than the same rows.
        var first = await BuildAWorldAsync();
        var second = await BuildAWorldAsync();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(
            first.Select(Describe).OrderBy(key => key, StringComparer.Ordinal),
            second.Select(Describe).OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task EveryPlayerIsDrawnWithAPotentialAtOrAboveWhereHeIs()
    {
        // The one value that must never be wrong: a ceiling below a man's own attributes is a
        // man who is frozen, who can never grow, and who can never be trained either — and
        // every one of those failures is silent.
        foreach (var player in await BuildAWorldAsync())
        {
            Assert.InRange(player.Potential, 1, 100);
            Assert.True(
                player.Potential >= DevelopmentRules.Overall(player) - 0.5,
                $"{player.Name} was drawn with a potential of {player.Potential} below his own " +
                $"reading of {DevelopmentRules.Overall(player):F1}.");
        }
    }

    [Fact]
    public async Task TheIntakeIsStillMostlyOutfielders()
    {
        // The assertion that caught the bug the first time, kept here as the standing guard
        // rather than left in the test that happened to trip over it. A change to the seeder
        // that puts a third of a season's intake in goal has broken the intake rule, whatever
        // it was for.
        await using var db = NewWorld(Seed);

        var season = Domain.Seasons.Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        await db.Seasons.AddAsync(season);
        await db.SaveChangesAsync();

        await new DatabaseSeeder(
            db,
            Options.Create(new DatabaseSeedOptions { RandomSeed = Seed }),
            NullLogger<DatabaseSeeder>.Instance).SeedYoungFreeAgentsAsync(
                YouthIntakeRules.FreeAgentsPerSeason, season.Id);

        var states = await db.PlayerSeasonStates
            .Where(state => state.SeasonId == season.Id)
            .Select(state => state.PlayerId)
            .ToListAsync();

        var intake = await db.Players
            .Where(player => states.Contains(player.Id))
            .Select(player => player.Position)
            .ToListAsync();

        Assert.Equal(YouthIntakeRules.FreeAgentsPerSeason, intake.Count);
        Assert.InRange(intake.Count(position => position == Position.GK), 5, 25);
    }

    [Fact]
    public async Task APlayersTankAndHisCeilingAreNotTheSameDraw()
    {
        // They are drawn from two streams and the correlation is not one anybody asked for. A
        // world where a man with a full tank is systematically a man with a full ceiling would
        // be a world where a club picks its prospects by endurance, which is not a scouting
        // policy the game has.
        var players = await BuildAWorldAsync();

        var stamina = players.Select(player => (double)player.Stamina).ToList();
        var potential = players.Select(player => (double)player.Potential).ToList();

        var meanStamina = stamina.Average();
        var meanPotential = potential.Average();

        var covariance = stamina.Zip(potential).Sum(pair => (pair.First - meanStamina) * (pair.Second - meanPotential));
        var spread = Math.Sqrt(stamina.Sum(v => (v - meanStamina) * (v - meanStamina))
            * potential.Sum(v => (v - meanPotential) * (v - meanPotential)));

        var correlation = spread <= 0 ? 0 : covariance / spread;

        Assert.InRange(correlation, -0.35, 0.35);
    }

    // --- Helpers ------------------------------------------------------------------

    private static NinjaElevenDbContext NewWorld(int seed) =>
        new(new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"SeederStream-{Guid.NewGuid()}")
            .Options);

    /// <summary>Everything a draw decides about a player, and nothing a clock does.</summary>
    private static string Describe(Player player) =>
        $"{player.Name}|{player.Age}|{player.Position}|{player.Speed}|{player.Accuracy}|" +
        $"{player.Dribbling}|{player.Heading}|{player.Strength}|{player.GoalkeeperPower}|" +
        $"{player.Reflexes}|{player.Stamina}|{player.Potential}";

    /// <summary>
    /// A world drawn from a fixed seed, which is the only kind this file can say anything
    /// about: an unseeded world is different every time by design.
    /// </summary>
    private static async Task<List<Player>> BuildAWorldAsync()
    {
        await using var db = NewWorld(Seed);

        await new DatabaseSeeder(
            db,
            Options.Create(new DatabaseSeedOptions { RandomSeed = Seed }),
            NullLogger<DatabaseSeeder>.Instance).SeedAsync();

        return await db.Players.OrderBy(player => player.Name).ToListAsync();
    }
}
