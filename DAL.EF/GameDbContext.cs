using Microsoft.EntityFrameworkCore;

namespace DAL.EF;

public sealed class ConfigEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsPreset { get; set; }
    public string Payload { get; set; } = "";
}

public sealed class GameEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public string Payload { get; set; } = "";
}

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<ConfigEntity> Configurations => Set<ConfigEntity>();
    public DbSet<GameEntity> Games => Set<GameEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ConfigEntity>().Property(entity => entity.Name).IsRequired();
        modelBuilder.Entity<ConfigEntity>().Property(entity => entity.Payload).IsRequired();
        modelBuilder.Entity<GameEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<GameEntity>().Property(entity => entity.Name).IsRequired();
        modelBuilder.Entity<GameEntity>().Property(entity => entity.Payload).IsRequired();
        modelBuilder.Entity<GameEntity>().HasIndex(entity => entity.UpdatedAt);
    }
}
