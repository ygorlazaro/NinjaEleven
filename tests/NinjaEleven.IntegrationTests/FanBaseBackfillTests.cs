using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A world that was created before crowds existed, put into crowds, against the database that
/// will actually refuse a second one.
///
/// <para>
/// The gap-fill has a job only a real career can present: sixty-four clubs, four divisions and
/// three finished seasons, none of which ever carried a supporter. An in-memory provider has
/// no <c>team_fan_bases</c> table and would answer "yes" to a duplicate, so this runs against
/// PostgreSQL — the unique index on <c>(team_id, season_id)</c> is the assertion that the pass
/// respects the one-crowd-per-club-per-season rule the domain already assumes.
/// </para>
///
/// <para>
/// The second half is the reason this test exists at all. The seeder runs on every startup, and
/// a startup that adds sixty-four rows every time is a startup that grows the world by a season
/// of crowds on every boot. So the second run is the interesting one.
/// </para>
/// </summary>
[Collection("Sequential")]
public class FanBaseBackfillTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"crowds_test_{Guid.NewGuid():N}";
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
    /// Every club of the current season ends up with a crowd, and exactly one.
    ///
    /// <para>
    /// The gap is the world as it was: clubs, divisions, participants and squads all present,
    /// and not one row of <c>team_fan_bases</c>. A club left without a crowd is a club whose
    /// every future attendance would have nothing to read, and that is a silent failure — the
    /// season would still be played, the table would still be settled, and the club would simply
    /// always be empty.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AWorldWithNoCrowdsIsGivenOnePerClub()
    {
        await GivenAPyramidOf(2, clubsPerDivision: 4);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var clubs = await check.Teams.CountAsync();
        var crowds = await check.TeamFanBases.CountAsync();

        Assert.Equal(8, clubs);
        Assert.Equal(clubs, crowds);

        // The unique index is the database's job and this is what it is protecting, and it is
        // the grouping that has to happen here: a provider cannot answer "which pair of columns
        // is duplicated" on the server, so the rows are read once and parted in memory.
        var all = await check.TeamFanBases
            .Select(fanBase => new { fanBase.TeamId, fanBase.SeasonId })
            .ToListAsync();

        var duplicates = all
            .GroupBy(row => (row.TeamId, row.SeasonId))
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// The second startup is the one that would break it.
    ///
    /// A pass that writes unconditionally would insert eight more rows and the unique index
    /// would take the API's startup down; a pass that upserts would quietly re-seed the clubs
    /// that already had a crowd, and a manager who had already watched his club's following
    /// move would find it moved back to opening day.
    /// </summary>
    [Fact]
    public async Task RunningTheSeederAgainDoesNotHandTheWorldASecondCrowd()
    {
        await GivenAPyramidOf(2, clubsPerDivision: 4);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var first = AContext();
        var before = await first.TeamFanBases
            .AsNoTracking()
            .OrderBy(fanBase => fanBase.TeamId)
            .Select(fanBase => new { fanBase.TeamId, fanBase.Supporters })
            .ToListAsync();

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var second = AContext();

        Assert.Equal(before.Count, await second.TeamFanBases.CountAsync());
        Assert.Equal(
            before,
            await second.TeamFanBases
                .AsNoTracking()
                .OrderBy(fanBase => fanBase.TeamId)
                .Select(fanBase => new { fanBase.TeamId, fanBase.Supporters })
                .ToListAsync());
    }

    /// <summary>
    /// The pyramid is told apart by the crowd, so two clubs in different divisions must never
    /// end up with the same following.
    ///
    /// <para>
    /// This is the assertion the ladder exists for. A gap-fill that ranked all sixty-four clubs
    /// together would still give every club a plausible-looking number, still satisfy the unique
    /// index, and still leave a third-division club with a crowd the size of a first-division
    /// one — which is the same crowd for a club four times weaker, and therefore the same
    /// stadium, the same income and the same atmosphere for two clubs that should not be alike.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheCrowdSitsOnTheDivisionsOwnLadder()
    {
        await GivenAPyramidOf(2, clubsPerDivision: 4);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var rows = await (
            from fanBase in check.TeamFanBases.AsNoTracking()
            join edition in check.CompetitionSeasons.AsNoTracking() on fanBase.SeasonId equals edition.SeasonId
            join division in check.Divisions.AsNoTracking() on edition.DivisionId equals division.Id
            join participant in check.CompetitionParticipants.AsNoTracking() on edition.Id equals participant.CompetitionSeasonId
            where participant.TeamId == fanBase.TeamId
            select new { fanBase.TeamId, fanBase.Supporters, division.Tier })
            .ToListAsync();

        Assert.Equal(8, rows.Count);

        foreach (var tier in CompetitionRules.Tiers())
        {
            var (_, floor, ceiling) = FanBaseRules.LadderFor(tier);

            foreach (var row in rows.Where(row => row.Tier == tier))
            {
                Assert.InRange(row.Supporters, floor, ceiling);
            }
        }

        // And the ladders really are stacked. The two ends touch — the first division's floor is
        // the second's ceiling — so the weakest first-division club and the strongest
        // second-division club are deliberately the same size: a club that has just been
        // promoted is a club the first division cannot tell apart from one that has just been
        // relegated, which is what makes the next season's movement cost something.
        var lowestOfTheTop = rows.Where(row => row.Tier == 1).Min(row => row.Supporters);
        var highestOfTheRest = rows.Where(row => row.Tier > 1).Max(row => row.Supporters);

        Assert.True(
            lowestOfTheTop >= highestOfTheRest,
            $"A second-division club was seeded {highestOfTheRest:N0} against a first-division club's {lowestOfTheTop:N0}.");

        Assert.Equal(FanBaseRules.LadderFor(2).Ceiling, FanBaseRules.LadderFor(1).Floor);

        // And a division's best is comfortably above its own worst, so the ladder within a
        // division is a ladder too. Only over the divisions this world actually has.
        foreach (var tier in rows.Select(row => row.Tier).Distinct())
        {
            var inThisTier = rows.Where(row => row.Tier == tier).ToList();

            Assert.True(
                inThisTier.Max(row => row.Supporters) > inThisTier.Min(row => row.Supporters),
                $"Tier {tier} seeded every club the same size.");
        }
    }

    /// <summary>
    /// Inside a division, the better squad is given the bigger crowd.
    ///
    /// This is the part that has to read the world rather than the club list: a gap-fill that
    /// seeded by the order clubs came out of the database would hand the biggest crowd of the
    /// fourth division to whichever club happened to be first.
    /// </summary>
    [Fact]
    public async Task TheBetterSquadGetsTheBiggerCrowdWithinItsOwnDivision()
    {
        await GivenAPyramidOf(1, clubsPerDivision: 4);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var rows = await (
            from fanBase in check.TeamFanBases.AsNoTracking()
            join membership in check.TeamMemberships.AsNoTracking()
                on fanBase.TeamId equals membership.TeamId
            where membership.EndDate == null
            select new { fanBase.TeamId, fanBase.Supporters, playerId = membership.PlayerId })
            .ToListAsync();

        var players = await check.Players.AsNoTracking().ToDictionaryAsync(player => player.Id);

        var byClub = rows
            .GroupBy(row => row.TeamId)
            .Select(group => (
                TeamId: group.Key,
                Supporters: group.First().Supporters,
                Stars: PlayerRating.CalculateTeamStars(group.Select(row => players[row.playerId]).ToList())))
            .OrderByDescending(club => club.Stars)
            .ThenByDescending(club => club.Supporters)
            .ToList();

        var best = byClub.First();
        var worst = byClub.Last();

        Assert.True(
            best.Supporters > worst.Supporters,
            $"The strongest club was seeded {best.Supporters:N0} and the weakest {worst.Supporters:N0}.");
    }

    /// <summary>
    /// The crowd is opened where the season opened it, so a club's page can print the season's
    /// change without inventing an opening number on the day it draws the row.
    /// </summary>
    [Fact]
    public async Task ASeededCrowdIsOpenedAtTheNumberTheSeasonStartsOn()
    {
        await GivenAPyramidOf(1, clubsPerDivision: 2);

        await using (var context = AContext())
        {
            await ASeeder(context).SeedAsync();
        }

        await using var check = AContext();

        var rows = await check.TeamFanBases.AsNoTracking().ToListAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal(row.Supporters, row.OpeningSupporters);
            Assert.Equal(row.Supporters, row.PeakSupporters);
            Assert.False(row.Grew);
            Assert.Equal(0, row.Change);
        });
    }

    /// <summary>
    /// Builds the world the way a career that predates crowds looks: a season, divisions, the
    /// competition editions that put a club in one of them, and squads whose quality is
    /// deliberately different per club so that ranking them means something.
    ///
    /// <para>
    /// It is written by hand rather than by running the seeder twice with a migration in between,
    /// because the state being reproduced is precisely one the seeder can no longer create.
    /// </para>
    /// </summary>
    private async Task GivenAPyramidOf(int divisions, int clubsPerDivision)
    {
        await using var context = AContext();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var season = Season.Create(1, today, today.AddDays(CompetitionRules.SeasonMatchDays - 1));
        season.Start();
        context.Seasons.Add(season);

        var championship = Competition.Create("Campeonato Brasileiro", CompetitionType.League);
        context.Competitions.Add(championship);

        var tiers = CompetitionRules.Tiers().Take(divisions).ToList();

        var divisionOfTier = new Dictionary<int, Division>();

        foreach (var tier in tiers)
        {
            var division = Division.Create(null, tier);
            context.Divisions.Add(division);
            divisionOfTier[tier] = division;

            var edition = CompetitionSeason.Create(championship.Id, season.Id, division.Id);
            context.CompetitionSeasons.Add(edition);

            // The squad quality is the tier's own, stepped by the club's place inside it, so the
            // ordering the seeder has to work out is the ordering the clubs actually have.
            for (var seat = 0; seat < clubsPerDivision; seat++)
            {
                var team = Team.Create(
                    $"Clube T{tier}-{seat}", $"T{tier}{seat}", "#101820", "#38d39f", 50 + tier);
                context.Teams.Add(team);

                var stadium = Stadium.Create(team.Id, $"Arena T{tier}-{seat}");
                context.Stadiums.Add(stadium);
                team.SetStadium(stadium);

                context.CompetitionParticipants.Add(
                    CompetitionParticipant.Create(edition.Id, team.Id));

                var quality = 92 - (seat * 4) - ((tier - 1) * 6);

                for (var slot = 0; slot < 20; slot++)
                {
                    var isKeeper = slot == 0;

                    Position position = isKeeper ? Position.GK : APositionFor(slot);

                    var man = Player.Create(
                        isKeeper ? $"Goleiro T{tier}-{seat}" : $"Jogador T{tier}-{seat}-{slot}",
                        25,
                        position,
                        quality, quality, quality, quality, quality,
                        isKeeper ? quality : 0,
                        isKeeper ? quality : 0);

                    context.Players.Add(man);
                    context.TeamMemberships.Add(
                        TeamMembership.Create(man.Id, team.Id, season.StartDate));
                }
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// A squad spread over the three outfield lines, so the strength a club is measured by is
    /// not the strength of one position repeated twenty times.
    /// </summary>
    private static Position APositionFor(int slot)
    {
        return (slot % 3) switch
        {
            1 => Position.DEF,
            2 => Position.MID,
            _ => Position.ATT,
        };
    }
}