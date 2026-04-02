using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Module.Worker.BackgroundServices;

namespace Module.Worker;

public static class WorkerStartup
{
    public static async Task RunAsync(
        IServiceCollection services,
        CancellationToken ct)
    {
        services.AddTransient<HelloWorldService>();

        await using var serviceProvider = services
            .BuildServiceProvider();

        var hostedServices = serviceProvider
            .GetServices<HelloWorldService>()
            .Cast<IHostedService>()
            .ToList();

        try
        {

            foreach (var service in hostedServices)
            {
                await service.StartAsync(ct);
            }

            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var service in Enumerable.Reverse(hostedServices))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await service.StopAsync(stopCts.Token);

                if (service is IAsyncDisposable ad)
                {
                    await ad.DisposeAsync();
                }
                else if (service is IDisposable d)
                {
                    d.Dispose();
                }
            }

            hostedServices.Clear();
        }
    }
}
