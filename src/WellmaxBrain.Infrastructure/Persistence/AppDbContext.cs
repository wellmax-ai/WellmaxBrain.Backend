using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WellmaxBrain.Domain.Entities;

namespace WellmaxBrain.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<LearnedAlias> LearnedAliases => Set<LearnedAlias>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.TenantId, u.PhoneNumber })
            .IsUnique();

        modelBuilder.Entity<LearnedAlias>()
            .HasIndex(a => new { a.TenantId, a.NormalizedPhrase })
            .IsUnique();

        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.TenantId, o.OrderCode })
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.Sku })
            .IsUnique();

        // JSON conversions for complex types
        modelBuilder.Entity<Channel>()
            .Property(c => c.DeputyIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>()
            );

        modelBuilder.Entity<Channel>()
            .Property(c => c.PinnedMessageIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>()
            );

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Reactions)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, int>()
            );
    }
}