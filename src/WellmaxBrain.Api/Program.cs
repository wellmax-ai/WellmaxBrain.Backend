using Microsoft.EntityFrameworkCore;
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
        p.WithOrigins("http://localhost:5173", "http://localhost:3000")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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