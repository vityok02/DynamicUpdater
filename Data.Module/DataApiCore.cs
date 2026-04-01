using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Data.Module;

public class DataApiCore : IDynamicCore
{
    private CancellationTokenSource _cts = new();
    private WebApplication? _app;
    private WebApplicationBuilder _appBuilder = WebApplication.CreateBuilder();

    public void ConfigureServices(IServiceCollection services)
    {
        _appBuilder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseNpgsql("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
            options.EnableServiceProviderCaching(false);
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

        using (var scope = _app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataApiCore>>();

            logger.LogInformation("DataApiCore started. Initializing database...");

            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

            var dbContext = dbContextFactory.CreateDbContext();

            var dbSet = dbContext.Set<Data>();

            var data = dbSet
                .ToArray();

            foreach (var item in data)
            {
                Console.WriteLine(item);
            }
        }

        _app.MapGet("/", (ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("DynamicPlugin");
            var message = $"Data API is alive: {DateTime.Now}";
            logger.LogInformation(message);
            return message;
        });

        // MEMORY LEAK
        _app.MapGet("/api/data", async (IDbContextFactory<AppDbContext> dbContextFactory) =>
        {
            using (var dbContext = dbContextFactory.CreateDbContext())
            {
                return await dbContext.Items.AsNoTracking().ToListAsync();
            }
        });

        await _app.StartAsync(_cts.Token);
    }

    public async Task Stop()
    {
        if (_app is null) return;

        _cts.Cancel();
        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await _app.StopAsync(stopCts.Token);
        await _app.DisposeAsync();

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
