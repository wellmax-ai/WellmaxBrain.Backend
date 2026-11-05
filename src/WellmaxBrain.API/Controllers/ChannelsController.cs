using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.API.Hubs;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChannelsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public ChannelsController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllChannels()
    {
        var channels = await _db.Channels
            .AsNoTracking()
            .Include(c => c.Members)
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
                members = c.Members.Select(m => m.UserId.ToString()).ToList(),
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

        // Tự động thêm Trưởng nhóm và Bot vào danh sách thành viên kênh
        newChannel.Members.Add(new ChannelMember
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            ChannelId = newChannel.Id,
            UserId = ownerGuid,
            Role = ChannelRole.Owner,
            JoinedAt = DateTime.UtcNow
        });

        var botGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
        newChannel.Members.Add(new ChannelMember
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            ChannelId = newChannel.Id,
            UserId = botGuid,
            Role = ChannelRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        _db.Channels.Add(newChannel);
        await _db.SaveChangesAsync();

        var resPayload = new
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
            members = newChannel.Members.Select(m => m.UserId.ToString()).ToList(),
            lastMessage = "Kênh mới đã được tạo",
            lastTime = DateTime.UtcNow.ToString("HH:mm")
        };

        return Ok(resPayload);
    }

    // THÊM THÀNH VIÊN VÀO KÊNH (Trưởng nhóm hoặc Thành viên được phân quyền)
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest req)
    {
        var channel = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
        if (channel == null) return NotFound(new { message = "Không tìm thấy kênh bán hàng." });

        if (!Guid.TryParse(req.UserId, out var memberGuid))
        {
            return BadRequest(new { message = "Mã người dùng không hợp lệ." });
        }

        // Kiểm tra xem đã có trong kênh chưa
        if (channel.Members.Any(m => m.UserId == memberGuid))
        {
            return BadRequest(new { message = "Nhân sự này đã có mặt trong kênh." });
        }

        channel.Members.Add(new ChannelMember
        {
            Id = Guid.NewGuid(),
            TenantId = channel.TenantId,
            ChannelId = channel.Id,
            UserId = memberGuid,
            Role = ChannelRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var updatedMemberIds = channel.Members.Select(m => m.UserId.ToString()).ToList();

        // Broadcast realtime qua SignalR để mọi người cập nhật danh sách thành viên kênh
        await _hub.Clients.Group(channel.Id.ToString().ToLowerInvariant()).SendAsync("ChannelMembersUpdated", new
        {
            channelId = channel.Id.ToString(),
            members = updatedMemberIds,
            action = "added",
            userId = req.UserId
        });

        return Ok(new
        {
            message = "Đã thêm thành viên vào kênh thành công!",
            channelId = channel.Id.ToString(),
            members = updatedMemberIds
        });
    }

    // XÓA THÀNH VIÊN KHỎI KÊNH (Chỉ Trưởng nhóm có quyền)
    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, [FromQuery] string requesterId)
    {
        var channel = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
        if (channel == null) return NotFound(new { message = "Không tìm thấy kênh bán hàng." });

        // Kiểm tra quyền: Người gọi API phải là Trưởng nhóm (Owner)
        if (!Guid.TryParse(requesterId, out var reqGuid) || channel.OwnerId != reqGuid)
        {
            return StatusCode(403, new { message = "Chỉ có Trưởng nhóm mới có quyền xóa thành viên khỏi kênh." });
        }

        if (!Guid.TryParse(userId, out var targetMemberGuid))
        {
            return BadRequest(new { message = "Mã thành viên cần xóa không hợp lệ." });
        }

        if (targetMemberGuid == channel.OwnerId)
        {
            return BadRequest(new { message = "Trưởng nhóm không thể tự xóa chính mình khỏi kênh." });
        }

        var memberRecord = channel.Members.FirstOrDefault(m => m.UserId == targetMemberGuid);
        if (memberRecord != null)
        {
            channel.Members.Remove(memberRecord);
            _db.ChannelMembers.Remove(memberRecord);
            await _db.SaveChangesAsync();
        }

        var updatedMemberIds = channel.Members.Select(m => m.UserId.ToString()).ToList();

        // Broadcast realtime qua SignalR tới tất cả người dùng trong phòng
        await _hub.Clients.Group(channel.Id.ToString().ToLowerInvariant()).SendAsync("ChannelMembersUpdated", new
        {
            channelId = channel.Id.ToString(),
            members = updatedMemberIds,
            action = "removed",
            userId = userId
        });

        return Ok(new
        {
            message = "Đã xóa thành viên khỏi kênh thành công!",
            channelId = channel.Id.ToString(),
            members = updatedMemberIds
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
public record AddMemberRequest(string UserId);