using HelloWorld.Module.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HelloWorld.Module;

public class DynamicHostCore : IDynamicCore
{
    private CancellationTokenSource _cts = new();
    private List<IHostedService> _hostedServices = [];

    private IServiceProvider? _internalProvider;
    private IServiceCollection? _tempCollection;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<HelloWorldService>();

        _tempCollection = services;
    }

    public async Task Start()
    {
        if (_tempCollection == null)
            throw new InvalidOperationException("ConfigureServices must be called before Start.");

        _internalProvider = _tempCollection.BuildServiceProvider();

        _tempCollection = null;

        _hostedServices = _internalProvider.GetServices<HelloWorldService>()
            .Cast<IHostedService>()
            .ToList();

        foreach (var service in _hostedServices)
        {
            await service.StartAsync(_cts.Token);
        }
    }

    public async Task Stop()
    {
        _cts?.Cancel();

        foreach (var service in Enumerable.Reverse(_hostedServices))
        {
            await service.StopAsync(CancellationToken.None);
            if (service is IAsyncDisposable ad) await ad.DisposeAsync();
            else if (service is IDisposable d) d.Dispose();
        }

        _hostedServices.Clear();

        if (_internalProvider is IDisposable disp)
        {
            disp.Dispose();
        }

        _internalProvider = null;
        _cts?.Dispose();
        _cts = null!;
    }
}
