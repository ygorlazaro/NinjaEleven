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
/// The club page answers with the season's business: a bid still on the table, a bid agreed
/// and a player who arrived. Rejected and expired are the two ways a proposal dies without
/// anybody moving, and they are left off the page — otherwise a club that had the same striker
/// bid for three times and lost him three times shows a season in which he left three times
/// and arrived never.
///
/// The query is exercised through a real DbContext rather than a mocked repository, so the
/// status filter is translated by EF Core and not merely asserted in C#.
/// </summary>
public class ClubTransferHistoryRepositoryTests : IDisposable
{
    private readonly NinjaElevenDbContext _dbContext;
    private readonly TransferRepository _repository;

    public ClubTransferHistoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"ClubTransferTest-{Guid.NewGuid()}")
            .Options;

        _dbContext = new NinjaElevenDbContext(options);
        _repository = new TransferRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ListByClubAsync_ReturnsLiveDealsAndLeavesOutTheDeadLetters()
    {
        var (club, rival, season) = await SeedAsync();

        var wanted = Player.Create("Bergamoti", 27, Position.MID, 12, 12, 12, 12, 12, 1, 1);
        var refused = Player.Create("Recusado", 30, Position.ATT, 12, 12, 12, 12, 12, 1, 1);
        var timedOut = Player.Create("Expirado", 24, Position.ATT, 12, 12, 12, 12, 12, 1, 1);
        var otherClub = Player.Create("DeOutroClube", 26, Position.ATT, 12, 12, 12, 12, 12, 1, 1);
        await _dbContext.Players.AddRangeAsync(wanted, refused, timedOut, otherClub);
        await _dbContext.SaveChangesAsync();

        var date = new DateOnly(2026, 7, 10);

        // Still on the selling club's desk.
        var pending = Transfer.Propose(
            wanted.Id, club.Id, rival.Id, season.Id, 2, null, 5_000_000m, date);
        pending.SetArrivalRound(12);

        // Read and agreed, waiting for the window to carry the player across.
        var accepted = Transfer.Propose(
            wanted.Id, club.Id, rival.Id, season.Id, 2, null, 4_000_000m, date);
        accepted.Accept(date.AddDays(1));
        accepted.SetArrivalRound(13);

        // The selling club said no.
        var rejected = Transfer.Propose(
            refused.Id, club.Id, rival.Id, season.Id, 2, null, 3_000_000m, date);
        rejected.Reject(date.AddDays(1));

        // Nobody answered it before the deadline.
        var expired = Transfer.Propose(
            timedOut.Id, club.Id, rival.Id, season.Id, 2, null, 2_000_000m, date);
        expired.Expire(date.AddDays(2));

        // A live deal between two clubs that have nothing to do with this one.
        var elsewhere = Transfer.Propose(
            otherClub.Id, rival.Id, club.Id == default ? rival.Id : Guid.NewGuid(),
            season.Id, 2, null, 1_000_000m, date);
        elsewhere.Accept(date.AddDays(1));

        await _dbContext.Transfers.AddRangeAsync(pending, accepted, rejected, expired, elsewhere);
        await _dbContext.SaveChangesAsync();

        var history = await _repository.ListByClubAsync(club.Id, new[] { 2 });

        Assert.Equal(
            new[] { pending.Id, accepted.Id }.OrderBy(id => id),
            history.Select(t => t.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task ListByClubAsync_KeepsEverySeasonItIsAskedAboutAndNoOther()
    {
        var (club, rival, season) = await SeedAsync();

        var player = Player.Create("Bisseti", 25, Position.DEF, 12, 12, 12, 12, 12, 1, 1);
        await _dbContext.Players.AddAsync(player);
        await _dbContext.SaveChangesAsync();

        var date = new DateOnly(2026, 7, 10);

        // A deal made in the first season and arriving in it.
        var thisSeason = Transfer.Propose(
            player.Id, club.Id, rival.Id, season.Id, 1, season.Id, 1_000_000m, date);
        thisSeason.Accept(date.AddDays(1));
        thisSeason.Complete(date.AddDays(2));

        // The same club's business a season earlier.
        var lastSeason = Transfer.Propose(
            player.Id, club.Id, rival.Id, season.Id, 2, null, 800_000m, date);
        lastSeason.Accept(date.AddDays(1));

        await _dbContext.Transfers.AddRangeAsync(thisSeason, lastSeason);
        await _dbContext.SaveChangesAsync();

        var current = await _repository.ListByClubAsync(club.Id, new[] { 1 });
        var both = await _repository.ListByClubAsync(club.Id, new[] { 1, 2 });

        Assert.Equal(new[] { thisSeason.Id }, current.Select(t => t.Id));
        Assert.Equal(2, both.Count);
    }

    private async Task<(Team Club, Team Rival, Season Season)> SeedAsync()
    {
        var club = Team.Create("Aurora", "CAU", "#E07B00", "#2B2B2B");
        var rival = Team.Create("Boreal", "BOR", "#0057B8", "#FFFFFF");
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        await _dbContext.Teams.AddRangeAsync(club, rival);
        await _dbContext.Seasons.AddAsync(season);
        await _dbContext.SaveChangesAsync();

        return (club, rival, season);
    }
}
