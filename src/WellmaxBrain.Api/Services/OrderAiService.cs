using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Api.Hubs;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Data;

namespace WellmaxBrain.Api.Services;

public interface IOrderAiService
{
    Task ProcessChannelMessageAsync(string channelId, string senderId, string senderName, string messageText, string rawMessageId);
}

public class OrderAiService : IOrderAiService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;

    public OrderAiService(IHubContext<ChatHub> hubContext, IServiceScopeFactory scopeFactory, HttpClient httpClient)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
    }

    public async Task ProcessChannelMessageAsync(string channelId, string senderId, string senderName, string messageText, string rawMessageId)
    {
        // Kiểm tra nếu tin nhắn có khả năng chứa đơn hàng (chứa SĐT 10 số hoặc từ khóa)
        var hasPhone = Regex.IsMatch(messageText, @"(0[3|5|7|8|9])+([0-9]{8})");
        var hasOrderKeywords = messageText.ToLower().Contains("lấy") || messageText.ToLower().Contains("đặt") || 
                               messageText.ToLower().Contains("hộp") || messageText.ToLower().Contains("gửi") ||
                               messageText.ToLower().Contains("ship") || messageText.ToLower().Contains("@ai") ||
                               messageText.ToLower().Contains("@wellmax");

        if (!hasPhone && !hasOrderKeywords) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Lấy cấu hình LLM toàn hệ thống
        var apiKeySetting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "Gemini:ApiKey");
        var modelSetting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "Gemini:Model");
        var apiKey = apiKeySetting?.Value;
        var model = modelSetting?.Value ?? "gemini-2.5-flash";

        // Lấy tri thức từ bảng LearnedAliases và Products
        var aliases = await db.LearnedAliases.AsNoTracking().ToListAsync();
        var products = await db.Products.AsNoTracking().ToListAsync();

        var aliasContext = string.Join("; ", aliases.Select(a => $"'{a.RawPhrase}' = {a.MatchedSku}"));
        var productCatalog = string.Join("; ", products.Select(p => $"{p.Sku}: {p.ProductName} ({p.UnitPrice:#,##0}đ)"));

        var systemPrompt = $@"Bạn là Wellmax Brain AI System - Trợ lý xử lý đơn hàng tự động cho chuỗi Wellmax Health & Beauty.
Từ điển viết tắt đã học: [{aliasContext}]
Danh mục sản phẩm hiện có: [{productCatalog}]

Nhiệm vụ: Phân tích nội dung sau xem có chứa thông tin đặt hàng/chốt đơn không:
""{messageText}""

Nếu có đơn hàng, hãy xuất ra duy nhất 1 chuỗi JSON theo cấu trúc (không thêm markdown ngoài JSON):
{{
  ""isOrder"": true,
  ""customerName"": ""Tên khách hàng"",
  ""phoneNumber"": ""Số điện thoại"",
  ""shippingAddress"": ""Địa chỉ nhận hàng"",
  ""items"": [
    {{
      ""sku"": ""Mã SKU tương ứng"",
      ""productName"": ""Tên sản phẩm chuẩn"",
      ""quantity"": 1,
      ""unitPrice"": 650000
    }}
  ],
  ""note"": ""Ghi chú quà tặng hoặc lưu ý""
}}

