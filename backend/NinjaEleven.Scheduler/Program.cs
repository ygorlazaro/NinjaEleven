using Microsoft.Extensions.Hosting;
using NinjaEleven.Infrastructure;
using NinjaEleven.Scheduler;

// The Scheduler is a process, not a layer.
//
// Everything it knows about the world is in the Application and Domain projects, which is the
// point of it being a separate executable rather than a hosted service inside the API: the
// world can be moved with no HTTP server running, no client connected and no signal of any
// kind, and the thing that moves it has no football in it at all.
//
// It is not a microservice either. There is one Application layer, one Domain and one
// database, and this process is a second way in — the API is a way for people to look at the
// world and this is the way the world moves itself.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddNinjaElevenScheduler(builder.Configuration);
builder.Services.AddHostedService<SchedulerAnnouncer>();

var host = builder.Build();

// Migrations are a thing a person does, so they are behind a flag: a scheduler that came up
// while somebody was halfway through a migration would be a second writer of the schema, and
// a world that is not running yet is a much smaller problem than a half-migrated one.
if (builder.Configuration.GetSection(SchedulerOptions.SectionName).Get<SchedulerOptions>()?.MigrateOnStartup == true)
{
    await host.Services.InitializeDatabaseAsync(seed: false);
}

await host.RunAsync();
