# DynamicUpdater

`DynamicUpdater.Console` is the **actual** runtime entry point for this repository.

`DynamicUpdater.Host` is experimental/legacy and **not actual** for current usage.

## What this project does

The console app loads module assemblies from a shared `Assemblies` directory using collectible `AssemblyLoadContext`, runs each module through a `RunAsync` entry method, and then unloads them.

## Active flow (`DynamicUpdater.Console`)

1. Find `Assemblies` folder at repository root.
2. Scan subfolders containing `Module.` in folder name.
3. Load `<FolderName>.dll` from each module folder.
4. Find static `RunAsync(IServiceCollection services, CancellationToken ct)` method.
5. Start module.
6. On key press, cancel modules and unload their load contexts.
7. Force GC cycles and print unload result.

## Module contract

Each module assembly must expose a static method:

- `RunAsync(IServiceCollection services, CancellationToken ct)`

The console host invokes this method by reflection.

## Repository structure (important parts)

- `DynamicUpdater.Console` — current real launcher and unload test harness.
- `HelloWorld.Module` — sample worker-style module.
- `Data.Module` — sample API/data module.
- `DynamicUpdater.Tests` — unloading and related tests.
- `DynamicUpdater.Host` — not current runtime path.

## Running

From repository root:

```powershell
dotnet run --project .\DynamicUpdater.Console\DynamicUpdater.Console.csproj
```

Behavior:

- Modules are started.
- Press any key to trigger cancellation/unload.
- Console prints whether all `AssemblyLoadContext` instances were collected.

## Notes

- Keep module folder and main dll names aligned: `<FolderName>/<FolderName>.dll`.
- `Data.Module` uses PostgreSQL connection settings hardcoded in module startup.
- For practical testing of dynamic load/unload, use the console app, not `DynamicUpdater.Host`.
