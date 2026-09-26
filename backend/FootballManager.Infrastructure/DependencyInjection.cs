using FootballManager.Application.Abstractions;
using FootballManager.Infrastructure.Persistence;
using FootballManager.Infrastructure.Persistence.Seeding;
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
