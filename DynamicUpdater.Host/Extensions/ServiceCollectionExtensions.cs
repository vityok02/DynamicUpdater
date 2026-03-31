using DynamicUpdater.Host.BackgroundServices;
using DynamicUpdater.Host.Infrastructure.AssemblyProvider;
using DynamicUpdater.Host.Infrastructure.DynamicManagement;
using DynamicUpdater.Host.Infrastructure.Http;
using DynamicUpdater.Host.Middlewares;
using DynamicUpdater.Host.Options;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace DynamicUpdater.Host.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenApi();

        services.AddOptions<AssemblyClientOptions>()
            .Bind(configuration.GetSection(AssemblyClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TimerOptions>()
            .Bind(configuration.GetSection(TimerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAssemblyProvider, AssemblyProvider>();
        services.AddSingleton<IDynamicContainer, DynamicContainer>();
        services.AddSingleton<DynamicModuleFactory>();

        services.AddAssemblyHttpClient();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddHealthChecks();

        services.AddHostedService<DynamicUpdateService>();

        services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .MinimumLevel.Information()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] [Host] {Message:lj}{NewLine}{Exception}");
        });

        return services;
    }

    private static IServiceCollection AddAssemblyHttpClient(
        this IServiceCollection services)
    {
        services.AddHttpClient<AssemblyHttpClient>(
            (sp, client) =>
            {
                var options = sp
                    .GetRequiredService<IOptionsMonitor<AssemblyClientOptions>>()
                    .CurrentValue;

                client.BaseAddress = new Uri(options.BaseAddress);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        return services;
    }
}
