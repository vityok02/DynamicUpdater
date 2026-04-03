using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;


var solutionRoot = Directory.GetParent(GetProjectRoot())!.FullName;
var assembliesPath = Path.Combine(solutionRoot, "Assemblies");

var moduleFolders = Directory.GetDirectories(assembliesPath);

var activeModules = new List<(DynamicAssemblyLoadContext alc, CancellationTokenSource cts)>();

foreach (var folder in moduleFolders)
{
    var folderName = Path.GetFileName(folder);

    if (!folderName.Contains("Module.", StringComparison.OrdinalIgnoreCase)) continue;

    var dllPath = Path.Combine(folder, $"{folderName}.dll");

    var alc = new DynamicAssemblyLoadContext(folderName, dllPath);
    var assembly = alc.LoadFromAssemblyPath(dllPath);

    var services = new ServiceCollection();

    var entryMethod = assembly.GetTypes()
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        .FirstOrDefault(m => m.Name == "RunAsync")
            ?? throw new Exception("Entry point not found");

    var cts = new CancellationTokenSource();
    
    var moduleTask = (Task)entryMethod.Invoke(null, [services, cts.Token])!;

    _ = moduleTask.ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            Console.WriteLine($"Exception in module [{folderName}]");
            Console.WriteLine(t.Exception.ToString());
        }
    }, TaskContinuationOptions.OnlyOnFaulted);

    activeModules.Add((alc, cts));
    Console.WriteLine($"[OK] Module [{folderName}] started.");
}

assembliesPath = null;
moduleFolders = null;

Console.WriteLine("Press the key to continue...");
await Task.Run(Console.ReadKey);
Console.WriteLine();

var weakReferences = new List<WeakReference>();

foreach (var (alc, cts) in activeModules)
{
    weakReferences.Add(new WeakReference(alc));
    cts.Cancel();
    cts.Dispose();
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

static string GetProjectRoot(
    [System.Runtime.CompilerServices.CallerFilePath] string path = "") 
    => Path.GetDirectoryName(path)!;
