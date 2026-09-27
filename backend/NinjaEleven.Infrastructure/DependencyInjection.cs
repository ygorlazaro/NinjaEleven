using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using NinjaEleven.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NinjaEleven.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "NinjaEleven";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");

        services.AddDbContext<NinjaElevenDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__ef_migrations_history"))
                .UseSnakeCaseNamingConvention());

        services.Configure<DatabaseSeedOptions>(configuration.GetSection(DatabaseSeedOptions.SectionName));
        services.AddScoped<IDataSeeder, DatabaseSeeder>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ICompetitionRepository, CompetitionRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IDivisionRepository, DivisionRepository>();
        services.AddScoped<IMatchDayRepository, MatchDayRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<IFixtureRepository, FixtureRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ICupTieRepository, CupTieRepository>();
        services.AddScoped<ITrophyRepository, TrophyRepository>();
        services.AddScoped<IFinanceRepository, FinanceRepository>();

        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TeamService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<SeasonService>();
        services.AddScoped<SeasonCalendarService>();
        services.AddScoped<CompetitionService>();
        services.AddScoped<RoundService>();
        services.AddScoped<FixtureService>();
        // The cup advances on the finish of a match rather than on a timer, so it is a scoped
        // service the match service calls, not a hosted one that sweeps the ties on a schedule.
        services.AddScoped<CupProgressionService>();
        services.AddScoped<MatchService>();
        services.AddScoped<LeagueService>();
        services.AddScoped<StandingsService>();
        services.AddScoped<AttendanceContextFactory>();

        services.AddSingleton<IMatchSessionRegistry, MatchSessionRegistry>();

        return services;
    }

    /// <summary>
    /// Brings the database up to date and, when it is asked for, seeds the starting world.
    /// The API never talks to the database outside of this step, controllers and hubs only
    /// use services.
    /// </summary>
    /// <param name="seed">
    /// Whether the world should be created. It is false by default and it is false on
    /// purpose: a manager who has decided what his world looks like should be able to start
    /// the API and find it exactly as he left it, and a developer working on the schema
    /// should be able to start the API and find an empty database. Seeding is something a
    /// human asks for, not something that happens because the process booted.
    /// </param>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        bool seed = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DependencyInjection));
        var dbContext = provider.GetRequiredService<NinjaElevenDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database schema is up to date.");

        if (!seed)
        {
            logger.LogInformation(
                "Seeding not requested. The database was left as it is; pass --seed to create the starting world.");
            return;
        }

        await provider.GetRequiredService<IDataSeeder>().SeedAsync(cancellationToken);
    }
}
