using System.Reflection;

namespace DynamicUpdater.Host.Infrastructure.DynamicManagement;

public sealed class DynamicModule : IAsyncDisposable, IDisposable
{
    private DynamicAssemblyLoadContext? _alc;
    private IServiceProvider? _serviceProvider;
    private int _disposedValue;

    public Assembly Assembly { get; }

    public DynamicAssemblyLoadContext ALC => _alc
        ?? throw new ObjectDisposedException(nameof(DynamicModule));

    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new ObjectDisposedException(nameof(DynamicModule));

    public DynamicModule(
        DynamicAssemblyLoadContext alc,
        Assembly assembly,
        IServiceProvider serviceProvider)
    {
        _alc = alc;
        Assembly = assembly;
        _serviceProvider = serviceProvider;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposedValue, 1) == 1)
        {
            return;
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        CleanupReferences();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposedValue, 1) == 1) return;

        (_serviceProvider as IDisposable)?.Dispose();

        CleanupReferences();
        GC.SuppressFinalize(this);
    }

    private void CleanupReferences()
    {
        _serviceProvider = null;
        _alc = null;
    }
}
