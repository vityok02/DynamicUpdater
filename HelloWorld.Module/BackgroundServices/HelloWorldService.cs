using Microsoft.Extensions.Hosting;

namespace Module.Worker.BackgroundServices;

public sealed class HelloWorldService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        Console.WriteLine($"Hello world! | {DateTime.Now}");

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            Console.WriteLine($"Hello world! | {DateTime.Now}");
        }
    }
}
