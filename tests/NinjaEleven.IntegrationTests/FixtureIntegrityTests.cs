using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// The reader that finds the fixtures the world believes it played and has no result for.
///
/// <para>
/// This runs against PostgreSQL because the question is a translation. The rule — a fixture
/// is decided only by a match that reached the final whistle — is held by the Application tests;
/// what only a real engine can hold is that the answer is asked in one query and that the
/// answer is the same one the world will get in production. A repository that fell back to
/// loading every fixture and every match into memory would pass a mocked test and hand a
/// season's football to the application layer on every poll.
/// </para>
///
/// <para>
/// The four fixtures below are the four cases, and the second one is the one that costs a
/// season: a fixture marked played whose only match was abandoned, which is exactly what six
/// fixtures of a second division looked like while the table above them was a game short for
/// eleven of sixteen clubs.
/// </para>
/// </summary>
[Collection("Sequential")]
public class FixtureIntegrityTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"fixture_integrity_{Guid.NewGuid():N}";
    private NinjaElevenDbContext? _setup;

    private string ConnectionString => $"{Server};Database={_database}";

    public async Task InitializeAsync()
    {
        try
        {
            await using var probe = new NinjaElevenDbContext(
                new DbContextOptionsBuilder<NinjaElevenDbContext>().UseNpgsql(Server).Options);

            await probe.Database.OpenConnectionAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "These tests ask PostgreSQL the question the world asks it on every poll. "
                + "Start it with `docker start postgres` and run them again.",
                exception);
        }

        _setup = new NinjaElevenDbContext(
            new DbContextOptionsBuilder<NinjaElevenDbContext>().UseNpgsql(Server).Options);

        await _setup.Database.ExecuteSqlRawAsync($"CREATE DATABASE {_database}");

        await using var created = AContext();
        await created.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_setup is not null)
        {
            await _setup.Database.CloseConnectionAsync();
            await _setup.Database.ExecuteSqlRawAsync(
                $"DROP DATABASE IF EXISTS {_database} WITH (FORCE)");

            await _setup.DisposeAsync();
        }
    }

    private NinjaElevenDbContext AContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>();
        NinjaElevenDbContext.Configure(options, ConnectionString);

        return new NinjaElevenDbContext(options.Options);
    }

    private static FixtureRepository ARepository(NinjaElevenDbContext db) => new(db);

    /// <summary>
    /// A window with four fixtures in it, one for each of the four cases the reader has to
    /// tell apart.
    /// </summary>
    private async Task<(Guid Played, Guid Abandoned, Guid Scheduled, Guid Replayed)> SeedFourFixturesAsync()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var competition = Competition.Create("Brasileirão", CompetitionType.League);
        var edition = CompetitionSeason.Create(competition.Id, season.Id);
        var round = Round.Create(edition.Id, 1);

        var teams = Enumerable.Range(1, 8)
            .Select(number => Team.Create($"Clube {number}", $"C{number}", "#111111", "#222222"))
            .ToList();

        var played = Fixture.Create(round.Id, teams[0].Id, teams[1].Id);
        played.MarkFinished();

        var abandoned = Fixture.Create(round.Id, teams[2].Id, teams[3].Id);
        abandoned.MarkFinished();

        var scheduled = Fixture.Create(round.Id, teams[4].Id, teams[5].Id);

        var replayed = Fixture.Create(round.Id, teams[6].Id, teams[0].Id);
        replayed.MarkFinished();

        await using var db = AContext();
        db.AddRange(season, competition, edition, round);
        db.AddRange(teams);
        db.AddRange(played, abandoned, scheduled, replayed);
        await db.SaveChangesAsync();

        // The match that reached the final whistle, and the one that did not.
        var finished = Match.Create(played.Id, teams[0].Id, teams[1].Id);
        finished.Finish();

        var givenUp = Match.Create(abandoned.Id, teams[2].Id, teams[3].Id);
        givenUp.Abandon();

        // A fixture that was played, given up on, and played again: the two rows are the
        // design (an abandoned match stays as the record of what the football was), and the
        // fixture is decided by the second one.
        var firstTry = Match.Create(replayed.Id, teams[6].Id, teams[0].Id);
        firstTry.Abandon();

        var secondTry = Match.Create(replayed.Id, teams[6].Id, teams[0].Id);
        secondTry.Finish();

        db.AddRange(finished, givenUp, firstTry, secondTry);
        await db.SaveChangesAsync();

        return (played.Id, abandoned.Id, scheduled.Id, replayed.Id);
    }

    [Fact]
    public async Task Only_the_fixture_whose_every_match_was_abandoned_is_in_the_answer()
    {
        var fixtures = await SeedFourFixturesAsync();

        await using var db = AContext();
        var lost = await ARepository(db).ListFinishedWithoutAFinishedMatchAsync();

        // The one that was played, the one still waiting for its day, and the one that was
        // given up on and then played again are all decided, and a decided fixture is never
        // in this answer: reopening one would have the world play a finished matchday twice.
        var ids = lost.Select(fixture => fixture.Id).ToList();

        Assert.Single(ids);
        Assert.Equal(fixtures.Abandoned, ids[0]);
        Assert.DoesNotContain(fixtures.Played, ids);
        Assert.DoesNotContain(fixtures.Scheduled, ids);
        Assert.DoesNotContain(fixtures.Replayed, ids);
    }

    [Fact]
    public async Task A_fixture_nobody_has_reached_yet_is_not_asked_about()
    {
        // A fixture with no match at all is a fixture the calendar has not got to, and
        // reopening one would put a future matchday into arrears. The reader is joined to the
        // matches rather than filtered by hand precisely so this case never reaches it.
        var fixtures = await SeedFourFixturesAsync();

        await using var db = AContext();
        var scheduled = await db.Fixtures.FirstAsync(fixture => fixture.Id == fixtures.Scheduled);
        var lost = await ARepository(db).ListFinishedWithoutAFinishedMatchAsync();

        Assert.Equal(FixtureStatus.Scheduled, scheduled.Status);
        Assert.DoesNotContain(fixtures.Scheduled, lost.Select(fixture => fixture.Id));
    }
}
