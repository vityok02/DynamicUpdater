using DynamicUpdater.Host.Contracts;
using System.Reflection;

namespace DynamicUpdater.Host.Infrastructure.DynamicManagement;

public sealed class DynamicModuleFactory
{
    private readonly ILogger<DynamicModuleFactory> _logger;
    private readonly string _assembliesPath;
    private readonly IHostEnvironment _env;

    public DynamicModuleFactory(
        ILogger<DynamicModuleFactory> logger,
        IHostEnvironment env)
    {
        _logger = logger;
        _env = env;

        _assembliesPath = Path
            .Combine(_env.ContentRootPath, "Assemblies");

        if (!Directory.Exists(_assembliesPath))
        {
            Directory.CreateDirectory(_assembliesPath);
        }
    }

    // TODO: Refactor the path logic
    public DynamicModule Create()
    {
        var mainAssemblyPath = Directory
            .GetFiles(_assembliesPath, "*.Module.dll")
            .FirstOrDefault();

        if (mainAssemblyPath is null)
        {
            throw new FileNotFoundException(
                "No dynamic assembly found. Ensure a .Module.dll file is present in the Assemblies directory.",
                _assembliesPath);
        }

        var alc = new DynamicAssemblyLoadContext(mainAssemblyPath);

        try
        {
            var assembly = alc
                .LoadFromAssemblyPath(mainAssemblyPath);

            _logger.LogInformation(
                "Dynamic assembly loaded {AssemblyName}",
                assembly.FullName);

            var services = new ServiceCollection();
            RegisterAssemblyServices(assembly, services);

            var dynamicServiceProvider = services
                .BuildServiceProvider();

            return new DynamicModule(
                alc,
                assembly,
                dynamicServiceProvider);
        }
        catch (BadImageFormatException ex)
        {
            _logger.LogError(ex,
                "Invalid assembly format. Ensure the dynamic assembly targets a compatible runtime version.");


            alc.Unload();

            throw new InvalidOperationException(
                "Dynamic assembly has invalid format or targets incompatible runtime.",
                ex);
        }
        catch (Exception)
        {
            alc.Unload();
            throw;
        }
    }

    private void RegisterAssemblyServices(
        Assembly assembly,
        IServiceCollection services)
    {
        try
        {
            var startupMethod = assembly.GetExportedTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .SingleOrDefault(m =>
                    m.Name == nameof(IDynamicCore.ConfigureServices) &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(IServiceCollection));

            if (startupMethod is not null)
            {
                var instance = Activator.CreateInstance(startupMethod.DeclaringType!);

                startupMethod.Invoke(instance, [services]);
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loaderExceptions = string
                .Join(" | ", ex.LoaderExceptions.Select(e => e?.Message));

            _logger.LogError(
                ex,
                "Failed to load assembly types. LoaderExceptions: {Exceptions}",
                loaderExceptions);

            throw new InvalidOperationException(
                $"Dynamic load failed: {loaderExceptions}",
                ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Multiple ConfigureServices methods found. Ensure only one public method with signature void ConfigureServices(IServiceCollection) exists.");

            throw new InvalidOperationException(
                "Dynamic load failed: Multiple ConfigureServices methods found.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during dynamic service configuration. Assembly: {AssemblyName}",
                assembly.FullName);

            throw;
        }
    }
}
