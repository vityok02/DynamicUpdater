using DynamicUpdater.Host.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DynamicUpdater.Host.Middlewares;

public sealed class GlobalExceptionHandler
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception,
            "Unhandled exception: {Message}. TraceId: {TraceId}",
            exception.Message,
            httpContext.TraceIdentifier);

        var descriptor = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = descriptor.Status,
            Title = descriptor.Title,
            Detail = _env.IsDevelopment()
                ? exception.ToString()
                : descriptor.Detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = descriptor.Status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response
            .WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ExceptionMappingResult MapException(Exception exception)
        => exception switch
        {
            DynamicModuleNotLoadedException => new(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                "The requested dynamic is currently unavailable. Please try again later."),

            DynamicConfigurationException => new(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "The system encountered a configuration error within the dynamic."),

            OperationCanceledException => new(
                StatusCodes.Status499ClientClosedRequest,
                "Client Closed Request",
                "The request was timed out or cancelled by the client."),

            _ => new(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please contact support with your Trace ID.")
        };

    private readonly record struct ExceptionMappingResult(
        int Status,
        string Title,
        string Detail);
}