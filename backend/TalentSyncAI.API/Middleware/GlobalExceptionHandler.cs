using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TalentSyncAI.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path,
            Detail = exception.Message
        };

        switch (exception)
        {
            case ArgumentException or InvalidOperationException:
                problemDetails.Title = "Bad Request";
                problemDetails.Status = StatusCodes.Status400BadRequest;
                break;

            case UnauthorizedAccessException:
                problemDetails.Title = "Unauthorized";
                problemDetails.Status = StatusCodes.Status401Unauthorized;
                break;

            case KeyNotFoundException:
                problemDetails.Title = "Not Found";
                problemDetails.Status = StatusCodes.Status404NotFound;
                break;

            default:
                problemDetails.Title = "Internal Server Error";
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
