using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Repositories;
using Xunit;

namespace NinjaEleven.IntegrationTests;

/// <summary>
/// A small world with one club, its men, and a hand on the calendar — the three things the
/// training rules are asked about.
///
/// <para>
/// The clock is a hand the test turns rather than the machine's own time, because "today" is
/// the axis the whole allowance turns on: a test that could not move it could only ever prove
/// one day's rule, and the difference between a matchday and a rest day is the rule.
/// </para>
/// </summary>
internal sealed class TrainingWorld(NinjaElevenDbContext db) : IDisposable
{
    public static readonly Guid TheClub = Guid.NewGuid();

    public IPlayerRepository Players { get; } = new PlayerRepository(db);
    public ISeasonRepository Seasons { get; } = new SeasonRepository(db);
    public IMatchDayRepository MatchDays { get; } = new MatchDayRepository(db);
    public IRoundRepository Rounds { get; } = new RoundRepository(db);
    public IFixtureRepository Fixtures { get; } = new FixtureRepository(db);
    public ITrainingSessionRepository Sessions { get; } = new TrainingSessionRepository(db);
    public IFinanceRepository Finance { get; } = new FinanceRepository(db);
    public ITeamRepository Teams { get; } = new TeamRepository(db);
    public IUnitOfWork UnitOfWork { get; } = new TestUnitOfWork(db);
    public HandClock Clock { get; } = new(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

    public Guid SeasonId { get; private set; }

    public TrainingService Training() => new(
        Players,
        Seasons,
        Teams,
        UnitOfWork,
        Clock,
        Sessions,
        MatchDays,
        Fixtures,
        new FinanceService(
            Finance,
            Teams,
            Players,
            Fixtures,
            Rounds,
            MatchDays,
            Seasons,
            Inbox(),
            UnitOfWork,
            NullLogger<FinanceService>.Instance));

    /// <summary>
    /// The box over the same context. The club in this world is not a manager's club, so every
    /// report handed to it is dropped by the service itself — which is the point: the fee has
    /// to be written whether or not anybody is told about it.
    /// </summary>
    private InboxService Inbox() => new(
        new InboxMessageRepository(db),
        Teams,
        new ManagedClubReader(db),
        UnitOfWork,
        NullLogger<InboxService>.Instance);

    /// <summary>
    /// Puts the club on the pitch on a day, which is the whole difference between one session
    /// and two. The calendar is drawn the way the real one is — a matchday, a window on it, a
    /// fixture in the window — because the question is asked by walking those three and a
    /// fixture conjured out of thin air would prove the walk nothing.
    /// </summary>
    public async Task GivenAMatchOnAsync(DateOnly day)
    {
        var season = await Seasons.GetAsync(SeasonId);
        var competitionId = db.Competitions.First().Id;

        var edition = CompetitionSeason.Create(competitionId, season.Id);
        db.CompetitionSeasons.Add(edition);

        var matchDay = MatchDay.Create(season.Id, day.DayNumber, day);
        db.MatchDays.Add(matchDay);

        var round = Round.Create(edition.Id, day.DayNumber);
        round.ScheduleOn(matchDay.Id);
        db.Rounds.Add(round);

        db.Fixtures.Add(Fixture.Create(round.Id, TheClub, Guid.NewGuid()));

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Gives the club a book to be charged against, the way a founded club has one.
    /// </summary>
    public async Task GivenAnOpenedBookAsync(decimal capital = 1_000_000m)
    {
        db.FinanceMovements.Add(FinanceMovement.Seed(TheClub, SeasonId, capital));

        await db.SaveChangesAsync();
    }

    public void Dispose() => db.Dispose();

    public sealed class TestUnitOfWork(NinjaElevenDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A clock a test moves by hand.</summary>
    public sealed class HandClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void AdvanceTo(DateOnly day) => UtcNow = new DateTimeOffset(
            day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    }

    /// <summary>
    /// A world with one started season and a club's worth of men in it, every one of them on
    /// a hundred per cent so that a test which is about the allowance is never refused for
    /// want of energy instead.
    /// </summary>
    public static async Task<TrainingWorld> GivenAsync(
        IEnumerable<Player> squad,
        int seasons = 1,
        Player? withoutASeason = null)
    {
        var options = new DbContextOptionsBuilder<NinjaElevenDbContext>()
            .UseInMemoryDatabase($"Training-{Guid.NewGuid()}")
            .Options;

        var db = new NinjaElevenDbContext(options);

        var players = squad.ToList();
        if (withoutASeason is not null) players.Add(withoutASeason);

        db.Players.AddRange(players);

        // A competition for the fixtures to hang from, since a window belongs to an edition.
        db.Competitions.Add(Competition.Create("Brasileirão", CompetitionType.League));

        for (var number = 1; number <= seasons; number++)
        {
            var season = Season.Create(number, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            season.Start();
            db.Seasons.Add(season);

            foreach (var player in players.Where(player => withoutASeason is null || player.Id != withoutASeason.Id))
            {
                var state = PlayerSeasonState.Create(player.Id, season.Id, TheClub, energy: 100);
                db.PlayerSeasonStates.Add(state);

                // Signed on the wage the man would have been valued at, because a contract
                // with no wage is not a cheap one — it is an unpayable one, and the fee is a
                // share of it. A world that gave its players free contracts would make every
                // test about what a session costs a test about a session that cannot happen.
                db.TeamMemberships.Add(TeamMembership.Create(
                    player.Id, TheClub, season.StartDate, number,
                    wage: PlayerValuation.SeasonWage(player, state)));
            }

            await db.SaveChangesAsync();
        }

        // A world with no season at all is a world the sheet must refuse rather than guess
        // at, so the season is named when there is one and left empty when there is not.
        var world = new TrainingWorld(db)
        {
            SeasonId = db.Seasons.OrderByDescending(season => season.Number)
                .Select(season => (Guid?)season.Id)
                .FirstOrDefault() ?? Guid.Empty
        };

        return world;
    }

    /// <summary>
    /// A man of a given shape, at an age and a potential that make his wage a real number
    /// rather than a rounding error, so that a test about what a session costs is testing the
    /// fee and not the seed.
    /// </summary>
    public static Player APlayer(string name, int age, Position position, int potential) =>
        Player.Create(
            name, age, position,
            speed: 55, accuracy: 50, dribbling: 52, heading: 58, strength: 54,
            goalkeeperPower: position == Position.GK ? 60 : 0,
            reflexes: position == Position.GK ? 62 : 0,
            stamina: 70,
            potential: potential);
}
