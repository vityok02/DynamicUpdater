using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Serilog;

namespace Data.Module;

public class DynamicModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => Guid.NewGuid();
}

public class DataApiCore : IDynamicCore
{
    private CancellationTokenSource _cts = new();
    private WebApplication? _app;
    private WebApplicationBuilder _appBuilder = WebApplication.CreateBuilder();

    private static int Counter = 0;

    public void ConfigureServices(IServiceCollection services)
    {
        Counter++;
        _appBuilder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
            options.EnableServiceProviderCaching(false);
            options.ReplaceService<IModelCacheKeyFactory, DynamicModelCacheKeyFactory>();
        });

        foreach (var service in services)
        {
            _appBuilder.Services.Add(service);
        }
    }

    public async Task Start()
    {
        _appBuilder.WebHost.UseUrls("http://localhost:9001");

        _app = _appBuilder.Build();

        using var scope = _app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataApiCore>>();

        logger.LogInformation("DataApiCore started. Initializing database...");

        try
        {
            //dbContext.Database.EnsureCreated();

            var dbSet = dbContext.Set<Data>();

            //if (!dbSet.Any())
            //{
            //    dbSet.AddRange(
            //        new Data(Guid.NewGuid(), "Value 1"),
            //        new Data(Guid.NewGuid(), "Value 2"),
            //        new Data(Guid.NewGuid(), "Value 3"));

            //    dbContext.SaveChanges();
            //}
        }
        catch (Exception)
        {
        }

        _app.MapGet("/", () => $"Data API is alive: {DateTime.Now}");

        _app.MapGet("/api/data", async (AppDbContext dbContext) =>
        {
            return await dbContext.Items.ToListAsync();
        });

        logger.LogInformation("Data API is starting on port 9001...");
        await _app.StartAsync(_cts.Token);
    }

    public async Task Stop()
    {
        if (_app is null) return;

        _cts.Cancel();
        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _app.StopAsync(stopCts.Token);
        await _app.DisposeAsync();

        // MEMORY LEAK
        NpgsqlConnection.ClearAllPools();

        _app = null;
        _appBuilder = null!;

        _cts.Dispose();
    }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Data> Items { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dataEntity = modelBuilder.Entity<Data>();

        dataEntity.HasKey(x => x.Id);

        dataEntity.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}

public record Data(Guid Id, string Value);
