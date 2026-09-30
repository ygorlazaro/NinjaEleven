using Microsoft.EntityFrameworkCore;
using NinjaEleven.Application.Abstractions;
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
/// The claim on a window of football, against the database that will actually be asked to
/// make it.
///
/// <para>
/// Every other test of the scheduler fakes this seam, because the rule and the locking are two
/// different things and a unit test can only hold one of them at a time. The rule is held by
/// the Application tests; this holds the locking, and the locking is the whole reason the
/// claim is a row and not a boolean. A claim that were only a boolean would let two processes
/// read <c>Scheduled</c> at the same instant and both write <c>Running</c>, and a season would
/// quietly play half its fixtures twice.
///
/// </para>
///
/// <para>
/// So this runs against PostgreSQL, in its own schema, and it runs two claims against the
/// same row at the same time. That needs the real engine: EF's in-memory provider has no
/// rows to lock and would answer "yes" to both, which is the failure this is here to catch.
/// </para>
/// </summary>
[Collection("Sequential")]
public class RoundExecutionStoreTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"claim_test_{Guid.NewGuid():N}";
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
                "These tests claim a row lock in PostgreSQL, which is the only engine that can "
                + "answer the question they ask. The world runs on PostgreSQL and so must its "
                + "tests: start it with `docker start postgres` and run them again.",
                exception);
        }

        // A database of its own, migrated from empty and dropped again. It is the real
        // migrations rather than a synthesized schema, because the claim is a row lock and a
        // row lock is only the same row lock if the table is the same table — a hand-built
        // schema would test a database nobody runs.
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
            // Npgsql refuses to drop the database it is connected to, so the connection that
            // made it is closed before the drop and reopened after.
            await _setup.Database.CloseConnectionAsync();
            await _setup.Database.ExecuteSqlRawAsync(
                $"DROP DATABASE IF EXISTS {_database} WITH (FORCE)");

            await _setup.DisposeAsync();
        }
    }

    // The world's own configuration, so the model under test is the model the world runs on
    // rather than a context that happens to share its class.
    /// <summary>Which process a store belongs to, so two of them can be two processes.</summary>
    private sealed class AHost(string hostId) : IMatchHost
    {
        public string HostId { get; } = hostId;
    }

    private RoundExecutionStore AStore(NinjaElevenDbContext db, string hostId) =>
        new(db, new AHost(hostId));

    private NinjaElevenDbContext AContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>();
        NinjaElevenDbContext.Configure(options, ConnectionString);

        return new NinjaElevenDbContext(options.Options);
    }

    /// <summary>
    /// A window of football, and everything the foreign keys under it insist on existing first.
    /// </summary>
    private async Task<Guid> SeedAWindowAsync()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var competition = Competition.Create("Brasileirão", CompetitionType.League);
        var edition = CompetitionSeason.Create(competition.Id, season.Id);
        var round = Round.Create(edition.Id, 1);

        await using var db = AContext();
        db.AddRange(season, competition, edition, round);
        await db.SaveChangesAsync();

        return round.Id;
    }

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 16, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A window of football with a fixture in it, for the tests that are about whether the
    /// window's own columns or its fixtures are the record of what was played.
    /// </summary>
    private async Task<Guid> SeedAWindowWithAFixtureAsync()
    {
        var roundId = await SeedAWindowAsync();

        var home = Team.Create("Casa", "CAS", "#111111", "#222222");
        var away = Team.Create("Fora", "FOR", "#333333", "#444444");

        await using var db = AContext();
        db.AddRange(home, away, Fixture.Create(roundId, home.Id, away.Id));
        await db.SaveChangesAsync();

        return roundId;
    }

    /// <summary>Says of a window that it has been played, without playing anything.</summary>
    private async Task CloseAWindowAsPlayedAsync(Guid roundId)
    {
        await using var db = AContext();
        var round = await db.Rounds.FirstAsync(item => item.Id == roundId);
        round.TryBeginExecution(Now, Lease, "scheduler-1");
        round.CompleteExecution();
        db.Rounds.Update(round);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Two_processes_claiming_the_same_window_produce_exactly_one_winner()
    {
        // The test the whole store exists for. Both contexts are separate connections, so the
        // only thing standing between them is `FOR UPDATE` and the state change under it.
        var roundId = await SeedAWindowAsync();

        // Two contexts, two connections, released together: the only thing between them is
        // the row lock and the state change underneath it.
        var barrier = new Barrier(2);

        async Task<RoundClaim> ClaimAsync()
        {
            await using var db = AContext();

            barrier.SignalAndWait();
            return await AStore(db, "api-1").TryClaimAsync(roundId, Lease, Now);
        }

        var claims = await Task.WhenAll(
            Task.Run(ClaimAsync),
            Task.Run(ClaimAsync));

        Assert.Single(claims, claim => claim is RoundClaim.Claimed);
        Assert.Single(claims, claim => claim is RoundClaim.HeldByAnotherProcess);
    }

    [Fact]
    public async Task A_window_whose_claim_was_taken_is_refused_while_the_lease_is_alive()
    {
        var roundId = await SeedAWindowAsync();

        await using var db = AContext();
        var store = AStore(db, "api-1");

        Assert.Equal(RoundClaim.Claimed, await store.TryClaimAsync(roundId, Lease, Now));

        // Same process, minutes later, the same window. This is the second poll of a scheduler
        // that has just played it, and it is the one that must not walk in.
        Assert.Equal(
            RoundClaim.HeldByAnotherProcess,
            await store.TryClaimAsync(roundId, Lease, Now.AddMinutes(5)));
    }

    [Fact]
    public async Task A_window_whose_owner_never_came_back_is_taken_by_the_next_process()
    {
        // The claim was written by a process that died at kick-off. The row is still on the
        // calendar, still says Running, and says it was last heard from half an hour ago.
        var roundId = await SeedAWindowAsync();

        await using (var crashed = AContext())
        {
            await AStore(crashed, "scheduler-1").TryClaimAsync(roundId, Lease, Now);
        }

        await using var survivor = AContext();

        Assert.Equal(
            RoundClaim.Claimed,
            await AStore(survivor, "scheduler-2").TryClaimAsync(roundId, Lease, Now.AddMinutes(31)));
    }

    [Fact]
    public async Task A_window_that_was_played_whole_is_never_offered_again()
    {
        var roundId = await SeedAWindowAsync();

        await using var db = AContext();
        var store = AStore(db, "api-1");

        await store.TryClaimAsync(roundId, Lease, Now);
        Assert.True(await store.TryCompleteAsync(roundId, Lease, Now.AddMinutes(2)));

        // A restart, a redeploy, a misfire: whatever the reason the job fired, the window has
        // been played and the answer has to be the same one.
        Assert.Equal(RoundClaim.AlreadyPlayed, await store.TryClaimAsync(roundId, Lease, Now.AddDays(1)));
    }

    [Fact]
    public async Task A_window_closed_by_hand_is_never_offered_to_the_scheduler()
    {
        // No claim, no execution status — a person pressed the button and the last fixture of
        // the window finished. Reading only the claim would hand it to the scheduler.
        var roundId = await SeedAWindowAsync();

        await using (var db = AContext())
        {
                var round = await db.Rounds.FirstAsync(item => item.Id == roundId);
            round.Complete();
            await db.SaveChangesAsync();
        }

        await using var verifier = AContext();

        Assert.Equal(
            RoundClaim.AlreadyPlayed,
            await AStore(verifier, "api-1").TryClaimAsync(roundId, Lease, Now.AddDays(1)));
    }

    [Fact]
    public async Task A_window_whose_lease_ran_out_is_not_completed_by_the_process_that_lost_it()
    {
        // The first process is still playing when its lease expires and a second process takes
        // the window over. The first one to finish must not write "completed" over the top of
        // the second one's work — the round is still being played, and it is being played by
        // somebody else now.
        var roundId = await SeedAWindowAsync();

        await using (var first = AContext())
        {
            await AStore(first, "scheduler-1").TryClaimAsync(roundId, Lease, Now);
        }

        await using var second = AContext();
        Assert.Equal(
            RoundClaim.Claimed,
            await AStore(second, "scheduler-2").TryClaimAsync(roundId, Lease, Now.AddMinutes(31)));

        // The process that lost the claim, arriving late with its results.
        await using var loser = AContext();
        Assert.False(await AStore(loser, "scheduler-1").TryCompleteAsync(roundId, Lease, Now.AddMinutes(40)));
    }

    [Fact]
    public async Task A_released_window_is_taken_by_the_next_run_without_waiting_out_the_lease()
    {
        // A window that failed is the common case, and a process that gave up on it must not
        // keep it for the length of the lease: the next run of the job is the thing that
        // finishes it.
        var roundId = await SeedAWindowAsync();

        await using var db = AContext();
        var store = AStore(db, "api-1");

        await store.TryClaimAsync(roundId, Lease, Now);
        await store.ReleaseAsync(roundId);

        Assert.Equal(
            RoundClaim.Claimed,
            await store.TryClaimAsync(roundId, Lease, Now.AddSeconds(1)));
    }

    [Fact]
    public async Task A_window_that_is_not_there_is_reported_as_missing_rather_than_thrown()
    {
        await using var db = AContext();

        Assert.Equal(
            RoundClaim.NotFound,
            await AStore(db, "api-1").TryClaimAsync(Guid.NewGuid(), Lease, Now));
    }

    [Fact]
    public async Task A_window_closed_over_a_fixture_nobody_played_is_offered_again()
    {
        // The bug this whole rule exists for. A match of this window was given up on, its fixture
        // was put back on the schedule, and the window was written up as played anyway. Believing
        // the window's own columns here would leave that matchday owed to nobody for the rest of
        // the season: a gap in the table that nothing would ever come back to fill.
        var roundId = await SeedAWindowWithAFixtureAsync();
        await CloseAWindowAsPlayedAsync(roundId);

        await using var db = AContext();

        Assert.Equal(
            RoundClaim.Claimed,
            await AStore(db, "api-2").TryClaimAsync(roundId, Lease, Now.AddDays(1)));
    }

    [Fact]
    public async Task A_window_closed_over_a_fixture_nobody_played_is_back_on_the_schedule()
    {
        // Being offered is only half of it: the window has to stop saying it has been played, or
        // the next poll after this one will find the same row and believe the same columns.
        var roundId = await SeedAWindowWithAFixtureAsync();
        await CloseAWindowAsPlayedAsync(roundId);

        await using (var db = AContext())
        {
            await AStore(db, "api-2").TryClaimAsync(roundId, Lease, Now.AddDays(1));
        }

        await using var verifier = AContext();
        var round = await verifier.Rounds.FirstAsync(item => item.Id == roundId);

        Assert.False(round.HasBeenExecuted);
        Assert.Equal(RoundExecutionStatus.Running, round.ExecutionStatus);
    }

    [Fact]
    public async Task A_window_whose_fixtures_are_all_finished_is_never_offered_again()
    {
        // The other half of the same rule, and the one that must not be given up: a matchday
        // that really was played stays played, or the world would replay a season forever.
        var roundId = await SeedAWindowWithAFixtureAsync();

        await using (var db = AContext())
        {
            var fixture = await db.Fixtures.FirstAsync(item => item.RoundId == roundId);
            fixture.MarkFinished();
            db.Fixtures.Update(fixture);
            await db.SaveChangesAsync();
        }

        await CloseAWindowAsPlayedAsync(roundId);

        await using var verifier = AContext();

        Assert.Equal(
            RoundClaim.AlreadyPlayed,
            await AStore(verifier, "api-2").TryClaimAsync(roundId, Lease, Now.AddDays(1)));
    }

    [Fact]
    public async Task A_window_with_a_fixture_left_in_it_cannot_be_written_up_as_played()
    {
        // The other end of the same hole. A process that played a window has finished it as far
        // as it knows, and asks to close it; the fixtures disagree, and the closure is refused —
        // because this write is the one that loses a matchday, and it must be impossible rather
        // than merely unlikely.
        var roundId = await SeedAWindowWithAFixtureAsync();

        await using var db = AContext();
        var store = AStore(db, "api-1");

        Assert.Equal(RoundClaim.Claimed, await store.TryClaimAsync(roundId, Lease, Now));
        Assert.False(await store.TryCompleteAsync(roundId, Lease, Now.AddMinutes(2)));

        await using var verifier = AContext();
        var round = await verifier.Rounds.FirstAsync(item => item.Id == roundId);

        Assert.False(round.HasBeenExecuted);
    }

    [Fact]
    public async Task A_window_is_written_up_once_the_last_of_its_fixtures_is_finished()
    {
        var roundId = await SeedAWindowWithAFixtureAsync();

        await using (var playing = AContext())
        {
            var store = AStore(playing, "api-1");
            await store.TryClaimAsync(roundId, Lease, Now);
        }

        await using (var settling = AContext())
        {
            var fixture = await settling.Fixtures.FirstAsync(item => item.RoundId == roundId);
            fixture.MarkFinished();
            settling.Fixtures.Update(fixture);
            await settling.SaveChangesAsync();
        }

        await using var closing = AContext();
        Assert.True(await AStore(closing, "api-1").TryCompleteAsync(roundId, Lease, Now.AddMinutes(3)));
    }
}
