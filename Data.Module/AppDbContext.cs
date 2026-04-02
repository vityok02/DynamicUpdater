using Microsoft.EntityFrameworkCore;

namespace Module.Api;

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
