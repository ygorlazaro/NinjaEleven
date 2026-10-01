using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// The artilharia of one division is the artilharia of that division and not of the country.
/// </summary>
/// <remarks>
/// <para>
/// This is the one statistic in the game that is filtered by something other than a kind of
/// competition, and the filter is a join across five tables: a line belongs to a match, a match
/// to a fixture, a fixture to a round, a round to an edition, and only the edition knows which
/// division it is the table of. That chain is where a season's chart quietly becomes a chart of
/// all ninety-six clubs — the query still runs, it just stops meaning anything — so it is
/// exercised against the database that will really hold it. An in-memory provider would
/// translate the same LINQ and hide a chain the server cannot execute at all.
/// </para>
///
/// <para>
/// It matters more than a wrong number on a screen: the three men at the top of this list are
/// paid a share of their own division's title. A chart that mixed the pyramid would pay a
/// fourth-division striker the first division's money.
/// </para>
/// </remarks>
[Collection("Sequential")]
public class ScorerDivisionFilterTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"scorers_test_{Guid.NewGuid():N}";
    private NinjaElevenDbContext? _setup;

    private string ConnectionString => $"{Server};Database={_database}";

    public async Task InitializeAsync()
    {
        _setup = new NinjaElevenDbContext(
            new DbContextOptionsBuilder<NinjaElevenDbContext>().UseNpgsql(Server).Options);

        await _setup.Database.ExecuteSqlRawAsync($"CREATE DATABASE {_database}");

        await using var created = AContext();
        await created.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_setup is null) return;

        await _setup.Database.CloseConnectionAsync();
        await _setup.Database.ExecuteSqlRawAsync($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)");
        await _setup.DisposeAsync();
    }

    private NinjaElevenDbContext AContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>();
        NinjaElevenDbContext.Configure(options, ConnectionString);

        return new NinjaElevenDbContext(options.Options);
    }

    /// <summary>
    /// Two divisions of the same season, each with a man who scored in it — the whole claim in
    /// one shape: two editions, two statistics rows, one question.
    /// </summary>
    [Fact]
    public async Task ADivisionsChartCarriesOnlyThatDivisionsGoals()
    {
        var world = await AWorldWithAStrikerInEachOfTwoDivisions();

        await using var context = AContext();
        var repository = new PlayerRepository(context);

        var firstDivision = await repository.ListSeasonScorerLinesAsync(
            world.SeasonId, CompetitionType.League, world.FirstDivisionId);

        var secondDivision = await repository.ListSeasonScorerLinesAsync(
            world.SeasonId, CompetitionType.League, world.SecondDivisionId);

        var wholeCountry = await repository.ListSeasonScorerLinesAsync(
            world.SeasonId, CompetitionType.League);

        // Each division sees its own man, and only its own man.
        Assert.Equal(world.FirstStrikerId, Assert.Single(firstDivision).PlayerId);
        Assert.Equal(world.SecondStrikerId, Assert.Single(secondDivision).PlayerId);

        // And the chart of the country is the two of them together, which is what makes the two
        // filtered answers a filter rather than a coincidence: a repository that ignored the
        // division would return the same two lines from every division, and the first assertion
        // above would be the only one to notice.
        Assert.Equal(2, wholeCountry.Count);
        Assert.Contains(wholeCountry, line => line.PlayerId == world.FirstStrikerId);
        Assert.Contains(wholeCountry, line => line.PlayerId == world.SecondStrikerId);
    }

    private readonly record struct SeededDivision(
        Guid SeasonId,
        Guid FirstDivisionId,
        Guid SecondDivisionId,
        Guid FirstStrikerId,
        Guid SecondStrikerId);

    private async Task<SeededDivision> AWorldWithAStrikerInEachOfTwoDivisions()
    {
        Guid seasonId;
        var divisions = new List<(Guid EditionId, Guid DivisionId, Guid HomeTeamId, Guid AwayTeamId, Guid StrikerId)>();

        await using (var context = AContext())
        {
            await new DatabaseSeeder(
                context,
                Options.Create(new DatabaseSeedOptions()),
                NullLogger<DatabaseSeeder>.Instance).SeedAsync();

            seasonId = await context.Seasons
                .Where(season => season.Status == SeasonStatus.InProgress)
                .Select(season => season.Id)
                .FirstAsync();

            // Two editions with clubs in them, taken by tier so the two divisions are different
            // ones rather than two tables of the same division.
            var editions = await context.CompetitionSeasons
                .Where(edition => edition.SeasonId == seasonId && edition.DivisionId != null)
                .OrderBy(edition => edition.DivisionId)
                .Select(edition => new { edition.Id, edition.DivisionId })
                .ToListAsync();

            Assert.True(editions.Count >= 2, "A seeded world has four divisions; two of them are enough.");

            foreach (var edition in editions.Take(2))
            {
                var teamId = await context.CompetitionParticipants
                    .Where(participant => participant.CompetitionSeasonId == edition.Id)
                    .OrderBy(participant => participant.TeamId)
                    .Select(participant => participant.TeamId)
                    .FirstAsync();

                // A man of that club, from the contracts it actually holds — a statistics row
                // belongs to a player and a club, and reaching past the club's own book would
                // write a line nobody could read back against a squad.
                var strikerId = await context.TeamMemberships
                    .Where(membership => membership.TeamId == teamId && membership.EndDate == null)
                    .OrderBy(membership => membership.PlayerId)
                    .Select(membership => membership.PlayerId)
                    .FirstAsync();

// The clubs of each edition, its home side and its away side: a fixture is two clubs
            // that both exist, and a guid invented here would be refused by the foreign key
            // rather than by the rule this test is about.
            var teams = await context.CompetitionParticipants
                .Where(participant => participant.CompetitionSeasonId == edition.Id)
                .OrderBy(participant => participant.TeamId)
                .Select(participant => participant.TeamId)
                .Take(2)
                .ToListAsync();

            Assert.True(teams.Count == 2, "A division's edition is asked for two of its clubs.");

            divisions.Add((edition.Id, edition.DivisionId!.Value, teams[0], teams[1], strikerId));
        }

        foreach (var division in divisions)
        {
            var round = Round.Create(division.EditionId, number: 1);
            var fixture = Fixture.Create(round.Id, division.HomeTeamId, division.AwayTeamId);
            var match = Match.Create(fixture.Id, division.HomeTeamId, division.AwayTeamId);

            context.Rounds.Add(round);
            context.Fixtures.Add(fixture);
            context.Matches.Add(match);

            // The home club's man scores three: one line per division, and the only thing that
            // tells the two lines apart is the edition their round belongs to.
            var statistics = MatchPlayerStatistics.Create(
                match.Id, division.StrikerId, division.HomeTeamId, seasonId);
            statistics.ApplyFrom(AScoringSnapshot(), started: true);

            context.MatchPlayerStatistics.Add(statistics);
        }

        await context.SaveChangesAsync();
    }

        return new SeededDivision(
            seasonId,
            divisions[0].DivisionId,
            divisions[1].DivisionId,
            divisions[0].StrikerId,
            divisions[1].StrikerId);
    }

    /// <summary>A man who found the net three times, which is the number the assertions read.</summary>
    private static MatchPlayerSnapshot AScoringSnapshot()
    {
        var player = Player.Create(
            "Zé da bola", 26, Position.ATT,
            speed: 78, accuracy: 78, dribbling: 78, heading: 78,
            strength: 78, goalkeeperPower: 0, reflexes: 0, stamina: 78, potential: 85);

        var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), Guid.NewGuid(), energy: 88);
        var snapshot = MatchPlayerSnapshot.FromPlayerSeasonState(player, state);

        snapshot.PlayedInMatch = true;
        snapshot.MatchGoals = 3;

        return snapshot;
    }
}