using Microsoft.EntityFrameworkCore;
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
/// A row read untracked, written through a context that is already holding it.
///
/// <para>
/// This is the failure that stopped the world. Reads are <c>AsNoTracking</c> all over the
/// repositories, so a service holding a membership or a deal is holding a copy the context
/// does not own; and when something earlier on the same request left the row tracked, the
/// write is a second instance of a key EF is already tracking, which it refuses. It was
/// refused from inside the transfer market, which poisons the context for the rest of the
/// request, and the window that the market runs on — the Supercup, the first window of a
/// season — could not commit its own result. It failed, reopened its fixture, and failed
/// again, for ever. A season's worth of football stopped because a repository could not tell
/// the difference between a row it had and a row it had been handed.
/// </para>
///
/// <para>
/// It cannot be held by a mocked test, because the clash is between two reads of the same
/// context and only a real change tracker produces it. The claim is asked of PostgreSQL
/// because the other half of the same rule — that the write lands — is only true there.
/// </para>
/// </summary>
[Collection("Sequential")]
public class TrackedWriteThroughTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"tracked_writes_{Guid.NewGuid():N}";
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
        if (_setup is null)
        {
            return;
        }

        await _setup.Database.CloseConnectionAsync();
        await _setup.Database.ExecuteSqlRawAsync(
            $"DROP DATABASE IF EXISTS {_database} WITH (FORCE)");

        await _setup.DisposeAsync();
    }

    private NinjaElevenDbContext AContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>();
        NinjaElevenDbContext.Configure(options, ConnectionString);

        return new NinjaElevenDbContext(options.Options);
    }

    /// <summary>
    /// A club, a player, a season, a contract between them, and a deal that has not been
    /// answered. The four things the market touches in one pass.
    /// </summary>
    private async Task<(Guid TeamId, Guid PlayerId, Guid SeasonId, Guid MembershipId, Guid DealId)>
        AClubWithAPlayerAndADealAsync()
    {
        await using var context = AContext();

        var team = Team.Create("Renascença", "REN", "#101820", "#38d39f", 70);
        var buyer = Team.Create("Tradição", "TRA", "#222222", "#dddddd", 68);
        var player = Player.Create(
            "Leonardo Vidal de Oliveira", 27, Position.MID, 12, 10, 9, 9, 14, 78, 74);
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        context.Teams.Add(team);
        context.Teams.Add(buyer);
        context.Players.Add(player);
        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        var membership = TeamMembership.Create(
            player.Id, team.Id, DateOnly.FromDateTime(DateTime.Now));

        context.TeamMemberships.Add(membership);

        // A club cannot bid for its own man, so the deal has a second club on the other side —
        // which is also the shape the market works in: a seller's contract ended and a buyer
        // signed, in one pass, in one context.
        var deal = Transfer.Propose(
            player.Id, team.Id, buyer.Id, season.Id,
            arrivalSeasonNumber: 2, arrivalSeasonId: null,
            fee: 1000m, DateOnly.FromDateTime(DateTime.Now), arrivalRoundNumber: 1);

        context.Transfers.Add(deal);
        await context.SaveChangesAsync();

        return (team.Id, player.Id, season.Id, membership.Id, deal.Id);
    }

    /// <summary>
    /// The shape the world actually produces: the row is read once tracked and once untracked
    /// in the same scope, and the untracked copy is the one the service is holding when it
    /// writes. Ending the seller's contract is exactly this — <c>GetLiveContractsAsync</c> is
    /// untracked, and the market ends the membership it read there.
    /// </summary>
    [Fact]
    public async Task AContractReadTwiceIsEndedByTheCopyTheServiceIsHolding()
    {
        var seeded = await AClubWithAPlayerAndADealAsync();

        await using var context = AContext();
        var teams = new TeamRepository(context);

        // The first read, tracked: what a query without AsNoTracking leaves behind.
        var trackedRead = await context.TeamMemberships
            .FirstAsync(membership => membership.Id == seeded.MembershipId);

        // The second read, untracked: what the repository hands the service.
        var held = await teams.GetLiveContractsAsync(seeded.TeamId);

        var contract = held.Single(membership => membership.Id == seeded.MembershipId);
        contract.End(DateOnly.FromDateTime(DateTime.Now));

        // This is the line that used to throw: EF refused a second instance of a key the
        // context was already tracking, from inside the market, on the first window of the
        // season.
        teams.UpdateMembership(contract);

        await context.SaveChangesAsync();

        // And the write landed on the row that was already there rather than on the copy,
        // which is the whole of what the fix is for.
        context.ChangeTracker.Clear();

        var written = await context.TeamMemberships
            .AsNoTracking()
            .SingleAsync(membership => membership.Id == seeded.MembershipId);

        Assert.NotNull(written.EndDate);
        Assert.NotEqual(trackedRead.Id, default);
    }

    /// <summary>
    /// The same rule for a deal. Expiring a season's unanswered proposals reads the list once
    /// and writes every row of it, so a single earlier read of one of them in the same request
    /// is enough to make the season's close throw from halfway through.
    /// </summary>
    [Fact]
    public async Task ADealReadTwiceIsExpiredByTheCopyTheServiceIsHolding()
    {
        var seeded = await AClubWithAPlayerAndADealAsync();

        await using var context = AContext();
        var transfers = new TransferRepository(context);

        var trackedRead = await context.Transfers
            .FirstAsync(deal => deal.Id == seeded.DealId);

        var pending = await transfers.ListPendingAsync(seeded.SeasonId);
        var deal = pending.Single(candidate => candidate.Id == seeded.DealId);

        deal.Expire(DateOnly.FromDateTime(DateTime.Now));
        transfers.Update(deal);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var written = await context.Transfers
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seeded.DealId);

        Assert.Equal(TransferStatus.Expired, written.Status);
        Assert.Equal(trackedRead.Id, written.Id);
    }
}
