using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Matches;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Infrastructure.Persistence;
using NinjaEleven.Infrastructure.Persistence.Seeding;
using NinjaEleven.Infrastructure.Repositories;
using NinjaEleven.Infrastructure.Security;
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
            NinjaElevenDbContext.Configure(options, connectionString));

        services.Configure<DatabaseSeedOptions>(configuration.GetSection(DatabaseSeedOptions.SectionName));
        services.Configure<WorldExecutionOptions>(configuration.GetSection(WorldExecutionOptions.SectionName));
        services.AddScoped<IDataSeeder, DatabaseSeeder>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();

        // Training is kept as rows rather than as a tally on the club, because the day's
        // allowance is counted from the sessions that were actually run. A counter would be a
        // second answer to the same question, and the two would only agree on the day nothing
        // went wrong.
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
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
        services.AddScoped<ITransferRepository, TransferRepository>();
    services.AddScoped<ISponsorRepository, SponsorRepository>();
    services.AddScoped<ISponsorContractRepository, SponsorContractRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IManagerRepository, ManagerRepository>();
        // Which clubs a person is in charge of. The world walk asks it before it plays a
        // window, because a match somebody is waiting to watch is not a match it may play
        // for him — whoever is walking the world, be it a scheduler or a hand.
        services.AddScoped<IManagedClubReader, ManagedClubReader>();
        services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
        services.AddScoped<IRoundExecutionStore, RoundExecutionStore>();
        // The plan a manager lays for his club's next match. The kick-off asks it for a club
        // whose manager is not there, which is the whole reason it is a row rather than a pair
        // of arguments that arrive with the request.
        services.AddScoped<ITeamMatchPlanRepository, TeamMatchPlanRepository>();

        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TeamService>();
        // The board a manager reads before a match and the order he leaves on it. The
        // kick-off asks it for the order, which is why it is a service and not a pair of
        // arguments that arrive with the request: a match opened by the world's schedule has
        // no request to arrive with.
        services.AddScoped<TacticsService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<SeasonService>();
        services.AddScoped<SeasonCalendarService>();
        services.AddScoped<SeasonCloseService>();
        // The same instance under the narrow door the walking of the world uses: whether a
        // season is over and what closing it does are the two questions the execution service
        // asks, and it must not be able to answer them with a second close of its own.
        services.AddScoped<ISeasonCloser>(provider => provider.GetRequiredService<SeasonCloseService>());
        // And the same instance under the other narrow door the walking of the world uses: a
        // season nobody has drawn has no matchdays at all, so the walk draws it before asking
        // it what is due — and the calendar of a season a close has just opened is drawn by
        // the walk that comes back, not by whoever noticed.
        services.AddScoped<ISeasonCalendarBuilder>(provider => provider.GetRequiredService<SeasonCalendarService>());
        services.AddScoped<CompetitionService>();
        services.AddScoped<RoundService>();
        services.AddScoped<FixtureService>();
        // The cup advances on the finish of a match rather than on a timer, so it is a scoped
        // service the match service calls, not a hosted one that sweeps the ties on a schedule.
        services.AddScoped<CupProgressionService>();
        // The matchday service starts the matches of a day, and the starting of a match is the
        // match service's job, so the two need each other. The seam is closed here, with a
        // scope of its own per match: a headless match of another club is started through
        // exactly the same path a watched one is, and a whole wave of thirty-four matches
        // must not be started inside one unit of work.
        services.AddScoped(provider =>
        {
            var matchday = ActivatorUtilities.CreateInstance<MatchdayService>(provider);
            var scopes = provider.GetRequiredService<IServiceScopeFactory>();

            matchday.UseStarter(async (fixtureId, cancellationToken) =>
            {
                using var scope = scopes.CreateScope();
                var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
                var started = await matchService.StartAsync(
                    fixtureId, headless: true, cancellationToken: cancellationToken);

                return started.Accepted ? started.MatchId : null;
            });

            return matchday;
        });
        services.AddScoped<MatchService>();
        // The same instance under the narrow door the walking of a window uses: a match left
        // running on a decided fixture is a claim on a fixture the world can never play again,
        // and only the owner of the live sessions can close it.
        services.AddScoped<IMatchCleaner>(provider => provider.GetRequiredService<MatchService>());
        services.AddScoped<MatchContextService>();
        // The service that moves the world: what the calendar says is due, which fixtures of
        // it are left, and how to ask the match service to play one. It is a service like any
        // other, so the Scheduler, a person pressing a button and a test all reach the world
        // through the same door.
        services.AddScoped<CompetitionExecutionService>();
        // Each fixture is played in a scope of its own, because a match is a hundred commits
        // and a window is thirty-two matches: one unit of work for the whole window would
        // leave the change tracker holding every event of the day for as long as the day lasted.
        services.AddScoped<IHeadlessMatchPlayer, HeadlessMatchPlayer>();
        services.AddScoped<LeagueService>();
        services.AddScoped<StandingsService>();
        services.AddScoped<CupBracketService>();
        services.AddScoped<ScorerPrizeService>();
        services.AddScoped<SponsorOfferService>();
        services.AddScoped<ManagerService>();
        services.AddScoped<AuthService>();
        services.AddScoped<ClubRankingService>();
        services.AddScoped<CupDrawService>();
        services.AddSingleton<Random>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<TransferService>();
        services.AddScoped<RosterService>();
        // Training spends energy off a season state and puts a point on the man, so it is
        // registered next to the two services that own the other halves of that: the roster
        // that ages him and the match that tires him.
        services.AddScoped<TrainingService>();
        services.AddScoped<AttendanceContextFactory>();
        // The box is written by the engine, so it is a service the other services call rather
        // than a screen that composes its own news. It depends on no other service, which is
        // what lets the ledger, the market and the match all tell the manager something without
        // any of them having to know who else does.
        services.AddScoped<InboxService>();
        // The treasurer's weekly statement, which is the one message a manager gets about a
        // gate receipt or a wage bill — read in a week rather than one line at a time.
        services.AddScoped<StatementService>();

        services.AddSingleton<IMatchSessionRegistry, MatchSessionRegistry>();

        // The two singletons that make "now" and "which process is this" answerable from
        // inside a service. Both are per process and both are asked for rather than read:
        // a service that read the clock itself could only be checked by waiting, and a
        // process that could not name itself could not tell its own matches from another
        // process's.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IMatchHost, ProcessMatchHost>();

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
