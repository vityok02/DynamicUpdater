using DynamicUpdater.Host.Infrastructure.DynamicManagement;

namespace DynamicUpdater.Host.Middlewares;

public sealed class DynamicModuleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDynamicContainer _dynamicContainer;
    private readonly ILogger<DynamicModuleMiddleware> _logger;

    public DynamicModuleMiddleware(
        RequestDelegate next,
        IDynamicContainer dynamicContainer,
        ILogger<DynamicModuleMiddleware> logger)
    {
        _next = next;
        _dynamicContainer = dynamicContainer;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/data"))
        {
            await _next(context);
            return;
        }

        var dynamicModule = _dynamicContainer.CurrentModule;

        if (dynamicModule is null)
        {
            _logger.LogWarning(
                "No dynamic module is currently loaded to handle the request.");

            await HandleErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Service is initializing or updating.");

            return;
        }

        var dynamicModuleName = dynamicModule.Assembly.FullName;

        _logger.LogInformation(
            "Processing request via dynamic module: {DynamicModuleName}",
            dynamicModuleName);

        await using var scope = dynamicModule.ServiceProvider
            .CreateAsyncScope();

        try
        {
            var cancellationToken = context.RequestAborted;

            _logger.LogInformation(
                "Request successfully handled by {DynamicModuleName}",
                dynamicModuleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An unhandled exception occurred during dynamic module execution.");

            await HandleErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Dynamic module execution failed.");

            return;
        }

        await _next(context);
    }

    private static async Task HandleErrorAsync(
        HttpContext context,
        int statusCode,
        string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;

        await context.Response
            .WriteAsJsonAsync(new
            {
                error = message,
            });
    }
}
