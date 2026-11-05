using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Application.DTOs;
using WellmaxBrain.Application.Interfaces;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.Infrastructure.Services;

public class GeminiExtractorService : IAIExtractorService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;

    public GeminiExtractorService(HttpClient http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<OrderExtractionDto?> ExtractOrderFromTextAsync(string rawText, string tenantId, string apiKey, string modelName)
    {
        // 1. Nhận diện có SĐT Việt Nam
        var phoneRegex = new Regex(@"(0[3|5|7|8|9]\d{8})");
        if (!phoneRegex.IsMatch(rawText)) return null;

        // 2. Nạp từ điển tri thức tự học (Active Learning)
        var aliases = await _db.LearnedAliases
            .Where(a => a.TenantId == tenantId)
            .Select(a => $"{a.RawPhrase} -> SKU: {a.MatchedSku} ({a.MatchedName})")
            .ToListAsync();

        var dictionaryContext = string.Join("
", aliases);

        // 3. System Prompt bóc tách JSON chính xác
        var systemPrompt = $@"Bạn là Wellmax Brain AI System Agent.
Hãy trích xuất thông tin đơn hàng từ tin nhắn thô.
Bộ nhớ từ điển SKU tham chiếu:
{dictionaryContext}

BẮT BUỘC chỉ trả lời JSON chuẩn theo định dạng sau (không markdown code block, không giải thích thêm):
{{
  ""customerName"": ""Tên khách hàng"",
  ""phoneNumber"": ""0988xxxxxx"",
  ""address"": ""Địa chỉ giao hàng"",
  ""items"": [
    {{ ""sku"": ""WM-SKU"", ""name"": ""Tên sản phẩm"", ""quantity"": 1, ""price"": 350000, ""isGift"": false }}
  ],
  ""totalAmount"": 350000
}}";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = rawText } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1
            }
        };

        var response = await _http.PostAsJsonAsync($"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}", payload);
        if (!response.IsSuccessStatusCode) return null;

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);
        var content = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

        if (string.IsNullOrEmpty(content)) return null;

        return JsonSerializer.Deserialize<OrderExtractionDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}