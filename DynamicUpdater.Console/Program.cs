using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;

var path = @"C:\Main\Repositories\DynamicUpdater\DynamicUpdater.Host\Assemblies\Data.Module.dll";

var alc = new CustomAssemblyLoadContext("CustomModule", path);
var assembly = alc.LoadFromAssemblyPath(path);

var coreType = assembly.GetTypes()
    .FirstOrDefault(t => !t.IsInterface && !t.IsAbstract && t.GetInterfaces().Any(i => i.Name == "IDynamicCore"));

if (coreType == null) throw new Exception("Core type not found!");

var instance = Activator.CreateInstance(coreType);

var serviceCollection = new ServiceCollection();
coreType.GetMethod("ConfigureServices")?.Invoke(instance, [serviceCollection]);

var startTask = coreType.GetMethod("Start")?.Invoke(instance, null) as Task;
startTask?.GetAwaiter().GetResult();
startTask = null!;

Console.WriteLine(">>> Module is running. Press any key to stop...");
Console.ReadLine();

var stopTask = coreType.GetMethod("Stop")?.Invoke(instance, null) as Task;
stopTask?.GetAwaiter().GetResult();
stopTask = null!;

coreType = null!;
assembly = null!;
instance = null!;
serviceCollection = null!;

alc.Unload();
alc = null!;

var weakAlc = new WeakReference(alc);

for (int i = 0; i < 12 && weakAlc.IsAlive; i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    if (weakAlc.IsAlive)
    {
        await Task.Delay(200);
        Console.WriteLine($"Step {i + 1}/12: Waiting for GC...");
    }
}

if (weakAlc.IsAlive)
{
    Console.WriteLine("FATAL: ALC is still alive. Step 12/12.");
}
else
{
    Console.WriteLine("SUCCESS: ALC successfully unloaded.");
}

foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
{
    var a = AssemblyLoadContext.GetLoadContext(asm);

    Console.WriteLine($"Assembly: {asm.GetName().Name} | Context: {a?.Name ?? "Unknown"}");
}

Console.ReadLine();

public sealed class CustomAssemblyLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver? _resolver;
    private string? _mainAssemblyName;

    public CustomAssemblyLoadContext(string name, string componentAssemblyPath)
    : base(name, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(componentAssemblyPath);
        _mainAssemblyName = Path.GetFileNameWithoutExtension(componentAssemblyPath);

        Default.Resolving += OnDefaultResolving;
    }

    // To load the packages into the Default context
    private Assembly? OnDefaultResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var assemblyPath = _resolver?.ResolveAssemblyToPath(assemblyName);

        Console.WriteLine($"[Default.Resolving] {assemblyName.Name} → {assemblyPath ?? "NULL"}");

        if (assemblyPath == null)
            return null;

        if (assemblyName.Name == _mainAssemblyName)
            return null;

        return Default.LoadFromAssemblyPath(assemblyPath);
    }

    public new void Unload()
    {
        Default.Resolving -= OnDefaultResolving;
        _mainAssemblyName = null;
        _resolver = null;
        base.Unload();
    }
}