using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A club's own page, read out of the database that will actually hold it.
/// </summary>
/// <remarks>
/// <para>
/// The unit tests for this service mock every reader, which is the right way to test the rules
/// and the wrong way to test the queries underneath them. The two readers this page leans on
/// hardest are exactly the two an in-memory provider would answer wrongly: the division walk
/// joins participants to editions to divisions and orders by the season's number, and the
/// scorers walk joins five tables to reach a match line. Neither has a translation an in-memory
/// provider checks, so a projection that does not compile to SQL fails here and passes in
/// every unit test above it.
/// </para>
///
/// <para>
/// It runs against a real PostgreSQL for the same reason the shirt-number backfill does: what
/// is being tested here is that the SQL is translatable and that the joins do not multiply the
/// rows.
/// </para>
/// </remarks>
[Collection("Sequential")]
public class ClubProfileReadTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"club_profile_test_{Guid.NewGuid():N}";
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

    /// <summary>
    /// The project's own configure helper, rather than a builder written here.
    /// </summary>
    /// <remarks>
    /// It is what puts the migrations history table under the name the rest of the world uses.
    /// A context configured any other way looks at a database with no history in it, so every
    /// migration in the project is replayed over a schema that already has one — and EF answers
    /// that with a pending-model-changes failure that has nothing to do with model changes.
    /// </remarks>
    private NinjaElevenDbContext AContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>();
        NinjaElevenDbContext.Configure(options, ConnectionString);

        return new NinjaElevenDbContext(options.Options);
    }

    private ClubProfileService AService(NinjaElevenDbContext db) => new(
        new TeamRepository(db),
        new PlayerRepository(db),
        new SeasonRepository(db),
        new CompetitionRepository(db),
        new TrophyRepository(db),
        new ClubEventRepository(db),
        new FinanceRepository(db));

    /// <summary>
    /// A world of one season, one division, two clubs and a squad, which is the smallest world
    /// a club's page has anything to say at all.
    /// </summary>
    private async Task<(Guid teamId, Guid otherTeamId, Guid seasonId, Guid editionId, Guid divisionId)>
        SeedAWorldAsync(NinjaElevenDbContext db)
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await db.Seasons.AddAsync(season);

        var division = Division.Create("1ª Divisão", 1);
        await db.Divisions.AddAsync(division);

        var competition = Competition.Create("Campeonato Brasileiro", CompetitionType.League);
        await db.Competitions.AddAsync(competition);

        var edition = CompetitionSeason.Create(competition.Id, season.Id, division.Id);
        await db.CompetitionSeasons.AddAsync(edition);

        var club = Team.Create("Náutico", "NAU", "#0a3d62", "#f2d34f");
        var other = Team.Create("Grêmio", "GRE", "#111111", "#eeeeee");
        await db.Teams.AddRangeAsync(club, other);
        await db.CompetitionParticipants.AddRangeAsync(
            CompetitionParticipant.Create(edition.Id, club.Id),
            CompetitionParticipant.Create(edition.Id, other.Id));

        var player = Player.Create("Zé Ramalho", 26, Position.ATT, 15, 15, 14, 13, 13, 1, 1);
        await db.Players.AddAsync(player);
        await db.TeamMemberships.AddAsync(
            TeamMembership.Create(player.Id, club.Id, new DateOnly(2026, 1, 1), 3, startSeasonNumber: 1));

        await db.SaveChangesAsync();

        return (club.Id, other.Id, season.Id, edition.Id, division.Id);
    }

    [Fact]
    public async Task AClubWithACareerHasAPageThatIsReadOutOfTheDatabase()
    {
        await using var db = AContext();
        var world = await SeedAWorldAsync(db);

        // The club's own two divisions and its two recorded moments, so the page has something
        // of every family in it: a derived fact, a title, and a decision nothing else keeps.
        db.TrophyAwards.Add(TrophyAward.Create(
            world.teamId, world.seasonId, world.editionId, world.divisionId, TrophyKind.Champion));

        db.ClubEvents.Add(ClubEvent.Create(
            world.teamId,
            ClubEventKind.NameChange,
            world.seasonId,
            "Náutico de Guanabara",
            "Náutico"));

        await db.SaveChangesAsync();

        var profile = await AService(db).GetProfileAsync(world.teamId, world.seasonId);

        Assert.Equal(world.teamId, profile.TeamId);
        Assert.Equal("Náutico", profile.Name);
        Assert.Equal(1, profile.SquadSize);

        // The founding and the rename, both read out of the database rather than drawn.
        Assert.Equal(
            ClubHistoryKind.FirstSeason,
            Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.FirstSeason).Kind);

        var rename = Assert.Single(profile.History, entry => entry.Kind == ClubHistoryKind.NameChange);
        Assert.Contains("Náutico de Guanabara", rename.Description);

        var medal = Assert.Single(profile.Trophies);
        Assert.Equal("1ª Divisão", medal.DivisionName);
        Assert.Equal(1, medal.DivisionTier);
        Assert.Equal(TrophyKind.Champion, medal.Kind);
    }

    /// <summary>
    /// A club that is not in the pyramid has a page that says so rather than one that guesses.
    /// </summary>
    [Fact]
    public async Task AClubWithNoParticipationHasAFoundingAndNothingElse()
    {
        await using var db = AContext();
        var world = await SeedAWorldAsync(db);

        var newcomer = Team.Create("Vila Nova", "VIL", "#222222", "#cccccc");
        await db.Teams.AddAsync(newcomer);
        await db.SaveChangesAsync();

        var profile = await AService(db).GetProfileAsync(newcomer.Id, world.seasonId);

        Assert.Empty(profile.History);
        Assert.Empty(profile.Trophies);
        Assert.Equal(0, profile.SquadSize);
        Assert.Equal(0m, profile.Balance);
    }

    /// <summary>
    /// The division walk joins participants to editions to divisions and must not multiply.
    /// </summary>
    /// <remarks>
    /// The row it produces is one per season, and it is compared against the season it came
    /// from to work out which way a club moved. A walk that returned the same division twice
    /// for one season would compare a club against itself and report a club that neither moved
    /// nor stayed still, and nothing above it would notice: the history would simply be missing a
    /// season that had in fact happened.
    /// </remarks>
    [Fact]
    public async Task TheDivisionWalkReturnsOneRowPerSeasonAndNotOnePerJoin()
    {
        await using var db = AContext();
        var world = await SeedAWorldAsync(db);

        var second = Season.Create(2, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31));
        await db.Seasons.AddAsync(second);

        var otherDivision = Division.Create("2ª Divisão", 2);
        await db.Divisions.AddAsync(otherDivision);

        var secondEdition = CompetitionSeason.Create(
            (await db.Competitions.FirstAsync()).Id, second.Id, otherDivision.Id);
        await db.CompetitionSeasons.AddAsync(secondEdition);
        await db.CompetitionParticipants.AddAsync(
            CompetitionParticipant.Create(secondEdition.Id, world.teamId));

        await db.SaveChangesAsync();

        var divisions = await new TeamRepository(db).ListDivisionSeasonsAsync(world.teamId);

        Assert.Equal(2, divisions.Count);
        Assert.Equal(new[] { 1, 2 }, divisions.Select(division => division.Tier).ToArray());
        Assert.Equal(
            second.Id,
            divisions[^1].SeasonId);

        // And the page agrees: a club that went from the first division to the second in one
        // season has been relegated, exactly once.
        var profile = await AService(db).GetProfileAsync(world.teamId, world.seasonId);

        Assert.Equal(0, profile.Promotions);
        Assert.Equal(1, profile.Relegations);
    }

    /// <summary>
    /// The scorers walk joins five tables, and it is asked with every edition at once.
    /// </summary>
    /// <remarks>
    /// An edition with no match lines at all must come back as nothing rather than as a null or
    /// a throw, and an empty set of editions must not reach the database. Both are the shape of
    /// the first day of a season, when the calendar exists and none of it has been played.
    /// </remarks>
    [Fact]
    public async Task TheScorersWalkAnswersAnEditionNobodyHasPlayedYet()
    {
        await using var db = AContext();
        var world = await SeedAWorldAsync(db);

        var players = new PlayerRepository(db);

        Assert.Empty(await players.ListEditionScorerLinesForEditionsAsync(new[] { world.editionId }));
        Assert.Empty(await players.ListEditionScorerLinesForEditionsAsync(Array.Empty<Guid>()));

        // And the page reads cleanly over a season in which nothing has been kicked off.
        var profile = await AService(db).GetProfileAsync(world.teamId, world.seasonId);

        Assert.DoesNotContain(profile.History, entry => entry.Kind == ClubHistoryKind.TopScorer);
    }

    /// <summary>
    /// A rename is written by the same call that renames the club.
    /// </summary>
    /// <remarks>
    /// The row and the column move together or not at all. A rename that changed the name and
    /// recorded nothing would leave a club's own history unable to mention the moment, which is
    /// the exact failure the recorded events exist to prevent — so this asserts both halves in
    /// the same save.
    /// </remarks>
    [Fact]
    public async Task RenamingAClubRecordsTheRenameInTheSameUnitOfWork()
    {
        Guid teamId;
        await using (var seeded = AContext())
        {
            teamId = (await SeedAWorldAsync(seeded)).teamId;
        }

        // A scope of its own, because that is what a request gets. The context that seeded the
        // world still has the club it created tracked, and handing that same club to a service
        // in the same scope is a setup no request ever produces: it would be two instances of
        // one row in one context, which EF refuses for a reason that has nothing to do with
        // anything this test is here to check.
        await using var db = AContext();

        var service = new TeamService(
            new TeamRepository(db),
            new PlayerRepository(db),
            new SeasonRepository(db),
            new MatchRepository(db),
            new ClubEventRepository(db),
            new EfUnitOfWork(db));

        await service.UpdateNameAsync(teamId, "Náutico FC");

        var club = await db.Teams.AsNoTracking().FirstAsync(team => team.Id == teamId);
        Assert.Equal("Náutico FC", club.Name);

        var recorded = await db.ClubEvents.AsNoTracking()
            .Where(entry => entry.TeamId == teamId)
            .ToListAsync();
        var rename = Assert.Single(recorded);

        Assert.Equal(ClubEventKind.NameChange, rename.Kind);
        Assert.Equal("Náutico", rename.PreviousValue);
        Assert.Equal("Náutico FC", rename.NewValue);

        // And the page says it, from the same database the rename was written to — and says it
        // with both names, because the rename is the one moment whose whole content is the two
        // values on either side of it.
        var profile = await AService(db).GetProfileAsync(teamId);

        var renameOnThePage = Assert.Single(
            profile.History, entry => entry.Kind == ClubHistoryKind.NameChange);

        Assert.Equal("Náutico passou a chamar-se Náutico FC.", renameOnThePage.Description);
    }
}