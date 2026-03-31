using DynamicUpdater.Host.Infrastructure.AssemblyProvider;
using DynamicUpdater.Host.Infrastructure.DynamicManagement;
using DynamicUpdater.Host.Options;
using Microsoft.Extensions.Options;

namespace DynamicUpdater.Host.BackgroundServices;

public sealed class DynamicUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DynamicModuleFactory _dynamicFactory;
    private readonly IDynamicContainer _dynamicContainer;
    private readonly ILogger<DynamicUpdateService> _logger;
    private readonly IOptionsMonitor<TimerOptions> _optionsMonitor;

    public DynamicUpdateService(
        IServiceScopeFactory scopeFactory,
        DynamicModuleFactory dynamicModuleFactory,
        IDynamicContainer dynamicContainer,
        ILogger<DynamicUpdateService> logger,
        IOptionsMonitor<TimerOptions> timerOptionsMonitor)
    {
        _scopeFactory = scopeFactory;
        _dynamicFactory = dynamicModuleFactory;
        _dynamicContainer = dynamicContainer;
        _logger = logger;
        _optionsMonitor = timerOptionsMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dynamic Update Service started.");

        await UpdateCycleAsync(stoppingToken);

        var options = _optionsMonitor.CurrentValue;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalInSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await UpdateCycleAsync(stoppingToken);

                var currentInterval = TimeSpan.FromSeconds(
                    _optionsMonitor.CurrentValue.IntervalInSeconds);

                if (timer.Period != currentInterval)
                {
                    timer.Period = currentInterval;
                    _logger.LogInformation(
                        "Update interval changed to {Seconds} seconds.",
                        currentInterval.TotalSeconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Dynamic Update Service is stopping due to host shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "FATAL: An unhandled exception occurred in the Dynamic Update Service. The background worker has crashed.");
        }
    }

    private async Task UpdateCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory
            .CreateScope();

        DynamicModule? newModule = null;

        try
        {
            var assemblyProvider = scope.ServiceProvider
                .GetRequiredService<IAssemblyProvider>();

            var assemblyBytes = await assemblyProvider
                .GetAssemblyBytesAsync(cancellationToken);

            newModule = _dynamicFactory
                .Create();

            await _dynamicContainer
                .UpdateModuleAsync(newModule, cancellationToken);

            _logger.LogInformation(
                "Dynamic module updated successfully. Current module: {DynamicModule}",
                newModule.Assembly.FullName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred during the dynamic module update cycle. The system will retry in {Delay} seconds.",
                _optionsMonitor.CurrentValue.IntervalInSeconds);

            if (newModule is not null)
            {
                await CleanupOrphanedDynamicModuleAsync(newModule);
            }
        }
    }

    private async Task CleanupOrphanedDynamicModuleAsync(DynamicModule dynamicModule)
    {
        try
        {
            _logger.LogWarning(
                "Cleaning up orphaned dynamic module.");

            var alc = dynamicModule.ALC;
            await dynamicModule.DisposeAsync();
            alc.Unload();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "FATAL: Failed to cleanup dynamic module. Potential memory leak.");
        }
    }
}
