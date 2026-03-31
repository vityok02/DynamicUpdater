using DynamicUpdater.Host.Contracts;
using Serilog;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace DynamicUpdater.Host.Infrastructure.DynamicManagement;

public sealed class DynamicContainer : IDynamicContainer
{
    private const int MaxUnloadAttempts = 10;
    private const int UnloadValidationDelay = 500;

    private DynamicModule? _currentDynamicModule;
    private readonly ILogger<DynamicContainer> _logger;
    private IDynamicCore _dynamicCore;

    public DynamicModule? CurrentModule => _currentDynamicModule;

    public DynamicContainer(ILogger<DynamicContainer> logger)
    {
        _logger = logger;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task UpdateModuleAsync(
    DynamicModule newModule,
    CancellationToken cancellationToken)
    {
        await StopAndUnloadAsync(cancellationToken);
        await StartAsync(newModule, cancellationToken);
    }

    private IDynamicCore InitDynamicCore(DynamicModule module)
    {
        var coreType = module.Assembly.GetTypes()
            .FirstOrDefault(t =>
                !t.IsInterface &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.Name == nameof(IDynamicCore)));

        if (coreType == null)
        {
            throw new InvalidOperationException($"IDynamicCore realization not found {module.Assembly.FullName}");
        }

        var instance = ActivatorUtilities.CreateInstance(module.ServiceProvider, coreType);

        return new DynamicProxy(instance);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task UnloadAsync(DynamicModule oldModule, CancellationToken ct)
    {
        _logger.LogInformation("Initiating unloading of the previous dynamic module version...");

        var alc = oldModule.ALC;
        var weakAlc = new WeakReference(alc);

        await DisposeDynamicAsync(oldModule);

        // Clear references to remove GC roots from the async state machine fields,
        // allowing the ALC to be collected while this method is suspended by await.
        oldModule = null!;
        alc = null!;

        await WaitForUnloadAsync(weakAlc, ct);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task DisposeDynamicAsync(DynamicModule module)
    {
        try
        {
            var alc = module.ALC;
            await module.DisposeAsync();

            alc.Unload();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dynamic module destruction sequence.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task WaitForUnloadAsync(WeakReference weakAlc, CancellationToken ct)
    {
        for (int i = 0; i < MaxUnloadAttempts && weakAlc.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (!weakAlc.IsAlive)
            {
                _logger.LogInformation(
                    "SUCCESS: Dynamic ALC successfully unloaded after {Iteration} cycles.",
                    i + 1);

                return;
            }

            _logger.LogDebug(
                "ALC still alive, waiting... (Cycle {Iteration}/{MaxAttempts})",
                i + 1,
                MaxUnloadAttempts);

            await Task.Delay(UnloadValidationDelay, ct);
        }

        if (weakAlc.IsAlive)
        {
            //if (weakAlc.Target is AssemblyLoadContext alc)
            //{
            //    var alive = alc.Assemblies
            //        .SelectMany(a => a.GetTypes())
            //        .Where(t => !t.IsValueType)
            //        .Select(t => t.FullName)
            //        .ToList();

            //    _logger.LogWarning(
            //        "ALC alive types: {Types}",
            //        string.Join(", ", alive));
            //}

            _logger.LogCritical(
                "MEMORY LEAK: Dynamic ALC is still alive after {MaxAttempts} GC cycles. Check for held references (Static fields, Event handlers, etc.)",
                MaxUnloadAttempts);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task StartAsync(DynamicModule newModule, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _currentDynamicModule, newModule);

        try
        {
            _dynamicCore = InitDynamicCore(newModule);

            var services = new ServiceCollection();

            services.AddSerilog((services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .Enrich.WithProperty("Module", newModule.Assembly.GetName().Name)
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] [{Module}] {Message:lj}{NewLine}{Exception}");
            });

            _dynamicCore.ConfigureServices(services);

            _logger.LogInformation("Starting the new dynamic core...");
            //await _dynamicCore.Start(newModule.ServiceProvider);
            await _dynamicCore.Start();

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FAILED to start new dynamic module. System might be in inconsistent state.");
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task StopAndUnloadAsync(CancellationToken cancellationToken)
    {
        if (_dynamicCore != null)
        {
            _logger.LogInformation("Stopping the old dynamic core...");
            await _dynamicCore.Stop();
            _dynamicCore = null!;
        }

        await UnloadCurrentAsync(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task UnloadCurrentAsync(CancellationToken cancellationToken)
    {
        var oldModule = Interlocked.Exchange(ref _currentDynamicModule, null);

        if (oldModule == null) return;

        await UnloadAsync(oldModule, cancellationToken);
        oldModule = null!;
    }
}
