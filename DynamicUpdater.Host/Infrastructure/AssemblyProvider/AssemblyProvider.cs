using SystemPath = System.IO.Path;

namespace DynamicUpdater.Host.Infrastructure.AssemblyProvider;

public class AssemblyProvider : IAssemblyProvider
{
    private readonly string _dynamicsDirectory;
    private readonly IHostEnvironment _env;

    public AssemblyProvider(IHostEnvironment env)
    {
        _env = env;

        _dynamicsDirectory = Path
            .Combine(_env.ContentRootPath, "Assemblies");

        if (!Directory.Exists(_dynamicsDirectory))
        {
            Directory.CreateDirectory(_dynamicsDirectory);
        }
    }

    // TODO: handle multiple assemblies
    public async Task<byte[]> GetAssemblyBytesAsync(CancellationToken cancellationToken)
    {
        var assemblyBytes = await File.ReadAllBytesAsync(
            Directory.GetFiles(_dynamicsDirectory, "*.dll").FirstOrDefault(),
            cancellationToken);

        return assemblyBytes;
    }

    private string GetAssemblyPath()
    {
        var fullPath = SystemPath.IsPathRooted("Dynamics")
            ? "Dynamics"
            : SystemPath.Combine(_env.ContentRootPath, "Dynamics");

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"The specified dynamics directory does not exist: {fullPath}");
        }

        var assemblies = Directory
            .GetFiles(fullPath, "*.dll")
            .Order()
            .ToArray();

        if (assemblies.Length == 0)
        {
            throw new DllNotFoundException(
                fullPath);
        }

        return assemblies.FirstOrDefault();
    }
}
