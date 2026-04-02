using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Module.Api;

public static class ApiStartup
{
    public static async Task RunAsync(
        IServiceCollection services,
        CancellationToken ct)
    {
        var appBuilder = WebApplication.CreateBuilder();

        appBuilder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
            options.EnableServiceProviderCaching(false);
        });

        foreach (var service in services)
        {
            appBuilder.Services.Add(service);
        }

        appBuilder.WebHost.UseUrls("http://localhost:9001");

        var app = appBuilder.Build();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/api/data", StringComparison.OrdinalIgnoreCase))
            {
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    await GetData(context);
                }

                if (HttpMethods.IsPost(context.Request.Method))
                {
                    await PostData(context);
                }
            }

            await next(context);

        });

        await app.StartAsync(ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        finally
        {
            DynamicJsonContext.Default.Dispose();

            await app.StopAsync(ct);
            await app.DisposeAsync();
        }
    }

    private static async Task GetData(HttpContext context)
    {
        var dbContext = context.RequestServices
            .GetRequiredService<AppDbContext>();

        var items = dbContext.Items
            .AsNoTracking()
            .ToList();

        await context.Response
            .WriteAsJsonAsync(items, DynamicJsonContext.Default.Options);
    }

    private static async Task PostData(HttpContext context)
    {
        try
        {
            var dbContext = context.RequestServices
                .GetRequiredService<AppDbContext>();

            var requestData = await context.Request
                .ReadFromJsonAsync<DataRequest>(DynamicJsonContext.Default.Options);

            if (requestData == null
                || string.IsNullOrWhiteSpace(requestData.Value))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response
                    .WriteAsync("Missing 'Value'");

                return;
            }

            var newItem = new Data(
                Guid.NewGuid(),
                requestData.Value);

            dbContext.Items.Add(newItem);
            await dbContext.SaveChangesAsync();

            context.Response.StatusCode = StatusCodes.Status201Created;
            await context.Response
                .WriteAsJsonAsync(newItem, DynamicJsonContext.Default.Options);
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response
                .WriteAsync("Invalid JSON");
        }
    }
}