using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Infrastructure.Persistence;

/// <summary>
/// EF Core entry point. It only maps the domain to PostgreSQL: it holds no business
/// rule and it is never consumed directly by controllers or hubs. Queries flow
/// through the repositories implemented in this same layer.
/// </summary>
public class NinjaElevenDbContext : DbContext
{
    public NinjaElevenDbContext(DbContextOptions<NinjaElevenDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerSeasonState> PlayerSeasonStates => Set<PlayerSeasonState>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<Stadium> Stadiums => Set<Stadium>();
    public DbSet<FinanceMovement> FinanceMovements => Set<FinanceMovement>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<SponsorContract> SponsorContracts => Set<SponsorContract>();
    public DbSet<Manager> Managers => Set<Manager>();

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

    public DbSet<Name> Names => Set<Name>();
    public DbSet<Surname> Surnames => Set<Surname>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NinjaElevenDbContext).Assembly);
    }
}
