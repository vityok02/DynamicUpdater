using DynamicUpdater.Host.Contracts;
using DynamicUpdater.Host.Infrastructure.DynamicManagement;
using System.Runtime.CompilerServices;

namespace DynamicUpdater.Host.BackgroundServices;

public sealed class DynamicUpdateService : BackgroundService
{
    private readonly DynamicModuleFactory _moduleFactory;
    private readonly ILogger<DynamicUpdateService> _logger;

    private readonly string _assembliesPath;
    private readonly IHostEnvironment _env;

    public DynamicUpdateService(
        DynamicModuleFactory moduleFactory,
        ILogger<DynamicUpdateService> logger,
        IHostEnvironment env)
    {
        _moduleFactory = moduleFactory;
        _logger = logger;

        _env = env;

        _assembliesPath = Path
            .Combine(_env.ContentRootPath, "Assemblies");

        if (!Directory.Exists(_assembliesPath))
        {
            Directory.CreateDirectory(_assembliesPath);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isolatedRoot = Directory
            .GetFiles(_assembliesPath, "*.Module.dll")
            .FirstOrDefault();

        try
        {
            var weakRef = await RunAndUnload(isolatedRoot);

            for (int i = 0; i < 12 && weakRef.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (weakRef.IsAlive)
                {
                    await Task.Delay(250);
                }
            }

            if (weakRef.IsAlive)
            {
                _logger.LogCritical("MEMORY LEAK");
            }
            else
            {
                _logger.LogInformation("ALC successfully unloaded.");
            }
        }
        finally
        {
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<WeakReference> RunAndUnload(string contentRoot)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));

        DynamicModule module = _moduleFactory.Create();
        var alc = module.ALC;
        var weakRef = new WeakReference(alc);

        var coreType = module.Assembly.GetTypes().FirstOrDefault(t =>
            !t.IsInterface &&
            !t.IsAbstract &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.ConfigureServices)) &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.Start)) &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.Stop)));

        var instance = ActivatorUtilities.CreateInstance(module.ServiceProvider, coreType!);
        var dynamicCore = new DynamicProxy(instance);

        var services = new ServiceCollection();
        dynamicCore.ConfigureServices(services);

        await dynamicCore.Start();
        await dynamicCore.Stop();

        dynamicCore = null!;
        instance = null!;

        await module.DisposeAsync();
        module = null!;

        alc.Unload();
        alc = null!;

        return weakRef;
    }
}