using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Api.Hubs;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Data;

namespace WellmaxBrain.Api.Services;

public interface IOrderAiService
{
    Task ProcessChannelMessageAsync(string channelId, string senderId, string senderName, string messageText);
}

public class OrderAiService : IOrderAiService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public OrderAiService(IHubContext<ChatHub> hubContext, IServiceScopeFactory scopeFactory, IConfiguration config, HttpClient httpClient)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _config = config;
        _httpClient = httpClient;
    }

    public async Task ProcessChannelMessageAsync(string channelId, string senderId, string senderName, string messageText)
    {
        var apiKey = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"] ?? "gemini-2.5-flash";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await _hubContext.Clients.Group(channelId).SendAsync("ReceiveMessage", new
            {
                Id = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                SenderId = "ai-bot",
                SenderName = "Wellmax AI Assistant",
                IsBot = true,
                Text = "🌿 [Wellmax Brain AI]: Vui lòng cập nhật Gemini:ApiKey trong appsettings.json của Backend để kích hoạt trích xuất đơn hàng tự động.",
                Timestamp = DateTime.Now.ToString("HH:mm")
            });
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Nạp từ điển tri thức đã học
            var aliases = await db.LearnedAliases.AsNoTracking().ToListAsync();
            var aliasContext = string.Join(", ", aliases.Select(a => $"'{a.RawPhrase}' => SKU '{a.MatchedSku}'"));

            var prompt = $@"Bạn là Wellmax Brain AI System - Trợ lý trích xuất đơn hàng từ chat bán hàng.
Dưới đây là từ điển viết tắt/tên gọi địa phương đã học: [{aliasContext}]

Nhiệm vụ: Phân tích tin nhắn sau xem có phải thông tin đơn hàng không:
""{messageText}""

Nếu là đơn hàng, hãy trích xuất sang định dạng JSON:
{{
  ""isOrder"": true,
  ""customerName"": ""Tên khách hàng"",
  ""phone"": ""SĐT"",
  ""address"": ""Địa chỉ"",
  ""items"": [
    {{ ""sku"": ""SKU"", ""productName"": ""Tên SP"", ""qty"": 1, ""price"": 0 }}
  ],
  ""note"": ""Ghi chú""
}}
Nếu không phải đơn hàng, trả lời ngắn gọn hỗ trợ người dùng theo phong cách Wellmax Health & Beauty.";

            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var respJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respJson);
                var textResult = doc.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                await _hubContext.Clients.Group(channelId).SendAsync("ReceiveMessage", new
                {
                    Id = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    SenderId = "ai-bot",
                    SenderName = "Wellmax AI Assistant",
                    IsBot = true,
                    Text = textResult,
                    Timestamp = DateTime.Now.ToString("HH:mm")
                });
            }
        }
        catch (Exception ex)
        {
            await _hubContext.Clients.Group(channelId).SendAsync("ReceiveMessage", new
            {
                Id = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                SenderId = "ai-bot",
                SenderName = "Wellmax AI Assistant",
                IsBot = true,
                Text = $"🌿 [Wellmax Brain AI]: Lỗi xử lý ({ex.Message})",
                Timestamp = DateTime.Now.ToString("HH:mm")
            });
        }
    }
}
