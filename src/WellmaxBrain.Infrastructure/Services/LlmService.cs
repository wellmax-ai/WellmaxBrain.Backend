using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Application.DTOs;
using WellmaxBrain.Application.Interfaces;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.Infrastructure.Services;

/// <summary>
/// Adapter gọi song song 2 nhà cung cấp LLM: Google Gemini và DeepSeek.
/// Toàn bộ API Key / Model / System Prompt được đọc động từ bảng AiProviderConfigs.
/// </summary>
public class LlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;

    public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    public const string DeepSeekBaseUrl = "https://api.deepseek.com/v1";

    public LlmService(HttpClient http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    private async Task<AiProviderConfig?> GetActiveConfigAsync(CancellationToken ct)
    {
        return await _db.AiProviderConfigs
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LlmResultDto> GenerateAsync(string prompt, string systemPrompt, double temperature, CancellationToken ct = default)
    {
        var cfg = await GetActiveConfigAsync(ct);
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            return new LlmResultDto(false, string.Empty, cfg?.Provider ?? "none", cfg?.ModelName ?? "-", 0,
                "Chưa cấu hình API Key cho LLM. Vui lòng vào Cấu hình AI để nhập API Key.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = cfg.Provider.ToLowerInvariant() switch
            {
                "deepseek" => await CallDeepSeekAsync(cfg, prompt, systemPrompt, temperature, ct),
                _ => await CallGeminiAsync(cfg, prompt, systemPrompt, temperature, ct),
            };
            sw.Stop();
            return result with { LatencyMs = (int)sw.ElapsedMilliseconds, Provider = cfg.Provider, ModelName = cfg.ModelName };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new LlmResultDto(false, string.Empty, cfg.Provider, cfg.ModelName, (int)sw.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<LlmResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        var cfg = await GetActiveConfigAsync(ct);
        var provider = cfg?.Provider ?? "none";
        var model = cfg?.ModelName ?? "-";

        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            return new LlmResultDto(false, string.Empty, provider, model, 0,
                "Chưa có API Key. Hãy nhập API Key và bấm Lưu cấu hình trước khi test.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var res = cfg.Provider.ToLowerInvariant() switch
            {
                "deepseek" => await CallDeepSeekAsync(cfg, "Trả lời đúng 1 từ: OK", "Bạn là trợ lý kiểm tra kết nối. Chỉ trả lời OK.", 0, ct),
                _ => await CallGeminiAsync(cfg, "Trả lời đúng 1 từ: OK", "Bạn là trợ lý kiểm tra kết nối. Chỉ trả lời OK.", 0, ct),
            };
            sw.Stop();
            var ok = res.IsSuccess && !string.IsNullOrWhiteSpace(res.Content);
            return new LlmResultDto(ok, res.Content, provider, model, (int)sw.ElapsedMilliseconds,
                ok ? null : (res.ErrorMessage ?? "Không nhận được phản hồi từ LLM."));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new LlmResultDto(false, string.Empty, provider, model, (int)sw.ElapsedMilliseconds, ex.Message);
        }
    }

    // ================= GEMINI =================
    private async Task<LlmResultDto> CallGeminiAsync(AiProviderConfig cfg, string prompt, string systemPrompt, double temperature, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(cfg.BaseUrl) ? GeminiBaseUrl : cfg.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/models/{cfg.ModelName}:generateContent?key={cfg.ApiKey}";

        var payload = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            systemInstruction = string.IsNullOrWhiteSpace(systemPrompt) ? null : new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { temperature, maxOutputTokens = 2048 }
        };

        using var resp = await _http.PostAsJsonAsync(url, payload, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return new LlmResultDto(false, string.Empty, cfg.Provider, cfg.ModelName, 0, $"HTTP {(int)resp.StatusCode}: {Trim(raw, 400)}");

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return new LlmResultDto(true, text, cfg.Provider, cfg.ModelName, 0, null);
    }

    // ================= DEEPSEEK (OpenAI-compatible) =================
    private async Task<LlmResultDto> CallDeepSeekAsync(AiProviderConfig cfg, string prompt, string systemPrompt, double temperature, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(cfg.BaseUrl) ? DeepSeekBaseUrl : cfg.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/chat/completions";

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        messages.Add(new { role = "user", content = prompt });

        var payload = new { model = cfg.ModelName, messages, temperature, max_tokens = 2048, stream = false };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", $"Bearer {cfg.ApiKey}");
        req.Content = JsonContent.Create(payload);

        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return new LlmResultDto(false, string.Empty, cfg.Provider, cfg.ModelName, 0, $"HTTP {(int)resp.StatusCode}: {Trim(raw, 400)}");

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return new LlmResultDto(true, text, cfg.Provider, cfg.ModelName, 0, null);
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}