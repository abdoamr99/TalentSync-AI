using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TalentSyncAI.Application.Common.Exceptions;

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
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        _logger.LogError(exception, "[TraceId: {TraceId}] Unhandled exception: {Message}", traceId, exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = traceId;

        switch (exception)
        {
            case AiServiceUnavailableException:
                problemDetails.Title = "Service Unavailable";
                problemDetails.Status = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Detail = "The AI processing service is currently unavailable. Please try again shortly.";
                break;

            case AiServiceBadResponseException:
                problemDetails.Title = "Bad Gateway";
                problemDetails.Status = StatusCodes.Status502BadGateway;
                problemDetails.Detail = "The upstream AI service returned an unprocessable response.";
                break;

            case ArgumentException or InvalidOperationException:
                problemDetails.Title = "Bad Request";
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Detail = exception.Message;
                break;

            case UnauthorizedAccessException:
                problemDetails.Title = "Unauthorized";
                problemDetails.Status = StatusCodes.Status401Unauthorized;
                problemDetails.Detail = exception.Message;
                break;

            case KeyNotFoundException:
                problemDetails.Title = "Not Found";
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Detail = exception.Message;
                break;

            default:
                problemDetails.Title = "Internal Server Error";
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Detail = "An unexpected internal server error occurred. Please contact support with the trace ID.";
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
