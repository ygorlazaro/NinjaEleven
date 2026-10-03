using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using NinjaEleven.Domain.Users;
using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;

namespace NinjaEleven.Infrastructure.Persistence;

/// <summary>
/// EF Core entry point. It only maps the domain to PostgreSQL: it holds no business
/// rule and it is never consumed directly by controllers or hubs. Queries flow
/// through the repositories implemented in this same layer.
/// </summary>
public class NinjaElevenDbContext : DbContext
{
    public NinjaElevenDbContext(DbContextOptions<NinjaElevenDbContext> options) : base(options) { }

    /// <summary>
    /// How this context talks to PostgreSQL, in one place.
    ///
    /// <para>
    /// The column names are snake_case and the migrations history has a name, and both of
    /// those used to be repeated at every call site: the API's registration and the design-time
    /// factory. Two call sites is one too many, because a context built by a third — a test,
    /// a script, a tool — without them is a context whose model believes the columns are
    /// called <c>Id</c> and <c>CompetitionSeasonId</c> while the database says <c>id</c> and
    /// <c>competition_season_id</c>. It fails on the first query with a message about a
    /// column nobody has ever heard of, and the message points at the database rather than at
    /// the omission.
    /// </para>
    ///
    /// <para>
    /// A context that reaches the database through this method is the world's context. One
    /// that does not is a different model wearing the same class, which is the thing this
    /// method exists to make impossible to do by accident.
    /// </para>
    /// </summary>
    /// <typeparam name="TBuilder">The builder being configured.</typeparam>
    /// <param name="options">The builder being configured.</param>
    /// <param name="connectionString">Where the world is.</param>
    public static void Configure<TBuilder>(TBuilder options, string connectionString)
        where TBuilder : DbContextOptionsBuilder
    {
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"));

        options.UseSnakeCaseNamingConvention();
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerSeasonState> PlayerSeasonStates => Set<PlayerSeasonState>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<Stadium> Stadiums => Set<Stadium>();
    public DbSet<TeamFanBase> TeamFanBases => Set<TeamFanBase>();
    public DbSet<StadiumConstruction> StadiumConstructions => Set<StadiumConstruction>();
    public DbSet<FinanceMovement> FinanceMovements => Set<FinanceMovement>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<SponsorContract> SponsorContracts => Set<SponsorContract>();
    public DbSet<Manager> Managers => Set<Manager>();
    public DbSet<User> Users => Set<User>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<ClubEvent> ClubEvents => Set<ClubEvent>();

    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionSeason> CompetitionSeasons => Set<CompetitionSeason>();
    public DbSet<CompetitionParticipant> CompetitionParticipants => Set<CompetitionParticipant>();
    public DbSet<MatchDay> MatchDays => Set<MatchDay>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<CupTie> CupTies => Set<CupTie>();
    public DbSet<TrophyAward> TrophyAwards => Set<TrophyAward>();

    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<MatchStatistics> MatchStatistics => Set<MatchStatistics>();
    public DbSet<MatchPlayerStatistics> MatchPlayerStatistics => Set<MatchPlayerStatistics>();
    public DbSet<Domain.Matches.TeamMatchPlan> TeamMatchPlans => Set<Domain.Matches.TeamMatchPlan>();

    public DbSet<Name> Names => Set<Name>();
    public DbSet<Surname> Surnames => Set<Surname>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NinjaElevenDbContext).Assembly);
    }
}
