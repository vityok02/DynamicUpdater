using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.Loader;


var solutionRoot = Directory.GetParent(GetProjectRoot())!.FullName;
var assembliesPath = Path.Combine(solutionRoot, "Assemblies");
var activeAssembliesPath = Path.Combine(assembliesPath, "Active");

var moduleFolders = Directory.GetDirectories(activeAssembliesPath);

var activeModules = new List<(DynamicAssemblyLoadContext alc, CancellationTokenSource cts)>();

#region START_ASSEMBLIES
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
#endregion

Console.WriteLine("Press the key to unload assemblies...");
await Task.Run(Console.ReadKey);
Console.WriteLine();

#region UNLOAD_ASSEMBLIES
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

    Environment.Exit(100);
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
#endregion

Console.WriteLine("Press the key to update assemblies...");
await Task.Run(Console.ReadKey);

Console.ReadKey();
UpdateAssemblies(
    Path.Combine(assembliesPath, "Incoming"),
    Path.Combine(assembliesPath, "Active"));

Console.ReadKey();

static string GetProjectRoot(
    [System.Runtime.CompilerServices.CallerFilePath] string path = "") 
    => Path.GetDirectoryName(path)!;

void UpdateAssemblies(string incomingPath, string activePath)
{
    if (!Directory.Exists(incomingPath)
        || !Directory.GetFileSystemEntries(incomingPath).Any())
    {
        return;
    }

    Console.WriteLine("[UPDATE] New files found. Applying...");

    var incomingModules = Directory.GetDirectories(incomingPath);

    foreach (var moduleDirectory in incomingModules)
    {
        var moduleName = Path.GetFileName(moduleDirectory);
        var targetDirectory = Path.Combine(activePath, moduleName);

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        try
        {
            foreach (var file in Directory.GetFiles(moduleDirectory))
            {
                var targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(file));
                File.Copy(file, targetFilePath, true);
            }

            Directory.Delete(moduleDirectory, true);
        }
        catch (IOException)
        {
        }
    }

    Console.WriteLine("[UPDATE] All modules updated in Active folder.");
}

void DownloadAssemblies(string assembliesRootPath)
{
    // Download assemblies from remote source and save them to the Download
}
