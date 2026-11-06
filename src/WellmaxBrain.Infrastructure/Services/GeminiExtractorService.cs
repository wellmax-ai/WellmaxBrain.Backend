using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Application.DTOs;
using WellmaxBrain.Application.Interfaces;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.Infrastructure.Services;

/// <summary>
/// Trich xuat don hang tu text bang LLM (Gemini hoac DeepSeek) - API Key doc dong tu Database.
/// </summary>
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
        var phoneRegex = new Regex(@"(0[3|5|7|8|9]\d{8})");
        if (!phoneRegex.IsMatch(rawText)) return null;

        var cfg = await _db.AiProviderConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();

        var effectiveKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : (cfg?.ApiKey ?? string.Empty);
        if (string.IsNullOrWhiteSpace(effectiveKey)) return null;

        var provider = cfg?.Provider?.ToLowerInvariant() ?? "gemini";
        var effectiveModel = !string.IsNullOrWhiteSpace(modelName) ? modelName : (cfg?.ModelName ?? "gemini-2.5-flash");

        var aliases = await _db.LearnedAliases
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.RawPhrase + " -> SKU: " + a.MatchedSku + " (" + a.MatchedName + ")")
            .ToListAsync();

        var dictionaryContext = string.Join("; ", aliases);

        var systemPrompt = $"Ban la Wellmax Brain AI System Agent. Hay trich xuat thong tin don hang tu tin nhan that." +
            (string.IsNullOrWhiteSpace(dictionaryContext) ? "" : $" Tu dien SKU tham chieu: {dictionaryContext}") +
            " BAT BUOC chi tra ve JSON thuan (khong markdown, khong giai thich) theo dinh dang: " +
            "{\"customerName\":\"Ten khach\",\"phoneNumber\":\"0988xxxxxx\",\"address\":\"Dia chi giao hang\"," +
            "\"items\":[{\"sku\":\"WM-SKU\",\"name\":\"Ten san pham\",\"quantity\":1,\"price\":350000,\"isGift\":false}],\"totalAmount\":350000}";

        string content;
        var sw = Stopwatch.StartNew();
        try
        {
            if (provider == "deepseek")
                content = await CallDeepSeekAsync(cfg, rawText, systemPrompt, effectiveKey);
            else
                content = await CallGeminiAsync(cfg, rawText, systemPrompt, effectiveKey);
        }
        catch (Exception ex)
        {
            _db.AiActivityLogs.Add(new AiActivityLog
            {
                TenantId = tenantId, Action = "ExtractOrder", Provider = provider,
                ModelName = effectiveModel, RequestSummary = rawText,
                IsSuccess = false, LatencyMs = (int)sw.ElapsedMilliseconds, ErrorMessage = ex.Message
            });
            await _db.SaveChangesAsync();
            return null;
        }

        sw.Stop();
        if (string.IsNullOrWhiteSpace(content))
        {
            _db.AiActivityLogs.Add(new AiActivityLog
            {
                TenantId = tenantId, Action = "ExtractOrder", Provider = provider,
                ModelName = effectiveModel, RequestSummary = rawText,
                IsSuccess = false, LatencyMs = (int)sw.ElapsedMilliseconds, ErrorMessage = "Empty response"
            });
            await _db.SaveChangesAsync();
            return null;
        }

        var clean = StripMarkdown(content);
        OrderExtractionDto? result = null;
        try
        {
            result = JsonSerializer.Deserialize<OrderExtractionDto>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _db.AiActivityLogs.Add(new AiActivityLog
            {
                TenantId = tenantId, Action = "ExtractOrder", Provider = provider,
                ModelName = effectiveModel, RequestSummary = rawText, ResponseSummary = clean,
                IsSuccess = false, LatencyMs = (int)sw.ElapsedMilliseconds, ErrorMessage = "JSON parse: " + ex.Message
            });
            await _db.SaveChangesAsync();
            return null;
        }

        _db.AiActivityLogs.Add(new AiActivityLog
        {
            TenantId = tenantId, Action = "ExtractOrder", Provider = provider,
            ModelName = effectiveModel, RequestSummary = rawText, ResponseSummary = clean,
            IsSuccess = result != null && !string.IsNullOrEmpty(result.PhoneNumber), LatencyMs = (int)sw.ElapsedMilliseconds
        });
        await _db.SaveChangesAsync();

        return result;
    }

    private async Task<string> CallGeminiAsync(AiProviderConfig? cfg, string prompt, string systemPrompt, string apiKey)
    {
        var baseUrl = string.IsNullOrWhiteSpace(cfg?.BaseUrl) ? LlmService.GeminiBaseUrl : cfg!.BaseUrl!.TrimEnd('/');
        var model = cfg?.ModelName ?? "gemini-2.5-flash";
        var payload = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { temperature = 0.1, responseMimeType = "application/json", maxOutputTokens = 2048 }
        };
        using var resp = await _http.PostAsJsonAsync($"{baseUrl}/models/{model}:generateContent?key={apiKey}", payload);
        var raw = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception($"Gemini HTTP {(int)resp.StatusCode}: {raw}");
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> CallDeepSeekAsync(AiProviderConfig? cfg, string prompt, string systemPrompt, string apiKey)
    {
        var baseUrl = string.IsNullOrWhiteSpace(cfg?.BaseUrl) ? LlmService.DeepSeekBaseUrl : cfg!.BaseUrl!.TrimEnd('/');
        var model = cfg?.ModelName ?? "deepseek-chat";
        var payload = new
        {
            model,
            messages = new object[] { new { role = "system", content = systemPrompt }, new { role = "user", content = prompt } },
            temperature = 0.1,
            max_tokens = 2048,
            response_format = new { type = "json_object" },
            stream = false
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = JsonContent.Create(payload);
        using var resp = await _http.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception($"DeepSeek HTTP {(int)resp.StatusCode}: {raw}");
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static string StripMarkdown(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("```"))
        {
            var idx = t.IndexOf('\n');
            if (idx > 0) t = t.Substring(idx + 1);
            if (t.EndsWith("```")) t = t.Substring(0, t.Length - 3);
        }
        return t.Trim();
    }
}