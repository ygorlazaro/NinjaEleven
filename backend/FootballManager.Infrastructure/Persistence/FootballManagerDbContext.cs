using FootballManager.Domain.Common;
using FootballManager.Domain.Competitions;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Players;
using FootballManager.Domain.Seasons;
using FootballManager.Domain.Teams;
using Microsoft.EntityFrameworkCore;

namespace FootballManager.Infrastructure.Persistence;

/// <summary>
/// EF Core entry point. It only maps the domain to PostgreSQL: it holds no business
/// rule and it is never consumed directly by controllers or hubs. Queries flow
/// through the repositories implemented in this same layer.
/// </summary>
public class FootballManagerDbContext : DbContext
{
    public FootballManagerDbContext(DbContextOptions<FootballManagerDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerSeasonState> PlayerSeasonStates => Set<PlayerSeasonState>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionSeason> CompetitionSeasons => Set<CompetitionSeason>();
    public DbSet<CompetitionParticipant> CompetitionParticipants => Set<CompetitionParticipant>();
    public DbSet<Round> Rounds => Set<Round>();

    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<MatchStatistics> MatchStatistics => Set<MatchStatistics>();

    public DbSet<Name> Names => Set<Name>();
    public DbSet<Surname> Surnames => Set<Surname>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FootballManagerDbContext).Assembly);
    }
}
