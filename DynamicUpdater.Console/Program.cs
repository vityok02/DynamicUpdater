using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;

Console.WriteLine("Hello, World!");

var mainAssemblyPath = "C:\\Main\\Repositories\\DynamicUpdater\\DynamicUpdater.Host\\Assemblies\\Data.Module.dll";

var alc = new CustomAssemblyLoadContext(mainAssemblyPath);

var assembly = alc.LoadFromAssemblyPath(mainAssemblyPath);

var coreType = assembly.GetTypes()
            .FirstOrDefault(t =>
                !t.IsInterface &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.Name == "IDynamicCore"));

var instance = Activator.CreateInstance(coreType);

var serviceCollection = new ServiceCollection();

instance.GetType().GetMethod("ConfigureServices")?
    .Invoke(instance, [serviceCollection]);

serviceCollection = null;

var startResult = coreType.GetMethod("Start")?.Invoke(instance, null);
if (startResult is Task startTask) await startTask;
startResult = null;

Console.ReadLine();

var stopResult = coreType.GetMethod("Stop")?.Invoke(instance, null);
if (stopResult is Task stopTask) await stopTask;
stopResult = null;

instance = null;

var weakAlc = new WeakReference(alc);

coreType = null;
assembly = null;
alc.Unload();
alc = null;


for (int i = 0; i < 12 && weakAlc.IsAlive; i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    if (weakAlc.IsAlive)
    {
        await Task.Delay(250);
        Console.WriteLine($"Step {i + 1}/12");
    }
}

if (weakAlc.IsAlive)
{
    foreach (var context in AssemblyLoadContext.All)
    {
        Console.WriteLine($"Active ALC: {context.Name}, IsCollectible: {context.IsCollectible}");
    }
    Console.WriteLine("FATAL: ALC is still alive after unload and forced GC.");
}
else
{
    Console.WriteLine("SUCCESS: ALC successfully unloaded.");
}

//Console.ReadLine();

public sealed class CustomAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public CustomAssemblyLoadContext(string componentAssemblyPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(componentAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath is not null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (libraryPath is not null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
