using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Module.Api;

public class DataApiCore : IDynamicCore
{
    private CancellationTokenSource _cts = new();
    private WebApplication _app = null!;
    private WebApplicationBuilder _appBuilder = WebApplication.CreateBuilder();

    public void ConfigureServices(IServiceCollection services)
    {
        _appBuilder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
            options.EnableServiceProviderCaching(false);
        });

        foreach (var service in services)
        {
            _appBuilder.Services.Add(service);
        }
    }

    class DataRequest
    {
        public string Value { get; set; } = null!;
    }

    public async Task Start()
    {
        _appBuilder.WebHost.UseUrls("http://localhost:9001");

        _app = _appBuilder.Build();
        _appBuilder = null!;

        _app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/api/data", StringComparison.OrdinalIgnoreCase))
            {
                var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();

                if (HttpMethods.IsGet(context.Request.Method))
                {
                    var items = dbContext.Items.AsNoTracking().ToList();
                    await context.Response.WriteAsJsonAsync(items, DynamicJsonContext.Default.Options);
                    return;
                }

                if (HttpMethods.IsPost(context.Request.Method))
                {
                    try
                    {
                        var requestData = await context.Request.ReadFromJsonAsync<DataRequest>(DynamicJsonContext.Default.Options);

                        if (requestData == null || string.IsNullOrWhiteSpace(requestData.Value))
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            await context.Response.WriteAsync("Missing 'Value'");
                            return;
                        }

                        var newItem = new Data(Guid.NewGuid(), requestData.Value);
                        dbContext.Items.Add(newItem);
                        await dbContext.SaveChangesAsync();

                        context.Response.StatusCode = StatusCodes.Status201Created;
                        await context.Response.WriteAsJsonAsync(newItem, DynamicJsonContext.Default.Options);
                    }
                    catch (Exception)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid JSON");
                    }
                    return;
                }
            }

            await next(context);
        });

        await _app.StartAsync(_cts.Token);
    }

    public async Task Stop()
    {
        if (_app is null) return;

        _cts.Cancel();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null!;

        DynamicJsonContext.Default.Dispose();

        _cts.Dispose();
    }
}
