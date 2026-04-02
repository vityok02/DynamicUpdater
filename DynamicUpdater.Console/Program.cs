using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;

var assembliesPath = @"C:\Main\Repositories\DynamicUpdater\DynamicUpdater.Host\Assemblies\";
var moduleFolders = Directory.GetDirectories(assembliesPath);

var activeModules = new List<(CustomAssemblyLoadContext Alc, object Instance, Type CoreType)>();

foreach (var folder in moduleFolders)
{
    var folderName = Path.GetFileName(folder);

    if (!folderName.Contains("Module.", StringComparison.OrdinalIgnoreCase)) continue;

    var dllPath = Path.Combine(folder, $"{folderName}.dll");

    var alc = new CustomAssemblyLoadContext(folderName, dllPath);
    var assembly = alc.LoadFromAssemblyPath(dllPath);

    var coreType = assembly.GetTypes()
        .FirstOrDefault(
            t => !t.IsInterface
            && !t.IsAbstract
            && t.GetInterfaces().Any(i => i.Name == "IDynamicCore"))
        ?? throw new Exception("Core type not found!");

    var instance = Activator.CreateInstance(coreType)
        ?? throw new Exception("Failed to create an instance of the core type!");

    var services = new ServiceCollection();

    coreType.GetMethod("ConfigureServices")?.Invoke(instance, [services]);

    var startTask = coreType.GetMethod("Start")?.Invoke(instance, null) as Task;
    startTask?.GetAwaiter().GetResult();
    startTask = null;

    activeModules.Add((alc, instance, coreType));
    Console.WriteLine($"[OK] Module {folderName} started.");
}

assembliesPath = null!;
moduleFolders = null!;

Console.WriteLine(">>> Module is running. Press any key to stop...");
await Task.Run(Console.ReadKey);
Console.WriteLine();

var weakReferences = new List<WeakReference>();

foreach (var (alc, instance, coreType) in activeModules)
{
    var stopTask = coreType.GetMethod("Stop")?.Invoke(instance, null) as Task;
    stopTask?.GetAwaiter().GetResult();
    stopTask = null;

    weakReferences.Add(new WeakReference(alc));

    alc.Unload();
}

activeModules.Clear();
activeModules = null!;

for (int i = 0; i < 12 && weakReferences.Any(w => w.IsAlive); i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    if (weakReferences.Any(wr => wr.IsAlive))
    {
        await Task.Delay(200);
        Console.WriteLine($"Step {i + 1}/12: Waiting for GC...");
    }
}

if (weakReferences.Any(wr => wr.IsAlive))
{
    Console.WriteLine("FATAL: ALC is still alive. Step 12/12.");
}
else
{
    Console.WriteLine("SUCCESS: ALC successfully unloaded.");
}

foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
{
    var alc = AssemblyLoadContext.GetLoadContext(asm);

    if (alc?.Name != "Default")
    {
        Console.WriteLine($"Assembly: {asm.GetName().Name} | Context: {alc?.Name ?? "Unknown"}");
    }
}

Console.ReadLine();
