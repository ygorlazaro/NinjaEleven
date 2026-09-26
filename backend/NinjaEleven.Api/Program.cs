using System.Text.Json.Serialization;
using NinjaEleven.Api.Middleware;
using NinjaEleven.Api.Realtime;
using NinjaEleven.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

// Malformed requests (an id that is not a guid, a missing field, a bad enum value) must
// answer with the same machine readable contract as the business errors.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "ValidationFailed",
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["code"] = "ValidationFailed";

        return new BadRequestObjectResult(problem);
    };
});

const string CorsPolicy = "frontend";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "https://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    // The SignalR client negotiates with credentials, so the preflight of
    // POST /matchHub/negotiate is only answered when credentials are allowed. Without
    // this the browser refuses the response and the live match never connects.
    .AllowCredentials()
    .WithExposedHeaders("Content-Type")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// SignalR has its own JSON protocol, so the enum converter has to be registered for
// it as well. Without this the hub would push numeric event types while the REST API
// pushes names, and the frontend compares against names.
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IMatchBroadcaster, SignalRMatchBroadcaster>();
builder.Services.AddSingleton<MatchSimulator>();
builder.Services.AddHostedService<MatchLoopService>();

// Whoever issues a match command, the events it produced reach every follower of that
// match. The hub does it for the commands that arrive over the hub and the controller for
// the ones that arrive over REST, through the same seam so a manager's own substitution is
// narrated like every other event of the match.
builder.Services.AddScoped<MatchCommandPublisher>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseCors(CorsPolicy);

app.MapControllers();
app.MapHub<MatchHub>("/matchHub");

app.Run();
