using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WellmaxBrain.Domain.Entities;

namespace WellmaxBrain.Infrastructure.Data;

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
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeputyIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );
        });

        modelBuilder.Entity<ChannelMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ChannelId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChannelId);
            entity.HasIndex(e => e.CreatedAtUnix);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Sku);
        });

        modelBuilder.Entity<LearnedAlias>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.NormalizedPhrase }).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderCode).IsUnique();
            entity.HasIndex(e => e.ChannelId);
            entity.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // 1. Seed Quản trị viên mẫu
        var adminId = "me";
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminId,
                FullName = "Đào Hà (Admin)",
                PhoneNumber = "0988888888",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Department = "Kỹ Thuật & AI",
                RoleGroup = "Quản trị viên tối cao (Super Admin)",
                Gender = "Nam",
                IsAdmin = true,
                IsActive = true,
                DateOfBirth = new DateTime(1988, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                Avatar = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80",
                Bio = "Quản trị viên điều hành Wellmax Brain AI System"
            },
            new User
            {
                Id = "u-1",
                FullName = "Lê Hoàng Nam",
                PhoneNumber = "0912345678",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Department = "Kỹ Thuật & AI",
                RoleGroup = "Quản lý bộ phận",
                Gender = "Nam",
                IsAdmin = false,
                IsActive = true,
                DateOfBirth = new DateTime(1992, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                Avatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=80&q=80",
                Bio = "Tech Lead / AI Architect"
            },
            new User
            {
                Id = "u-2",
                FullName = "Phạm Thu Trang",
                PhoneNumber = "0977654321",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Department = "Kinh Doanh & Sale",
                RoleGroup = "Nhân viên kinh doanh",
                Gender = "Nữ",
                IsAdmin = false,
                IsActive = true,
                DateOfBirth = new DateTime(1995, 10, 12, 0, 0, 0, DateTimeKind.Utc),
                Avatar = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=80&q=80",
                Bio = "Trưởng nhóm Sale & CSKH TikTok"
            }
        );

        // 2. Seed Sản phẩm Wellmax Health & Beauty
        modelBuilder.Entity<Product>().HasData(
            new Product { Sku = "WM-MELASMA-02", ProductName = "Kem Nám Ban Đêm Thế Hệ Mới Melasma v2", UnitPrice = 650000, StockQty = 350 },
            new Product { Sku = "WM-CLEANSER-APPLE", ProductName = "Sữa Rửa Mặt Tinh Chất Táo Xanh Wellmax", UnitPrice = 280000, StockQty = 500 },
            new Product { Sku = "WM-SUN-01", ProductName = "Kem Chống Nắng Phổ Rộng Tone-up Wellmax UV Shield", UnitPrice = 420000, StockQty = 800 },
            new Product { Sku = "WM-SERUM-HA", ProductName = "Serum Cấp Ẩm Phục Hồi Chuyên Sâu HA B5", UnitPrice = 520000, StockQty = 420 }
        );

        // 3. Seed Bảng LearnedAliases (Bộ nhớ tri thức ban đầu)
        modelBuilder.Entity<LearnedAlias>().HasData(
            new LearnedAlias { Id = 1, TenantId = "wellmax-core", RawPhrase = "nám đêm v2", NormalizedPhrase = "nam dem v2", MatchedSku = "WM-MELASMA-02", ConfirmedBy = "admin", UsageCount = 10 },
            new LearnedAlias { Id = 2, TenantId = "wellmax-core", RawPhrase = "srm táo", NormalizedPhrase = "srm tao", MatchedSku = "WM-CLEANSER-APPLE", ConfirmedBy = "admin", UsageCount = 8 },
            new LearnedAlias { Id = 3, TenantId = "wellmax-core", RawPhrase = "kcn", NormalizedPhrase = "kcn", MatchedSku = "WM-SUN-01", ConfirmedBy = "admin", UsageCount = 15 }
        );

        // 4. Seed Cấu hình LLM toàn hệ thống
        modelBuilder.Entity<SystemSetting>().HasData(
            new SystemSetting { Key = "Gemini:Model", Value = "gemini-2.5-flash", Description = "Model LLM mặc định cho trích xuất đơn hàng" },
            new SystemSetting { Key = "Gemini:ApiKey", Value = "", Description = "Google Gemini API Key toàn hệ thống" },
            new SystemSetting { Key = "Gemini:SystemPrompt", Value = "Bạn là Wellmax Brain AI System - Trợ lý xử lý và trích xuất đơn hàng thông minh.", Description = "Định hình vai trò AI" }
        );
    }
}
