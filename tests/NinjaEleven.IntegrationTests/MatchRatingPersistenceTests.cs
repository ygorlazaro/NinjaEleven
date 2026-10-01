using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A finished match, written and read back against the database that will actually hold it.
///
/// <para>
/// A rating is the one number in the game that is stored rather than worked out on demand, and
/// that choice has a cost: if the column is missing, misnamed or rounded on the way in, the
/// card a manager reads is a number the database invented. An in-memory provider would accept
/// a mapping that PostgreSQL refuses, and would round nothing, so this runs against the real
/// thing — including a man who was on the pitch for four minutes and must come back with no
/// rating at all rather than a zero.
/// </para>
/// </summary>
[Collection("Sequential")]
public class MatchRatingPersistenceTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"ratings_test_{Guid.NewGuid():N}";
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

    [Fact]
    public async Task TheRatingTheWhistleProducedIsTheRatingTheCardShows()
    {
        var world = await AWorld();
        var (matchId, playerId, teamId, otherPlayerId, otherTeamId) = world;

        // A striker's evening and a four-minute substitute, written in the same transaction
        // because that is what finishing a match does.
        await using (var context = AContext())
        {
            var match = await context.Matches.SingleAsync(row => row.Id == matchId);

            context.MatchPlayerStatistics.Add(StatisticsOf(match, playerId, teamId, TheGoodEvening()));
            context.MatchPlayerStatistics.Add(
                StatisticsOf(match, otherPlayerId, otherTeamId, TheCameo(), finalMinute: 94));

            await context.SaveChangesAsync();
        }

        await using var check = AContext();

        var stored = await check.MatchPlayerStatistics
            .Where(statistics => statistics.MatchId == matchId)
            .ToListAsync();

        Assert.Equal(2, stored.Count);

        var rated = stored.Single(statistics => statistics.Rating is not null);
        var cameo = stored.Single(statistics => statistics.Rating is null);

        // The number, to the tenth the engine rounded it to, and not one digit more: a card
        // with two decimals is a card claiming to know more about a man than a match can say.
        Assert.Equal(MatchRating.Of(TheGoodEvening(), 90, 90), rated.Rating);
        Assert.Equal(Math.Round(rated.Rating!.Value, 1), rated.Rating.Value);

        // The tally beside it, so the number can be read rather than believed.
        Assert.Equal(2, rated.Goals);
        Assert.Equal(1, rated.Assists);
        Assert.Equal(3, rated.ShotsOnTarget);
        Assert.Equal(1, rated.ShotsOffTarget);
        Assert.Equal(4, rated.DuelsWon);
        Assert.Equal(2, rated.DuelsLost);
        Assert.Equal(1, rated.CornersWon);
        Assert.Equal(90, rated.MinutesPlayed);

        // Four minutes is a cameo, and a cameo has no rating. A zero here would be the worst
        // of the three answers: it would put the worst player of the match on a card that
        // says six, and it would drag the season's average down with him.
        Assert.Equal(4, cameo.MinutesPlayed);
        Assert.Null(cameo.Rating);
    }

    [Fact]
    public async Task TheBandIsReadOffTheStoredNumberAndNotOffTheColumn()
    {
        // There is no band column, and that is deliberate: a band is a reading of a number and
        // storing it beside the number is a second thing to keep in step with the first. This
        // is the assertion that the reading is done from the value and reaches the same answer
        // the engine reached when it wrote it.
        var world = await AWorld();
        var (matchId, playerId, teamId, otherPlayerId, otherTeamId) = world;

        await using (var context = AContext())
        {
            var match = await context.Matches.SingleAsync(row => row.Id == matchId);
            context.MatchPlayerStatistics.Add(StatisticsOf(match, playerId, teamId, TheGoodEvening()));
            await context.SaveChangesAsync();
        }

        await using var check = AContext();

        var stored = await check.MatchPlayerStatistics
            .SingleAsync(statistics => statistics.MatchId == matchId);

        Assert.Equal(MatchRating.BandOf(stored.Rating), MatchRating.BandOf(MatchRating.Of(TheGoodEvening(), 90, 90)));
    }

    // --- The world ---------------------------------------------------------------

    private async Task<(Guid MatchId, Guid PlayerId, Guid TeamId, Guid OtherPlayerId, Guid OtherTeamId)> AWorld()
    {
        await using (var context = AContext())
        {
            await new DatabaseSeeder(
                context,
                Options.Create(new DatabaseSeedOptions()),
                NullLogger<DatabaseSeeder>.Instance).SeedAsync();
        }

        await using var check = AContext();

        // The edition that has clubs in it, rather than the first one an arbitrary guid
        // ordering lands on. Not every edition in a seeded world carries participants — a cup
        // drawn later carries none until it is drawn — so "the first competition season" was
        // a coin between an edition with sixteen clubs and one with none, and the test failed
        // on whichever side of that the ordering happened to fall this time.
        var clubs = await check.CompetitionParticipants
            .GroupBy(row => row.CompetitionSeasonId)
            .Where(group => group.Count() >= 2)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                CompetitionSeasonId = group.Key,
                TeamIds = group.OrderBy(row => row.TeamId).Select(row => row.TeamId).Take(2).ToList(),
            })
            .FirstAsync();

        var competitionSeasonId = clubs.CompetitionSeasonId;

        // Two men, each with his own club, rather than two out of one squad. A statistics row
        // belongs to a player and a club rather than to the match's two sides, and reaching
        // into a single squad for a second name meant the test failed whenever the club it
        // happened to pick first had one live contract rather than twenty-three — which is a
        // fact about a seed nobody reading this test would guess was load-bearing.
        var twoMen = await check.TeamMemberships
            .Where(row => row.EndDate == null)
            .OrderBy(row => row.TeamId)
            .ThenBy(row => row.PlayerId)
            .Select(row => new { row.PlayerId, row.TeamId })
            .Take(2)
            .ToListAsync();

        var round = Round.Create(competitionSeasonId, number: 1);
        var fixture = Fixture.Create(round.Id, clubs.TeamIds[0], clubs.TeamIds[1]);
        var match = Match.Create(fixture.Id, clubs.TeamIds[0], clubs.TeamIds[1]);

        check.Rounds.Add(round);
        check.Fixtures.Add(fixture);
        check.Matches.Add(match);
        await check.SaveChangesAsync();

        return (match.Id, twoMen[0].PlayerId, twoMen[0].TeamId, twoMen[1].PlayerId, twoMen[1].TeamId);
    }

    private static MatchPlayerStatistics StatisticsOf(
        Match match,
        Guid playerId,
        Guid teamId,
        MatchPlayerSnapshot player,
        int finalMinute = 90)
    {
        var statistics = MatchPlayerStatistics.Create(match.Id, playerId, teamId, seasonId: null);
        statistics.ApplyFrom(player, started: true, wasOnBenchUnused: false, finalMinute: finalMinute);

        return statistics;
    }

    /// <summary>A striker who scored twice and was busy all evening.</summary>
    private static MatchPlayerSnapshot TheGoodEvening()
    {
        var player = ASnapshotOf(Position.ATT, outfield: 78);
        player.PlayedInMatch = true;
        player.MatchGoals = 2;
        player.Performance.RecordShot(onTarget: true, chance: 0.30);
        player.Performance.RecordShot(onTarget: true, chance: 0.30);
        player.Performance.RecordShot(onTarget: true, chance: 0.40);
        player.Performance.RecordShot(onTarget: false, chance: 0.60);

        for (var duel = 0; duel < 6; duel++)
        {
            player.Performance.RecordDuel(won: duel < 4, chance: 0.5);
        }

        player.Performance.RecordAssist();
        player.Performance.RecordCorner();
        return player;
    }

    /// <summary>Four minutes off the bench and one honest touch.</summary>
    private static MatchPlayerSnapshot TheCameo()
    {
        var player = ASnapshotOf(Position.MID, outfield: 70);
        player.PlayedInMatch = true;
        player.EnteredAtMinute = 90;
        player.LeftAtMinute = 94;
        player.Performance.RecordInvolvement();
        return player;
    }

    private static MatchPlayerSnapshot ASnapshotOf(Position position, int outfield)
    {
        var isKeeper = position == Position.GK;
        var player = Player.Create(
            "Zé da bola", 26, position,
            speed: outfield, accuracy: outfield, dribbling: outfield, heading: outfield,
            strength: outfield,
            goalkeeperPower: isKeeper ? outfield : 0,
            reflexes: isKeeper ? outfield : 0,
            stamina: outfield, potential: 85);

        var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), Guid.NewGuid(), energy: 88);

        return MatchPlayerSnapshot.FromPlayerSeasonState(player, state);
    }
}
