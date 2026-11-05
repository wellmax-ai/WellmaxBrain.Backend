using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WellmaxBrain.API.Hubs;
using WellmaxBrain.Application.Interfaces;
using WellmaxBrain.Infrastructure.Persistence;
using WellmaxBrain.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// DbContext PostgreSQL
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Host=localhost;Database=wellmax_brain_db;Username=postgres;Password=postgres"));

// Services & HttpClient
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAIExtractorService, GeminiExtractorService>();

// SignalR Realtime Service
builder.Services.AddSignalR();

// CORS Client React Vite
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowClient", p =>
    {
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// CẤU HÌNH THƯ MỤC LƯU TRỮ VÀ PHỤC VỤ STATIC FILE CHO UPLOAD ẢNH/TỆP
var uploadsFolder = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsFolder))
{
    Directory.CreateDirectory(uploadsFolder);
}

app.UseStaticFiles(); // Default wwwroot nếu có

// Cấu hình rõ ràng đường dẫn /uploads để truy cập trực tiếp file ảnh
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsFolder),
    RequestPath = "/uploads"
});

// Khởi tạo Database và Seed dữ liệu ban đầu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await DbInitializer.SeedAsync(db);
        Console.WriteLine("--> [Database] Khoi tao va Seed du lieu PostgreSQL thanh cong!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> [Database Error] Khoi tao database that bai: {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowClient");
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();