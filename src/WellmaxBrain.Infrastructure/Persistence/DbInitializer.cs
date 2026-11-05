using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Domain.Entities;

namespace WellmaxBrain.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }

        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var user1Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var user2Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var user3Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var user4Id = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var botId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var channel1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var channel2Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var channel3Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        // 1. Chỉ khởi tạo danh sách Users chuẩn của hệ thống Wellmax
        if (!await db.Users.AnyAsync())
        {
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123");
            var users = new List<User>
            {
                new User
                {
                    Id = adminId,
                    TenantId = "wellmax-default",
                    FullName = "Đào Hà (Admin)",
                    PhoneNumber = "0988888888",
                    PasswordHash = defaultPasswordHash,
                    BirthDate = "1990-05-15",
                    Gender = "Nam",
                    Department = "Ban Giám Đốc",
                    RoleGroup = "Quản trị viên tối cao (Super Admin)",
                    Avatar = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80",
                    Note = "Sáng lập viên & Quản trị trưởng hệ thống",
                    Bio = "Quản trị viên & Điều hành kinh doanh",
                    IsAdmin = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = user1Id,
                    TenantId = "wellmax-default",
                    FullName = "Lê Hoàng Nam",
                    PhoneNumber = "0912345671",
                    PasswordHash = defaultPasswordHash,
                    BirthDate = "1992-08-20",
                    Gender = "Nam",
                    Department = "Vận Hành & Kho",
                    RoleGroup = "Quản lý bộ phận (Manager)",
                    Avatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=80&q=80",
                    Note = "Phụ trách điều phối kho và giao vận hỏa tốc",
                    Bio = "Trưởng Nhóm Vận Hành & Kho",
                    IsAdmin = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = user2Id,
                    TenantId = "wellmax-default",
                    FullName = "Phạm Thu Trang",
                    PhoneNumber = "0912345672",
                    PasswordHash = defaultPasswordHash,
                    BirthDate = "1996-11-04",
                    Gender = "Nữ",
                    Department = "Kinh Doanh & Sale",
                    RoleGroup = "Nhân viên kinh doanh (Sale Rep)",
                    Avatar = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=80&q=80",
                    Note = "Chuyên chốt đơn livestream và khách VIP TikTok",
                    Bio = "Chuyên Viên Chốt Đơn TikTok",
                    IsAdmin = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = user3Id,
                    TenantId = "wellmax-default",
                    FullName = "Đặng Tuấn Kiệt",
                    PhoneNumber = "0912345673",
                    PasswordHash = defaultPasswordHash,
                    BirthDate = "1993-02-12",
                    Gender = "Nam",
                    Department = "Kế Toán - Tài Chính",
                    RoleGroup = "Kế toán - Công nợ (Finance)",
                    Avatar = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=80&q=80",
                    Note = "Quản lý sổ quỹ, hóa đơn VAT và hạn mức công nợ",
                    Bio = "Kế Toán & Quản Lý Công Nợ",
                    IsAdmin = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = user4Id,
                    TenantId = "wellmax-default",
                    FullName = "Nguyễn Bích Ngọc",
                    PhoneNumber = "0912345674",
                    PasswordHash = defaultPasswordHash,
                    BirthDate = "1995-09-30",
                    Gender = "Nữ",
                    Department = "Chăm Sóc Khách Hàng",
                    RoleGroup = "Chuyên viên CSKH (Support)",
                    Avatar = "https://images.unsplash.com/photo-1517841905240-472988babdf9?auto=format&fit=crop&w=80&q=80",
                    Note = "Tư vấn phác đồ da và chăm sóc sắc đẹp",
                    Bio = "Chuyên Viên Tư Vấn Facebook Ads",
                    IsAdmin = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = botId,
                    TenantId = "wellmax-default",
                    FullName = "Wellmax Brain AI System",
                    PhoneNumber = "19008888",
                    PasswordHash = "",
                    BirthDate = "2026-01-01",
                    Gender = "Khác",
                    Department = "Kỹ Thuật & AI",
                    RoleGroup = "Quản trị viên tối cao (Super Admin)",
                    Avatar = "bot-avatar",
                    Note = "AI Agent trung tâm điều hành dữ liệu và trích xuất đơn hàng",
                    Bio = "Trợ lý điều hành kinh doanh & đơn hàng",
                    IsAdmin = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
            await db.Users.AddRangeAsync(users);
            await db.SaveChangesAsync();
        }

        // 2. Khởi tạo Nhóm và phân bổ đúng thành viên (Không seed tin nhắn demo)
        if (!await db.Channels.AnyAsync())
        {
            var channels = new List<Channel>
            {
                new Channel
                {
                    Id = channel1Id,
                    TenantId = "wellmax-default",
                    Name = "Nhóm TikTok Shop & Livestream",
                    Avatar = "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80",
                    Description = "Tiếp nhận đơn hàng livestream và tư vấn qua TikTok Shop",
                    OwnerId = adminId,
                    DeputyIds = new List<Guid> { user1Id },
                    IsPinned = true,
                    PinnedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    IsHidden = false,
                    CreatedAt = DateTime.UtcNow
                },
                new Channel
                {
                    Id = channel2Id,
                    TenantId = "wellmax-default",
                    Name = "Nhóm Facebook Ads & Fanpage",
                    Avatar = "https://images.unsplash.com/photo-1497366216548-37526070297c?auto=format&fit=crop&w=120&q=80",
                    Description = "Xử lý inbox khách hàng từ chiến dịch quảng cáo Facebook",
                    OwnerId = user1Id,
                    DeputyIds = new List<Guid>(),
                    IsPinned = false,
                    PinnedAtUnix = 0,
                    IsHidden = false,
                    CreatedAt = DateTime.UtcNow
                },
                new Channel
                {
                    Id = channel3Id,
                    TenantId = "wellmax-default",
                    Name = "Nhóm Đại Lý & Bán Buôn B2B",
                    Avatar = "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?auto=format&fit=crop&w=120&q=80",
                    Description = "Điều phối xuất kho đại lý và nhà phân phối toàn quốc",
                    OwnerId = user4Id,
                    DeputyIds = new List<Guid> { adminId },
                    IsPinned = false,
                    PinnedAtUnix = 0,
                    IsHidden = false,
                    CreatedAt = DateTime.UtcNow
                }
            };
            await db.Channels.AddRangeAsync(channels);
            await db.SaveChangesAsync();

            // Phân quyền thành viên đúng từng nhóm
            var members = new List<ChannelMember>
            {
                // Nhóm 1: Admin, Nam, Trang, Bot
                new ChannelMember { ChannelId = channel1Id, UserId = adminId, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel1Id, UserId = user1Id, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel1Id, UserId = user2Id, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel1Id, UserId = botId, JoinedAt = DateTime.UtcNow },

                // Nhóm 2: Nam, Ngọc, Bot (Trang, Kiệt không có trong nhóm này)
                new ChannelMember { ChannelId = channel2Id, UserId = user1Id, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel2Id, UserId = user4Id, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel2Id, UserId = botId, JoinedAt = DateTime.UtcNow },

                // Nhóm 3: Ngọc, Admin, Kiệt, Bot (Trang không có trong nhóm này)
                new ChannelMember { ChannelId = channel3Id, UserId = user4Id, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel3Id, UserId = adminId, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel3Id, UserId = user3Id, JoinedAt = DateTime.UtcNow },
                new ChannelMember { ChannelId = channel3Id, UserId = botId, JoinedAt = DateTime.UtcNow },
            };
            await db.ChannelMembers.AddRangeAsync(members);
            await db.SaveChangesAsync();
        }

        // HOÀN TOÀN KHÔNG SEED TIN NHẮN DEMO:
        // Cơ sở dữ liệu sạch, tin nhắn chỉ sinh ra khi người dùng thực hiện chat.

        // 3. Seed Danh mục sản phẩm & Bí danh SKU
        if (!await db.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = "wellmax-default",
                    Sku = "WM-SUN-01",
                    ProductName = "Kem Chống Nắng Phổ Rộng Wellmax",
                    UnitPrice = 350000,
                    StockQty = 500,
                    Unit = "Tuýp",
                    IsActive = true
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = "wellmax-default",
                    Sku = "WM-MELASMA-02",
                    ProductName = "Kem Nám Đêm Wellmax Melasma V2",
                    UnitPrice = 550000,
                    StockQty = 300,
                    Unit = "Hộp",
                    IsActive = true
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = "wellmax-default",
                    Sku = "WM-CLEAN-APPLE",
                    ProductName = "Sữa Rửa Mặt Tinh Chất Táo (Quà tặng)",
                    UnitPrice = 0,
                    StockQty = 1000,
                    Unit = "Tuýp",
                    IsActive = true
                }
            };
            await db.Products.AddRangeAsync(products);
            await db.SaveChangesAsync();
        }
    }
}