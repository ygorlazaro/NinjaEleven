using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FootballManager.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core tooling (`dotnet ef`) to build a context at design time,
/// without booting the API. The connection string is the same one the API uses.
/// </summary>
public class FootballManagerDbContextFactory : IDesignTimeDbContextFactory<FootballManagerDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=football_manager;Username=postgres;Password=postgres";

    public FootballManagerDbContext CreateDbContext(string[] args)
    {
        var infrastructureDirectory = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(infrastructureDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(Path.Combine(infrastructureDirectory, "..", "FootballManager.Api", "appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName)
            ?? configuration["ConnectionStrings:FootballManager"]
            ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<FootballManagerDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();

        return new FootballManagerDbContext(optionsBuilder.Options);
    }
}
