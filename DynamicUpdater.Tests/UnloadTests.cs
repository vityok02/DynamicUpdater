using DynamicUpdater.Host.Contracts;
using DynamicUpdater.Host.Infrastructure.DynamicManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace DynamicUpdater.Tests;

public class UnloadTests
{
    [Fact]
    public async Task Data_Module_Should_Unload_Cleanly()
    {
        var modulePath = GetModulePathFromHostAssemblies("Data.Module.dll");
        Assert.True(File.Exists(modulePath), $"Module binary was not found: {modulePath}");

        var weakRef = await ExecuteAndUnloadInternal(modulePath);

        for (int i = 0; i < 12 && weakRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (weakRef.IsAlive)
            {
                await Task.Delay(250);
            }
        }

        Assert.False(weakRef.IsAlive, "ALC is still alive after unload and forced GC.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ExecuteAndUnloadInternal(string modulePath)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));

        var alc = new DynamicAssemblyLoadContext(modulePath);
        var assembly = alc.LoadFromAssemblyPath(modulePath);
        var weakRef = new WeakReference(alc);

        var coreType = assembly.GetTypes().FirstOrDefault(t =>
            !t.IsInterface &&
            !t.IsAbstract &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.ConfigureServices)) &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.Start)) &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.Stop)));

        Assert.NotNull(coreType);

        var instance = Activator.CreateInstance(coreType!);
        var dynamicCore = new DynamicProxy(instance);

        var services = new ServiceCollection();
        dynamicCore.ConfigureServices(services);

        await dynamicCore.Start();
        await dynamicCore.Stop();

        dynamicCore = null!;
        instance = null!;

        alc.Unload();
        alc = null!;

        return weakRef;
    }

    private static string GetModulePathFromHostAssemblies(string moduleFileName)
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var assembliesPath = Path.Combine(solutionRoot, "DynamicUpdater.Host", "Assemblies");

        return Path.Combine(assembliesPath, moduleFileName);
    }
}