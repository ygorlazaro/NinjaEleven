using Microsoft.EntityFrameworkCore;
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
/// The shape each side went out in, as a club's own history reads it.
///
/// <para>
/// A match records the home shape in one column and the away shape in another, and a club's
/// line of its own history has to arrive the right way up whichever of the two it was: the
/// manager of the away club reading his own line and finding the home club's shape in it would
/// be told he went out in somebody else's system, which is the one answer the tactics board
/// cannot give. The flip lives in the projection rather than in the caller because the whole
/// line is already flipped — the two goals are too — and a line whose goals are in the club's
/// order and whose shapes are in the fixture's is a line that lies about half of itself.
/// </para>
///
/// <para>
/// This runs against the database that will actually hold it: the projection is a join over
/// six tables and an in-memory provider would happily accept a translation PostgreSQL refuses.
/// </para>
/// </summary>
[Collection("Sequential")]
public class MatchTacticHistoryTests : IAsyncLifetime
{
    private const string Server =
        "Host=localhost;Port=5433;Username=postgres;Password=postgres";

    private readonly string _database = $"tactics_test_{Guid.NewGuid():N}";
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

    [Fact]
    public async Task Each_club_reads_its_own_shape_and_the_club_it_played_against()
    {
        var world = await AWorld(new PlayedIn("442", "352", GoalsForHome: 2, GoalsForAway: 1));

        var homeLine = await world.Repository.GetTeamHistoryAsync(world.Home.Id, 5);
        var awayLine = await world.Repository.GetTeamHistoryAsync(world.Away.Id, 5);

        // The two lines are one match read from each side, so the shapes come back as mirrors
        // rather than as one club's shape sitting on both rows.
        Assert.Equal("442", homeLine.Single().TacticCode);
        Assert.Equal("352", homeLine.Single().OpponentTacticCode);

        Assert.Equal("352", awayLine.Single().TacticCode);
        Assert.Equal("442", awayLine.Single().OpponentTacticCode);

        // And the goals arrive flipped with them, which is the same fact about the same line:
        // the away club's 2-1 is its own two and the other's one.
        Assert.Equal((2, 1), (homeLine.Single().GoalsFor, homeLine.Single().GoalsAgainst));
        Assert.Equal((1, 2), (awayLine.Single().GoalsFor, awayLine.Single().GoalsAgainst));

        Assert.Equal(world.MatchId, homeLine.Single().MatchId);
        Assert.Equal(world.MatchId, awayLine.Single().MatchId);
    }

    [Fact]
    public async Task A_match_played_before_either_shape_was_recorded_reports_no_shape()
    {
        // The away column arrived after the home one, so a match from before it has a shape
        // written down for one side and not for the other. Empty is the honest reading of that
        // column: a reader that turned the gap into a shape would be claiming a club went out
        // in a system nobody chose for it.
        var world = await AWorld(new PlayedIn("", "", GoalsForHome: 0, GoalsForAway: 0));

        var record = Assert.Single(await world.Repository.GetTeamHistoryAsync(world.Home.Id, 5));

        Assert.Equal(string.Empty, record.TacticCode);
        Assert.Equal(string.Empty, record.OpponentTacticCode);
    }

    [Fact]
    public async Task A_match_that_is_still_being_played_is_not_part_of_a_club_s_form()
    {
        var world = await AWorld(new PlayedIn("442", "352", GoalsForHome: 0, GoalsForAway: 0));

        await using (var context = AContext())
        {
            var match = await context.Matches.SingleAsync(row => row.Id == world.MatchId);
            match.Abandon();
            await context.SaveChangesAsync();
        }

        // An abandoned match is one that never reached the final whistle, and its score is a
        // score that was not played. It stays out of the form guide for the same reason it
        // keeps its fixture owed.
        Assert.Empty(await world.Repository.GetTeamHistoryAsync(world.Home.Id, 5));
    }

    private NinjaElevenDbContext AContext()
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>();
        NinjaElevenDbContext.Configure(options, ConnectionString);

        return new NinjaElevenDbContext(options.Options);
    }

    // --- The world ---------------------------------------------------------------

    /// <summary>How the one match in it was played.</summary>
    private record PlayedIn(string HomeTactic, string AwayTactic, int GoalsForHome, int GoalsForAway);

    private sealed record World(
        Team Home,
        Team Away,
        Guid MatchId,
        MatchRepository Repository);

    /// <summary>
    /// Two clubs, one round and one match in it, finished in the shape and the score given.
    ///
    /// <para>
    /// It is built by hand rather than seeded because the whole point of the test is the six
    /// tables the projection joins: a world with an edition, a competition, a season and a
    /// round in it is the least the query can be honest about.
    /// </para>
    /// </summary>
    private async Task<World> AWorld(PlayedIn played)
    {
        var home = Team.Create("Atlético do Bairro", "ATB", "#123456", "#FFFFFF", rating: 55);
        var away = Team.Create("Ferroviária", "FER", "#654321", "#000000", rating: 52);

        var competition = Competition.Create("Campeonato", CompetitionType.League);
        var season = Season.Create(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var division = Division.Create(null, tier: 1);
        var edition = CompetitionSeason.Create(competition.Id, season.Id, division.Id);

        var round = Round.Create(edition.Id, number: 1);
        var fixture = Fixture.Create(round.Id, home.Id, away.Id);

        var match = Match.Create(fixture.Id, home.Id, away.Id);
        match.RecordTactic(played.HomeTactic);
        match.RecordAwayTactic(played.AwayTactic);

        for (var goal = 0; goal < played.GoalsForHome; goal++) match.RegisterGoal(isHome: true);
        for (var goal = 0; goal < played.GoalsForAway; goal++) match.RegisterGoal(isHome: false);

        match.Finish();

        await using (var context = AContext())
        {
            context.Competitions.Add(competition);
            context.Seasons.Add(season);
            context.Divisions.Add(division);
            context.CompetitionSeasons.Add(edition);
            context.Teams.AddRange(home, away);
            context.Rounds.Add(round);
            context.Fixtures.Add(fixture);
            context.Matches.Add(match);
            await context.SaveChangesAsync();
        }

        return new World(home, away, match.Id, new MatchRepository(AContext()));
    }
}