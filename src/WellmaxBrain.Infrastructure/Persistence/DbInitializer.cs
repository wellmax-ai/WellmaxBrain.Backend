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

        // Chỉ tạo tài khoản Super Admin ban đầu nếu bảng Users hoàn toàn trống
        if (!await db.Users.AnyAsync())
        {
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123");
            var adminUser = new User
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
            };
            await db.Users.AddAsync(adminUser);
            await db.SaveChangesAsync();
        }

        // KHÔNG SEED BẤT KỲ NHÓM HAY TIN NHẮN DEMO NÀO
        // Toàn bộ nhóm và tin nhắn được tạo thật từ UI và lưu/lấy trực tiếp từ PostgreSQL
        // 4. Seed cau hinh AI Provider mac dinh (Gemini + DeepSeek)
        if (!await db.AiProviderConfigs.AnyAsync())
        {
            db.AiProviderConfigs.AddRange(
                new AiProviderConfig
                {
                    TenantId = "wellmax-default",
                    Provider = "gemini",
                    ModelName = "gemini-2.5-flash",
                    ApiKey = string.Empty,
                    BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                    IsActive = true
                },
                new AiProviderConfig
                {
                    TenantId = "wellmax-default",
                    Provider = "deepseek",
                    ModelName = "deepseek-chat",
                    ApiKey = string.Empty,
                    BaseUrl = "https://api.deepseek.com/v1",
                    IsActive = false
                });
            await db.SaveChangesAsync();
        }
    }
}