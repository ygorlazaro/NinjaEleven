using System.Text.Json.Serialization;
using NinjaEleven.Api;
using NinjaEleven.Api.Middleware;
using NinjaEleven.Api.Realtime;
using NinjaEleven.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text;

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

var jwtOptions = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtOptions);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions["Issuer"],
        ValidAudience = jwtOptions["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions["Key"] ?? throw new InvalidOperationException("JWT key is missing.")))
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            if (context.Response.HasStarted) return Task.CompletedTask;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Authentication is required to access this resource."
            };
            problem.Extensions["code"] = "Unauthorized";
            return context.Response.WriteAsJsonAsync(problem, context.HttpContext.RequestAborted);
        }
    };
});

builder.Services.AddAuthorization();

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

// Seeding is a thing a human asks for and not a thing that happens because the process
// booted, so the flag has to actually be read from the command line: the initializer has
// always taken one, and nothing was passing it, so a world could only be drawn by editing
// this line.
await app.Services.InitializeDatabaseAsync(args.Contains("--seed"));

app.UseExceptionHandler();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MatchHub>("/matchHub");

app.Run();
