using FootballManager.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FootballManager.Api.Middleware;

/// <summary>
/// Translates domain errors into HTTP responses. The body keeps a stable machine
/// readable <c>code</c> so the frontend never has to interpret a message to discover a
/// rule.
/// </summary>
public class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var statusCode = exception switch
        {
            EntityNotFoundException => StatusCodes.Status404NotFound,
            DomainValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status409Conflict
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(domainException, "Domain error {Code}", domainException.Code);
        }
        else
        {
            _logger.LogWarning(domainException, "Domain error {Code}", domainException.Code);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = domainException.Code,
            Detail = domainException.Message,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = domainException.Code;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
