using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChannelsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ChannelsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllChannels()
    {
        var channels = await _db.Channels
            .AsNoTracking()
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.PinnedAtUnix)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                id = c.Id.ToString(),
                name = c.Name,
                avatar = c.Avatar ?? "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80",
                description = c.Description ?? "Kênh bán hàng Wellmax",
                ownerId = c.OwnerId.ToString(),
                deputyIds = c.DeputyIds.Select(d => d.ToString()).ToList(),
                isPinned = c.IsPinned,
                pinnedAt = c.PinnedAtUnix,
                isHidden = c.IsHidden,
                isGroup = true,
                unread = 0,
                lastMessage = "Chưa có tin nhắn mới",
                lastTime = c.CreatedAt.ToString("HH:mm")
            })
            .ToListAsync();

        return Ok(channels);
    }

    [HttpPost]
    public async Task<IActionResult> CreateChannel([FromBody] CreateChannelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Tên kênh không được để trống." });

        Guid ownerGuid;
        if (!Guid.TryParse(dto.OwnerId, out ownerGuid))
        {
            ownerGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        var newChannel = new Channel
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? "Kênh bán hàng mới",
            Avatar = string.IsNullOrWhiteSpace(dto.Avatar) 
                ? "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80" 
                : dto.Avatar.Trim(),
            OwnerId = ownerGuid,
            DeputyIds = new List<Guid>(),
            IsPinned = false,
            PinnedAtUnix = 0,
            IsHidden = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Channels.Add(newChannel);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = newChannel.Id.ToString(),
            name = newChannel.Name,
            avatar = newChannel.Avatar,
            description = newChannel.Description,
            ownerId = newChannel.OwnerId.ToString(),
            deputyIds = new List<string>(),
            isPinned = newChannel.IsPinned,
            pinnedAt = newChannel.PinnedAtUnix,
            isHidden = newChannel.IsHidden,
            isGroup = true,
            unread = 0,
            lastMessage = "Kênh mới đã được tạo",
            lastTime = DateTime.UtcNow.ToString("HH:mm")
        });
    }

    [HttpPut("{id}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        var channel = await _db.Channels.FindAsync(id);
        if (channel == null) return NotFound();

        channel.IsPinned = !channel.IsPinned;
        channel.PinnedAtUnix = channel.IsPinned ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 0;
        await _db.SaveChangesAsync();

        return Ok(new { isPinned = channel.IsPinned });
    }
}

public record CreateChannelDto(string Name, string? Description, string? Avatar, string? OwnerId);