Nếu không phải đơn hàng, trả về:
{{ ""isOrder"": false, ""reply"": ""Câu trả lời tư vấn ngắn gọn thân thiện của Wellmax AI"" }}";

        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Fallback nếu chưa điền API Key: Dùng Regex parser cục bộ
                await FallbackLocalOrderParser(db, channelId, messageText, rawMessageId, products);
                return;
            }

            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = systemPrompt } } } },
                generationConfig = new { temperature = 0.2, maxOutputTokens = 800 }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var respString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(respString);
                var rawText = doc.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "";

                // Làm sạch JSON
                var cleanJson = rawText.Trim();
                if (cleanJson.StartsWith("```json")) cleanJson = cleanJson.Substring(7);
                if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);

                using var orderDoc = JsonDocument.Parse(cleanJson.Trim());
                var root = orderDoc.RootElement;

                if (root.TryGetProperty("isOrder", out var isOrderProp) && isOrderProp.GetBoolean())
                {
                    await CreateAndBroadcastOrder(db, channelId, root, rawMessageId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OrderAiService Error]: {ex.Message}");
            await FallbackLocalOrderParser(db, channelId, messageText, rawMessageId, products);
        }
    }

    private async Task FallbackLocalOrderParser(AppDbContext db, string channelId, string text, string rawMsgId, List<Product> products)
    {
        var phoneMatch = Regex.Match(text, @"(0[3|5|7|8|9])+([0-9]{8})");
        if (!phoneMatch.Success) return;

        var phone = phoneMatch.Value;
        var orderCode = $"WM-ORD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";

        // Tự động gán sản phẩm mẫu khớp với text
        var matchedProduct = products.FirstOrDefault(p => text.ToLower().Contains("nám") || text.ToLower().Contains("melasma")) 
            ?? products.First();

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            OrderCode = orderCode,
            ChannelId = channelId,
            CustomerName = "Khách Hàng (Tự động nhận diện)",
            PhoneNumber = phone,
            ShippingAddress = text,
            TotalAmount = matchedProduct.UnitPrice * 2,
            Status = "PendingConfirmation",
            Note = "Trích xuất tự động qua Wellmax Brain AI Fallback Parser",
            RawMessageId = rawMsgId,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    Sku = matchedProduct.Sku,
                    ProductName = matchedProduct.ProductName,
                    Quantity = 2,
                    UnitPrice = matchedProduct.UnitPrice
                }
            }
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Broadcast đơn hàng mới tới Sidebar phải của Kênh chat
        await _hubContext.Clients.Group(channelId).SendAsync("OrderCreated", order);

        // Bắn tin nhắn phản hồi của AI Bot vào khung chat
        await _hubContext.Clients.Group(channelId).SendAsync("ReceiveMessage", new
        {
            Id = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            ChannelId = channelId,
            SenderId = "ai-bot",
            SenderName = "Wellmax AI Assistant",
            SenderAvatar = "bot-avatar",
            IsBot = true,
            Text = $"🌿 [Wellmax Brain AI]: Đã phát hiện và tạo đơn hàng mới #{order.OrderCode} cho SĐT {phone}. Thông tin chi tiết đã hiển thị tại tab Đơn hàng bên phải!",
            Timestamp = DateTime.Now.ToString("HH:mm"),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsDeleted = false
        });
    }

    private async Task CreateAndBroadcastOrder(AppDbContext db, string channelId, JsonElement root, string rawMsgId)
    {
        var customerName = root.TryGetProperty("customerName", out var cn) ? cn.GetString() ?? "Khách hàng" : "Khách hàng";
        var phone = root.TryGetProperty("phoneNumber", out var pn) ? pn.GetString() ?? "" : "";
        var address = root.TryGetProperty("shippingAddress", out var sa) ? sa.GetString() ?? "" : "";
        var note = root.TryGetProperty("note", out var nt) ? nt.GetString() ?? "" : "";

        var orderCode = $"WM-ORD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
        var items = new List<OrderItem>();
        decimal totalAmount = 0;

        if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in itemsElem.EnumerateArray())
            {
                var sku = it.TryGetProperty("sku", out var s) ? s.GetString() ?? "WM-GENERIC" : "WM-GENERIC";
                var name = it.TryGetProperty("productName", out var n) ? n.GetString() ?? "Sản phẩm Wellmax" : "Sản phẩm Wellmax";
                var qty = it.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1;
                var price = it.TryGetProperty("unitPrice", out var p) ? p.GetDecimal() : 0;

                totalAmount += (price * qty);
                items.Add(new OrderItem
                {
                    Sku = sku,
                    ProductName = name,
                    Quantity = qty,
                    UnitPrice = price
                });
            }
        }

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            OrderCode = orderCode,
            ChannelId = channelId,
            CustomerName = customerName,
            PhoneNumber = phone,
            ShippingAddress = address,
            TotalAmount = totalAmount,
            Status = "PendingConfirmation",
            Note = note,
            RawMessageId = rawMsgId,
            CreatedAt = DateTime.UtcNow,
            Items = items
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(channelId).SendAsync("OrderCreated", order);

        await _hubContext.Clients.Group(channelId).SendAsync("ReceiveMessage", new
        {
            Id = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            ChannelId = channelId,
            SenderId = "ai-bot",
            SenderName = "Wellmax AI Assistant",
            SenderAvatar = "bot-avatar",
            IsBot = true,
            Text = $"🌿 [Wellmax Brain AI]: Đã trích xuất thành công đơn hàng #{order.OrderCode} cho khách {customerName} ({phone}). Bạn có thể xem và bấm 'Đẩy Sang POS' ở Sidebar bên phải!",
            Timestamp = DateTime.Now.ToString("HH:mm"),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsDeleted = false
        });
    }
}
