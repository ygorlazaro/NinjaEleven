using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// Two callers completing the same window, against the database that will settle it.
///
/// <para>
/// A deal is completed by the matchday sweep when a window closes and by
/// <c>POST /transfer/complete</c> when a hand asks for it, and both read the same list of
/// accepted deals untracked. Before the claim, each of them signed the player: the membership
/// is written before the status is, so the second caller added a second one on top of the
/// first and the club finished the window holding one man three times, with a single deal row
/// insisting he had arrived once.
/// </para>
///
/// <para>
/// The claim is a conditional update, and an in-memory provider cannot hold it: there are no
/// row locks, so two callers would both "win" and the test would pass against a provider that
/// is not the one the world runs on. This asks PostgreSQL directly.
/// </para>
/// </summary>
[Collection("Sequential")]
public class TransferCompletionClaimTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"claim_transfers_{Guid.NewGuid():N}";
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

    private TransferRepository AStore(NinjaElevenDbContext db) => new(db);

    /// <summary>
    /// One deal, waiting for a window nobody has opened yet.
    /// </summary>
    private Task<Guid> AnAcceptedDealAsync() => ADealAsync(accepted: true);

    private async Task<Guid> APendingDealAsync() => await ADealAsync(accepted: false);

    private async Task<Guid> ADealAsync(bool accepted)
    {
        await using var context = AContext();

        var team = Team.Create("Renascença", "REN", "#101820", "#38d39f", 70);
        context.Teams.Add(team);

        var player = Player.Create(
            "Leonardo Vidal de Oliveira", 27, Position.GK, 12, 10, 9, 9, 14, 78, 74);
        context.Players.Add(player);

        // A deal names the season it was proposed in, and the row carries a foreign key to it —
        // so the fixture owns a real season rather than a random guid the database refuses.
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        context.Seasons.Add(season);

        await context.SaveChangesAsync();

        var deal = Transfer.Propose(
            player.Id, sellingClubId: null, team.Id, season.Id,
            arrivalSeasonNumber: 2, arrivalSeasonId: null,
            fee: 1000m, DateOnly.FromDateTime(DateTime.Now), arrivalRoundNumber: 1);
        if (accepted)
        {
            deal.Accept(DateOnly.FromDateTime(DateTime.Now));
        }

        context.Transfers.Add(deal);
        await context.SaveChangesAsync();

        return deal.Id;
    }

    /// <summary>
    /// The claim itself, asked twice the way the world asks it: once by the sweep and once by
    /// the route. Exactly one of them is told yes.
    /// </summary>
    [Fact]
    public async Task TwoCallersCompletingOneWindowProduceOneClaim()
    {
        var dealId = await AnAcceptedDealAsync();

        bool first, second;

        await using (var context = AContext())
        {
            first = await AStore(context).TryClaimForCompletionAsync(dealId);
        }

        await using (var other = AContext())
        {
            second = await AStore(other).TryClaimForCompletionAsync(dealId);
        }

        Assert.True(first);
        Assert.False(second);
    }

    /// <summary>
    /// And the consequence in the row: the deal says the player arrived, once, and it says it
    /// to whoever reads it next.
    /// </summary>
    [Fact]
    public async Task AClaimedDealIsCompletedAndStaysCompleted()
    {
        var dealId = await AnAcceptedDealAsync();

        await using (var context = AContext())
        {
            await AStore(context).TryClaimForCompletionAsync(dealId);
        }

        await using var check = AContext();

        var deal = await check.Transfers.AsNoTracking().FirstAsync(t => t.Id == dealId);

        Assert.Equal(TransferStatus.Completed, deal.Status);
        Assert.NotNull(deal.CompletedAt);

        // A third caller, later, is still told no: the window is not re-opened by being asked
        // again, and this is what the controller's "safe to call twice" has to mean.
        await using var later = AContext();
        Assert.False(await AStore(later).TryClaimForCompletionAsync(dealId));
    }

    /// <summary>
    /// A deal nobody claimed is still claimable, which is the other half: the claim refuses a
    /// second attempt and not the first one.
    /// </summary>
    [Fact]
    public async Task ADealThatWasNeverClaimedIsStillWaiting()
    {
        var dealId = await AnAcceptedDealAsync();

        await using var check = AContext();
        var deal = await check.Transfers.AsNoTracking().FirstAsync(t => t.Id == dealId);

        Assert.Equal(TransferStatus.Accepted, deal.Status);
        Assert.Null(deal.CompletedAt);
    }

    /// <summary>
    /// A deal for a player nobody signed is not claimable at all, so the predicate cannot be
    /// satisfied by an id that happens to exist: the claim is on the status, not on the row.
    /// </summary>
    [Fact]
    public async Task ADealThatIsNotAcceptedIsNotClaimable()
    {
        // A proposal still waiting on an answer is not a deal arriving, and the claim is on
        // the status rather than on the row: a caller cannot sign a player by asking for a
        // transfer id that happens to exist.
        var dealId = await APendingDealAsync();

        await using var claim = AContext();
        Assert.False(await AStore(claim).TryClaimForCompletionAsync(dealId));
    }

    /// <summary>
    /// The invariant the claim protects, written down where it can be read on its own: a club
    /// does not hold the same man twice, and the membership is what says so.
    /// </summary>
    [Fact]
    public async Task AClubDoesNotHoldTheSameManTwice()
    {
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        await using var context = AContext();

        var team = Team.Create("Clube", "CLB", "#101820", "#38d39f", 70);
        context.Teams.Add(team);

        var player = Player.Create(
            "Renzo Simonato", 26, Position.ATT, 15, 15, 14, 13, 13, 0, 7);
        context.Players.Add(player);

        await context.SaveChangesAsync();
        teamId = team.Id;
        playerId = player.Id;

        context.TeamMemberships.Add(TeamMembership.Create(playerId, teamId, new DateOnly(2026, 1, 1), shirtNumber: 9));
        await context.SaveChangesAsync();

        await using var check = AContext();

        var live = await check.TeamMemberships
            .Where(membership => membership.EndDate == null && membership.TeamId == teamId)
            .ToListAsync();

        Assert.Single(live);
        Assert.Equal(playerId, live[0].PlayerId);
    }
}
