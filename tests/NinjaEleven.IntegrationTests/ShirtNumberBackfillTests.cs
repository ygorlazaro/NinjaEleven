using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A world that was created before shirts existed, put into shirts, against the database
/// that will actually refuse it.
///
/// <para>
/// The column is nullable precisely so this pass has something to do, and a pass that hands
/// out a number twice does not merely look wrong: the unique index on
/// <c>(team_id, shirt_number)</c> refuses the save, and a save refused inside the seeder
/// takes the API's startup with it. An in-memory provider has no such index and would answer
/// "yes" to every duplicate, so this runs against PostgreSQL.
/// </para>
///
/// <para>
/// It is also the only test that can reproduce the state a manager's world is actually in:
/// sixty-four clubs whose contracts are all wearing nothing, some of which have since signed
/// men who were dealt a number. A pass that started each club from an empty set would number
/// the shirtless men into the numbers the new ones are already wearing.
/// </para>
/// </summary>
[Collection("Sequential")]
public class ShirtNumberBackfillTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"shirts_test_{Guid.NewGuid():N}";
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

    private static DatabaseSeeder ASeeder(NinjaElevenDbContext context) => new(
        context,
        Options.Create(new DatabaseSeedOptions()),
        NullLogger<DatabaseSeeder>.Instance);

    /// <summary>
    /// The situation this exists for: every contract of every club is wearing nothing, which
    /// is what a world seeded before the column existed looks like to the seeder.
    /// </summary>
    [Fact]
    public async Task AWorldWithNoNumbersIsPutIntoShirts()
    {
        await GivenAWorldWith(2, keepersPerClub: 3);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var live = await check.TeamMemberships
            .Where(membership => membership.EndDate == null)
            .ToListAsync();

        Assert.NotEmpty(live);
        Assert.All(live, membership => Assert.True(membership.HasShirtNumber));

        // Uniqueness is the database's job and it is enforced by a partial index that only
        // sees live contracts, so this is the assertion that the pass respects the index.
        var clashes = live
            .GroupBy(membership => (membership.TeamId, membership.ShirtNumber!.Value))
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(clashes);
    }

    /// <summary>
    /// The standard the world is numbered to: a keeper wears one, twelve or twenty-three,
    /// and every outfield man is given the lowest free number from two upwards.
    ///
    /// It is what makes a backfilled world and a freshly seeded one agree, and it is what a
    /// manager recognises: a keeper in a striker's shirt is not a numbering that happened to
    /// come out differently, it is a numbering that is wrong.
    /// </summary>
    [Fact]
    public async Task KeepersAreGivenKeeperNumbersAndOutfieldTheLowestFreeFromTwo()
    {
        await GivenAWorldWith(2, keepersPerClub: 3);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var numbers = await check.TeamMemberships
            .Where(membership => membership.EndDate == null)
            .Select(membership => new { membership.TeamId, membership.PlayerId, membership.ShirtNumber })
            .ToListAsync();

        var positions = await check.Players
            .Where(player => numbers.Select(n => n.PlayerId).Contains(player.Id))
            .ToDictionaryAsync(player => player.Id, player => player.Position);

        foreach (var club in numbers.GroupBy(number => number.TeamId))
        {
            var keepers = club
                .Where(number => positions[number.PlayerId] == Position.GK)
                .Select(number => number.ShirtNumber!.Value)
                .OrderBy(number => number)
                .ToList();

            Assert.Equal(
                ShirtNumberRules.GoalkeeperNumbers.Take(keepers.Count).OrderBy(n => n).ToList(),
                keepers);

            // Every outfield number is two or more: a number one belongs to a keeper whenever
            // there is a keeper to give it to, and it waits for one when there is not.
            Assert.All(
                club.Where(number => positions[number.PlayerId] != Position.GK),
                number => Assert.True(number.ShirtNumber >= ShirtNumberRules.LowestOutfield));
        }
    }

    /// <summary>
    /// The case that broke it: a world where one man has already been given a number and the
    /// rest have not. A pass that started the club from an empty set would deal his number to
    /// somebody else, and the unique index would refuse the whole save — taking the API's
    /// startup down rather than leaving one row wrong.
    /// </summary>
    [Fact]
    public async Task AMenWhoAlreadyHasANumberIsNotHandedToSomebodyElse()
    {
        await GivenAWorldWith(1, keepersPerClub: 2);

        var (teamId, takenBy) = await GivenAContractThatAlreadyHasANumber(2);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var live = await check.TeamMemberships
            .Where(membership => membership.EndDate == null && membership.TeamId == teamId)
            .ToListAsync();

        Assert.All(live, membership => Assert.True(membership.HasShirtNumber));

        var others = live.Where(membership => membership.PlayerId != takenBy).ToList();
        Assert.DoesNotContain(others, membership => membership.ShirtNumber == 2);
    }

    /// <summary>
    /// An ended contract is history: it keeps whatever it had, including nothing, because
    /// what a man wore for a club he has left is not rewritten by a newer season arriving.
    /// </summary>
    [Fact]
    public async Task AnEndedContractIsNotPutIntoAShirt()
    {
        await GivenAWorldWith(1, keepersPerClub: 1);

        // The contract that leaves is one that was never given a shirt, because that is the
        // only case where "leave it alone" is visible: an ended contract that already has a
        // number is untestable on this point, since there is nothing for the pass to add.
        var teamId = default(Guid);

        await using (var context = AContext())
        {
            teamId = await context.Teams.Select(team => team.Id).FirstAsync();

            var leaving = await context.TeamMemberships
                .FirstAsync(membership => membership.TeamId == teamId);
            leaving.End(new DateOnly(2026, 12, 31));

            await context.SaveChangesAsync();
        }

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var shirtlessEnded = await check.TeamMemberships
            .AnyAsync(membership => membership.TeamId == teamId && membership.EndDate != null
                && membership.ShirtNumber == null);

        Assert.True(shirtlessEnded);
    }

    /// <summary>The pass is a repair, and a repair that ran twice must change nothing.</summary>
    [Fact]
    public async Task RunningThePassTwiceChangesNothing()
    {
        await GivenAWorldWith(2, keepersPerClub: 3);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        List<int> before;
        await using (var context = AContext())
        {
            before = await NumbersOf(context);
        }

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();
        Assert.Equal(before, await NumbersOf(check));
    }

    /// <summary>
    /// Every live contract's number, in a fixed order, so two runs can be compared.
    ///
    /// It is the number and not the id that is compared: the id never changes, so asking it
    /// would be asking whether the same rows exist rather than whether they were renumbered.
    /// </summary>
    private static async Task<List<int>> NumbersOf(NinjaElevenDbContext context) =>
        await context.TeamMemberships
            .Where(membership => membership.EndDate == null)
            .OrderBy(membership => membership.Id)
            .Select(membership => membership.ShirtNumber.Value)
            .ToListAsync();

    /// <summary>
    /// A world of clubs and men, and — the point of it — not one shirt between them.
    /// </summary>
    private async Task GivenAWorldWith(int clubs, int keepersPerClub)
    {
        await using var context = AContext();

        for (var club = 0; club < clubs; club++)
        {
            var team = Team.Create($"Clube {club}", $"C{club}", "#101820", "#38d39f", 70);
            context.Teams.Add(team);

            for (var keeper = 0; keeper < keepersPerClub; keeper++)
            {
                context.TeamMemberships.Add(GivenAKeeper(context, team.Id, keeper));
            }

            // More outfield men than there are numbers below the keepers, so the standard has
            // to skip 12 and 23 rather than hand them to a striker.
            for (var outfield = 0; outfield < 20; outfield++)
            {
                context.TeamMemberships.Add(AnOutfielder(context, team.Id, outfield));
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task<(Guid TeamId, Guid PlayerId)> GivenAContractThatAlreadyHasANumber(int number)
    {
        await using var context = AContext();

        var team = Team.Create("Clube Numerado", "CN", "#101820", "#38d39f", 70);
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var membership = AnOutfielder(context, team.Id, 0, number);
        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        return (team.Id, membership.PlayerId);
    }

    private static TeamMembership GivenAKeeper(
        NinjaElevenDbContext context,
        Guid teamId,
        int index)
    {
        var player = Player.Create(
            $"Goleiro {index}", 27, Position.GK, 12, 10, 9, 9, 14, 78, 74);

        context.Players.Add(player);

        return TeamMembership.Create(player.Id, teamId, new DateOnly(2026, 1, 1));
    }

    private static TeamMembership AnOutfielder(
        NinjaElevenDbContext context,
        Guid teamId,
        int index,
        int? shirtNumber = null)
    {
        var player = Player.Create(
            $"Jogador {index}", 25, Position.ATT, 15, 15, 14, 13, 13, 0, 7);

        context.Players.Add(player);

        return TeamMembership.Create(player.Id, teamId, new DateOnly(2026, 1, 1), shirtNumber: shirtNumber);
    }
}
