using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.API.Hubs;
using WellmaxBrain.Application.DTOs;
using WellmaxBrain.Application.Interfaces;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;
using WellmaxBrain.Infrastructure.Services;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILlmService _llm;
    private readonly IAIExtractorService _extractor;
    private readonly IHubContext<ChatHub> _hub;
    private const string Tenant = "wellmax-default";

    public AiController(AppDbContext db, ILlmService llm, IAIExtractorService extractor, IHubContext<ChatHub> hub)
    {
        _db = db;
        _llm = llm;
        _extractor = extractor;
        _hub = hub;
    }

    // ================== CẤU HÌNH PROVIDER ==================

    [HttpGet("config")]
    public async Task<IActionResult> GetConfigs()
    {
        var list = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.TenantId == Tenant)
            .OrderBy(c => c.Provider)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    [HttpPost("config")]
    public async Task<IActionResult> SaveConfig([FromBody] SaveAiProviderConfigRequest req)
    {
        var provider = (req.Provider ?? "gemini").Trim().ToLowerInvariant();
        if (provider != "gemini" && provider != "deepseek")
            return BadRequest(new { message = "Provider chỉ hỗ trợ gemini hoặc deepseek." });

        var cfg = await _db.AiProviderConfigs
            .FirstOrDefaultAsync(c => c.TenantId == Tenant && c.Provider == provider);

        if (cfg == null)
        {
            cfg = new AiProviderConfig { TenantId = Tenant, Provider = provider };
            _db.AiProviderConfigs.Add(cfg);
        }

        // Chỉ ghi đè API Key khi có giá trị mới
        if (!string.IsNullOrWhiteSpace(req.ApiKey) && !req.ApiKey.Contains("****"))
            cfg.ApiKey = req.ApiKey.Trim();

        if (!string.IsNullOrWhiteSpace(req.ModelName)) cfg.ModelName = req.ModelName.Trim();
        cfg.BaseUrl = string.IsNullOrWhiteSpace(req.BaseUrl) ? null : req.BaseUrl.Trim();
        cfg.SystemPrompt = string.IsNullOrWhiteSpace(req.SystemPrompt) ? AiProviderConfig.DefaultSystemPrompt : req.SystemPrompt;
        cfg.Temperature = req.Temperature <= 0 ? 0.4 : req.Temperature;
        cfg.UpdatedAt = DateTime.UtcNow;
        cfg.UpdatedByUserId = req.UpdatedByUserId;

        // Chỉ 1 provider được active tại một thời điểm -> provider vừa lưu sẽ thành active
        var others = await _db.AiProviderConfigs.Where(c => c.TenantId == Tenant && c.Provider != provider).ToListAsync();
        foreach (var o in others) o.IsActive = false;
        cfg.IsActive = true;

        await _db.SaveChangesAsync();
        return Ok(ToDto(cfg));
    }

    [HttpPost("config/{provider}/activate")]
    public async Task<IActionResult> Activate(string provider)
    {
        var p = provider.Trim().ToLowerInvariant();
        var all = await _db.AiProviderConfigs.Where(c => c.TenantId == Tenant).ToListAsync();
        var target = all.FirstOrDefault(c => c.Provider == p);
        if (target == null) return NotFound(new { message = "Chưa có cấu hình cho provider này." });

        foreach (var c in all) c.IsActive = c.Provider == p;
        await _db.SaveChangesAsync();
        return Ok(new { message = $"Đã kích hoạt {p}." });
    }

    // ================== TEST KẾT NỐI ==================

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        var r = await _llm.TestConnectionAsync();
        var active = await _db.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == Tenant && c.IsActive);

        _db.AiActivityLogs.Add(new AiActivityLog
        {
            TenantId = Tenant,
            Action = "TestConnection",
            Provider = r.Provider,
            ModelName = r.ModelName,
            RequestSummary = "Ping test",
            ResponseSummary = r.Content,
            IsSuccess = r.IsSuccess,
            LatencyMs = r.LatencyMs,
            ErrorMessage = r.ErrorMessage
        });
        await _db.SaveChangesAsync();

        return Ok(new TestAiConnectionResult(
            r.IsSuccess, r.Provider, r.ModelName, r.LatencyMs,
            r.IsSuccess ? "Kết nối LLM thành công!" : r.ErrorMessage,
            r.Content
        ));
    }

    // ================== TRAINING DATA ==================

    [HttpGet("training")]
    public async Task<IActionResult> GetTraining()
    {
        var list = await _db.AiTrainingSet.AsNoTracking()
            .Where(t => t.TenantId == Tenant)
            .OrderByDescending(t => t.Priority).ThenByDescending(t => t.UpdatedAt)
            .ToListAsync();

        return Ok(list.Select(t => new TrainingDataDto(
            t.Id, t.Title, t.Content, t.SourceType.ToString(), t.SourceRef,
            t.ChannelId?.ToString(), t.Priority, t.IsActive, t.CreatedAt, t.UpdatedAt)));
    }

    [HttpPost("training")]
    public async Task<IActionResult> CreateTraining([FromBody] CreateTrainingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "Nội dung huấn luyện không được rỗng." });

        var source = Enum.TryParse<AiTrainingSource>(req.SourceType, true, out var s) ? s : AiTrainingSource.KnowledgeFile;

        var entity = new AiTrainingData
        {
            TenantId = Tenant,
            Title = string.IsNullOrWhiteSpace(req.Title) ? "Huấn luyện " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") : req.Title.Trim(),
            Content = req.Content.Trim(),
            SourceType = source,
            SourceRef = req.SourceRef,
            ChannelId = Guid.TryParse(req.ChannelId, out var cid) ? cid : null,
            CreatedByUserId = req.CreatedByUserId,
            Priority = req.Priority,
            IsActive = true
        };

        _db.AiTrainingSet.Add(entity);
        _db.AiActivityLogs.Add(new AiActivityLog
        {
            TenantId = Tenant,
            ChannelId = entity.ChannelId,
            Action = "Training",
            Provider = "-",
            ModelName = "-",
            RequestSummary = entity.Title,
            ResponseSummary = $"Đã lưu {entity.Content.Length} ký tự tri thức ({source}).",
            IsSuccess = true
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã lưu dữ liệu huấn luyện.", id = entity.Id });
    }

    [HttpPut("training/{id:guid}")]
    public async Task<IActionResult> UpdateTraining(Guid id, [FromBody] CreateTrainingRequest req)
    {
        var e = await _db.AiTrainingSet.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == Tenant);
        if (e == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Title)) e.Title = req.Title.Trim();
        if (!string.IsNullOrWhiteSpace(req.Content)) e.Content = req.Content.Trim();
        e.Priority = req.Priority;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã cập nhật." });
    }

    [HttpDelete("training/{id:guid}")]
    public async Task<IActionResult> DeleteTraining(Guid id)
    {
        var e = await _db.AiTrainingSet.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == Tenant);
        if (e == null) return NotFound();

        _db.AiTrainingSet.Remove(e);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã xóa dữ liệu huấn luyện." });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int take = 50)
    {
        var list = await _db.AiActivityLogs.AsNoTracking()
            .Where(l => l.TenantId == Tenant)
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .ToListAsync();

        return Ok(list.Select(l => new AiActivityLogDto(
            l.Id, l.Action, l.Provider, l.ModelName,
            l.RequestSummary, l.ResponseSummary, l.IsSuccess, l.LatencyMs, l.ErrorMessage, l.CreatedAt)));
    }

    // ================== AGENT CHAT (tag @AI trong nhóm) ==================

    [HttpPost("agent/chat")]
    public async Task<IActionResult> AgentChat([FromBody] AiAgentChatRequest req)
    {
        var sw = Stopwatch.StartNew();

        // 1. Thu thập dữ liệu huấn luyện động
        var trainingBlocks = await _db.AiTrainingSet.AsNoTracking()
            .Where(t => t.TenantId == Tenant && t.IsActive)
            .OrderByDescending(t => t.Priority).ThenByDescending(t => t.UpdatedAt)
            .Take(20)
            .Select(t => $"### {t.Title}\n{t.Content}")
            .ToListAsync();

        var activeCfg = await _db.AiProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == Tenant && c.IsActive);
        var basePrompt = activeCfg?.SystemPrompt ?? AiProviderConfig.DefaultSystemPrompt;

        var fullSystemPrompt = basePrompt;
        if (trainingBlocks.Any())
        {
            fullSystemPrompt += "\n\n===== TRI THỨC HUẤN LUYỆN NỘI BỘ WELLMAX =====\n" +
                                string.Join("\n\n", trainingBlocks) +
                                "\n===== HẾT TRI THỨC =====";
        }

        // 2. Gọi LLM trả lời
        var llm = await _llm.GenerateAsync(req.Message, fullSystemPrompt, activeCfg?.Temperature ?? 0.4);

        // 3. Trích xuất đơn hàng nếu phát hiện SĐT
        OrderExtractionDto? order = null;
        if (llm.IsSuccess && System.Text.RegularExpressions.Regex.IsMatch(req.Message, @"0[3|5|7|8|9]\d{8}"))
        {
            try { order = await _extractor.ExtractOrderFromTextAsync(req.Message, Tenant, string.Empty, string.Empty); }
            catch (Exception ex) { Console.WriteLine("[ExtractOrder] " + ex.Message); }
        }

        sw.Stop();

        _db.AiActivityLogs.Add(new AiActivityLog
        {
            TenantId = Tenant,
            ChannelId = Guid.TryParse(req.ChannelId, out var ch) ? ch : null,
            Action = "ChatReply",
            Provider = llm.Provider,
            ModelName = llm.ModelName,
            RequestSummary = req.Message.Length > 500 ? req.Message.Substring(0, 500) : req.Message,
            ResponseSummary = llm.Content.Length > 500 ? llm.Content.Substring(0, 500) : llm.Content,
            IsSuccess = llm.IsSuccess,
            LatencyMs = llm.LatencyMs,
            ErrorMessage = llm.ErrorMessage
        });
        await _db.SaveChangesAsync();

        return Ok(new AiAgentChatResponse(
            llm.IsSuccess, llm.Content, llm.Provider, llm.ModelName, llm.LatencyMs, order, llm.ErrorMessage));
    }

    private static AiProviderConfigDto ToDto(AiProviderConfig c) => new(
        c.Id, c.Provider, c.ModelName,
        c.BaseUrl ?? (c.Provider == "deepseek" ? LlmService.DeepSeekBaseUrl : LlmService.GeminiBaseUrl),
        c.SystemPrompt, c.Temperature, c.IsActive,
        !string.IsNullOrWhiteSpace(c.ApiKey),
        string.IsNullOrWhiteSpace(c.ApiKey) ? "" : "****" + c.ApiKey[^Math.Min(4, c.ApiKey.Length)..],
        c.UpdatedAt
    );
}