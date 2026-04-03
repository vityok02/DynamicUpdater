using System.Reflection;
using System.Runtime.Loader;

public sealed class DynamicAssemblyLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver? _resolver;
    private string? _mainAssemblyName;

    public DynamicAssemblyLoadContext(string name, string componentAssemblyPath)
    : base(name, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(componentAssemblyPath);
        _mainAssemblyName = Path.GetFileNameWithoutExtension(componentAssemblyPath);

        Default.Resolving += OnDefaultResolving;
    }

    private Assembly? OnDefaultResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var assemblyPath = _resolver?.ResolveAssemblyToPath(assemblyName);

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
