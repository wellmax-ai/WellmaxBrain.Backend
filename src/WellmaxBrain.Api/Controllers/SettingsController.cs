using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Data;

namespace WellmaxBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SettingsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        return Ok(settings);
    }

    public record SaveSettingsDto(string ModelName, string ApiKey, string SystemPrompt);

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveSettingsDto dto)
    {
        var sModel = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "Gemini:Model");
        if (sModel != null) sModel.Value = dto.ModelName;

        var sKey = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "Gemini:ApiKey");
        if (sKey != null && !string.IsNullOrEmpty(dto.ApiKey)) sKey.Value = dto.ApiKey;

        var sPrompt = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "Gemini:SystemPrompt");
        if (sPrompt != null) sPrompt.Value = dto.SystemPrompt;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Lưu cấu hình AI toàn hệ thống thành công!" });
    }
}
