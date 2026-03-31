using DynamicUpdater.Host.Contracts;
using DynamicUpdater.Host.Infrastructure.DynamicManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace DynamicUpdater.Tests;

public class UnloadTests
{
    [Fact]
    public async Task Data_Module_Should_Unload_Cleanly()
    {
        var moduleBinaryPath = Path.Combine(AppContext.BaseDirectory, "Data.Module.dll");
        Assert.True(File.Exists(moduleBinaryPath), $"Module binary was not found: {moduleBinaryPath}");

        var isolatedRoot = CreateIsolatedContentRoot(moduleBinaryPath);

        try
        {
            var weakRef = await ExecuteAndUnloadInternal(isolatedRoot);

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
        finally
        {
            TryDeleteDirectory(isolatedRoot);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ExecuteAndUnloadInternal(string contentRoot)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));

        var moduleFactory = new DynamicModuleFactory(
            loggerFactory.CreateLogger<DynamicModuleFactory>(),
            new TestHostEnvironment(contentRoot));

        DynamicModule module = moduleFactory.Create();
        var alc = module.ALC;
        var weakRef = new WeakReference(alc);

        var coreType = module.Assembly.GetTypes().FirstOrDefault(t =>
            !t.IsInterface &&
            !t.IsAbstract &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.ConfigureServices)) &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.Start)) &&
            t.GetMethods().Any(m => m.Name == nameof(IDynamicCore.Stop)));

        Assert.NotNull(coreType);

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

    private static string CreateIsolatedContentRoot(string moduleBinaryPath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"dynamic-updater-tests-{Guid.NewGuid():N}");
        var assembliesPath = Path.Combine(root, "Assemblies");
        Directory.CreateDirectory(assembliesPath);

        var moduleDirectory = Path.GetDirectoryName(moduleBinaryPath)!;

        foreach (var filePath in Directory.GetFiles(moduleDirectory, "Data.Module*"))
        {
            var targetPath = Path.Combine(assembliesPath, Path.GetFileName(filePath));
            File.Copy(filePath, targetPath, overwrite: true);
        }

        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ApplicationName = nameof(DynamicUpdater.Tests);
            EnvironmentName = Environments.Development;
            ContentRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; }

        public string ContentRootPath { get; set; }

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }
}