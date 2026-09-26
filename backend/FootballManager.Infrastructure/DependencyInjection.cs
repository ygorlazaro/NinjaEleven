using FootballManager.Application.Abstractions;
using FootballManager.Application.Matches;
using FootballManager.Application.Repositories;
using FootballManager.Application.Services;
using FootballManager.Infrastructure.Persistence;
using FootballManager.Infrastructure.Persistence.Seeding;
using FootballManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FootballManager.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FootballManager";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");

        services.AddDbContext<FootballManagerDbContext>(options =>
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
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<IFixtureRepository, FixtureRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();

        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TeamService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<SeasonService>();
        services.AddScoped<CompetitionService>();
        services.AddScoped<RoundService>();
        services.AddScoped<FixtureService>();
        services.AddScoped<MatchService>();
        services.AddScoped<LeagueService>();

        services.AddSingleton<IMatchSessionRegistry, MatchSessionRegistry>();

        return services;
    }

    /// <summary>
    /// Brings the database up to date and seeds the starting world. The API never talks
    /// to the database outside of this step, controllers and hubs only use services.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DependencyInjection));
        var dbContext = provider.GetRequiredService<FootballManagerDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database schema is up to date.");

        await provider.GetRequiredService<IDataSeeder>().SeedAsync(cancellationToken);
    }
}
