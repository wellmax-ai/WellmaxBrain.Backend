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

    // CHỈ TRẢ VỀ CÁC KÊNH MÀ USERID ĐÓ ĐƯỢC PHÉP THAM GIA (Hoặc là Super Admin)
    [HttpGet]
    public async Task<IActionResult> GetAllChannels([FromQuery] string? userId = null)
    {
        Guid userGuid = Guid.Empty;
        bool hasUserFilter = !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out userGuid);

        bool isSuperAdmin = false;
        if (hasUserFilter)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userGuid);
            isSuperAdmin = user?.IsAdmin == true;
        }

        // Lấy danh sách nhóm
        var query = _db.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .AsQueryable();

        // Nếu không phải Super Admin, CHỈ lấy những nhóm mà user có trong danh sách thành viên hoặc là chủ/phó nhóm
        if (hasUserFilter && !isSuperAdmin)
        {
            query = query.Where(c => c.OwnerId == userGuid || 
                                     c.DeputyIds.Contains(userGuid) || 
                                     c.Members.Any(m => m.UserId == userGuid));
        }

        var channelsList = await query
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.PinnedAtUnix)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = new List<object>();

        // Với mỗi nhóm, lấy chính xác nội dung tin nhắn cuối cùng từ bảng ChatMessages trong PostgreSQL
        foreach (var c in channelsList)
        {
            var lastMsg = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => m.ChannelId == c.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAtUnix)
                .FirstOrDefaultAsync();

            string lastMessageText = "Chưa có tin nhắn mới";
            string lastTimeText = "";

            if (lastMsg != null)
            {
                if (!string.IsNullOrWhiteSpace(lastMsg.Text))
                {
                    lastMessageText = lastMsg.Text;
                }
                else if (!string.IsNullOrEmpty(lastMsg.ImageUrl))
                {
                    lastMessageText = "[Hình ảnh]";
                }
                else if (!string.IsNullOrEmpty(lastMsg.FileUrl))
                {
                    lastMessageText = $"[Tệp tin: {lastMsg.FileName ?? "Tài liệu"}]";
                }
                else if (!string.IsNullOrEmpty(lastMsg.ReminderJson))
                {
                    lastMessageText = "[Nhắc hẹn công việc]";
                }

                lastTimeText = DateTimeOffset.FromUnixTimeMilliseconds(lastMsg.CreatedAtUnix).ToLocalTime().ToString("HH:mm");
            }

            var allMembers = c.Members.Select(m => m.UserId.ToString().ToLowerInvariant()).ToList();
            if (!allMembers.Contains(c.OwnerId.ToString().ToLowerInvariant()))
            {
                allMembers.Add(c.OwnerId.ToString().ToLowerInvariant());
            }

            result.Add(new
            {
                id = c.Id.ToString().ToLowerInvariant(),
                name = c.Name,
                avatar = c.Avatar ?? "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80",
                description = c.Description ?? "Nhóm bán hàng Wellmax",
                ownerId = c.OwnerId.ToString().ToLowerInvariant(),
                deputyIds = c.DeputyIds.Select(d => d.ToString().ToLowerInvariant()).ToList(),
                isPinned = c.IsPinned,
                pinnedAt = c.PinnedAtUnix,
                isHidden = c.IsHidden,
                isGroup = true,
                members = allMembers,
                lastMessage = lastMessageText,
                lastTime = lastTimeText
            });
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateChannel([FromBody] CreateChannelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Tên nhóm không được để trống." });

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
            Description = dto.Description?.Trim() ?? "Nhóm bán hàng mới",
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

        newChannel.Members.Add(new ChannelMember
        {
            ChannelId = newChannel.Id,
            UserId = ownerGuid,
            JoinedAt = DateTime.UtcNow
        });

        var botGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
        newChannel.Members.Add(new ChannelMember
        {
            ChannelId = newChannel.Id,
            UserId = botGuid,
            JoinedAt = DateTime.UtcNow
        });

        _db.Channels.Add(newChannel);
        await _db.SaveChangesAsync();

        var resPayload = new
        {
            id = newChannel.Id.ToString().ToLowerInvariant(),
            name = newChannel.Name,
            avatar = newChannel.Avatar,
            description = newChannel.Description,
            ownerId = newChannel.OwnerId.ToString().ToLowerInvariant(),
            deputyIds = new List<string>(),
            isPinned = newChannel.IsPinned,
            pinnedAt = newChannel.PinnedAtUnix,
            isHidden = newChannel.IsHidden,
            isGroup = true,
            members = newChannel.Members.Select(m => m.UserId.ToString().ToLowerInvariant()).ToList(),
            lastMessage = "Chưa có tin nhắn mới",
            lastTime = ""
        };

        return Ok(resPayload);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest req)
    {
        var channel = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
        if (channel == null) return NotFound(new { message = "Không tìm thấy nhóm bán hàng." });

        if (!Guid.TryParse(req.UserId, out var memberGuid))
        {
            return BadRequest(new { message = "Mã người dùng không hợp lệ." });
        }

        if (channel.Members.Any(m => m.UserId == memberGuid))
        {
            return BadRequest(new { message = "Nhân sự này đã có mặt trong nhóm." });
        }

        channel.Members.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = memberGuid,
            JoinedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var updatedMemberIds = channel.Members.Select(m => m.UserId.ToString().ToLowerInvariant()).ToList();
        if (!updatedMemberIds.Contains(channel.OwnerId.ToString().ToLowerInvariant()))
        {
            updatedMemberIds.Add(channel.OwnerId.ToString().ToLowerInvariant());
        }

        await _hub.Clients.Group(channel.Id.ToString().ToLowerInvariant()).SendAsync("ChannelMembersUpdated", new
        {
            channelId = channel.Id.ToString().ToLowerInvariant(),
            members = updatedMemberIds,
            action = "added",
            userId = req.UserId
        });

        return Ok(new
        {
            message = "Đã thêm thành viên vào nhóm thành công!",
            channelId = channel.Id.ToString().ToLowerInvariant(),
            members = updatedMemberIds
        });
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, [FromQuery] string requesterId)
    {
        var channel = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
        if (channel == null) return NotFound(new { message = "Không tìm thấy nhóm bán hàng." });

        if (!Guid.TryParse(requesterId, out var reqGuid) || channel.OwnerId != reqGuid)
        {
            return StatusCode(403, new { message = "Chỉ có Trưởng nhóm mới có quyền xóa thành viên khỏi nhóm." });
        }

        if (!Guid.TryParse(userId, out var targetMemberGuid))
        {
            return BadRequest(new { message = "Mã thành viên cần xóa không hợp lệ." });
        }

        if (targetMemberGuid == channel.OwnerId)
        {
            return BadRequest(new { message = "Trưởng nhóm không thể tự xóa chính mình khỏi nhóm." });
        }

        var memberRecord = channel.Members.FirstOrDefault(m => m.UserId == targetMemberGuid);
        if (memberRecord != null)
        {
            channel.Members.Remove(memberRecord);
            _db.ChannelMembers.Remove(memberRecord);
            await _db.SaveChangesAsync();
        }

        var updatedMemberIds = channel.Members.Select(m => m.UserId.ToString().ToLowerInvariant()).ToList();
        if (!updatedMemberIds.Contains(channel.OwnerId.ToString().ToLowerInvariant()))
        {
            updatedMemberIds.Add(channel.OwnerId.ToString().ToLowerInvariant());
        }

        await _hub.Clients.Group(channel.Id.ToString().ToLowerInvariant()).SendAsync("ChannelMembersUpdated", new
        {
            channelId = channel.Id.ToString().ToLowerInvariant(),
            members = updatedMemberIds,
            action = "removed",
            userId = userId
        });

        return Ok(new
        {
            message = "Đã xóa thành viên khỏi nhóm thành công!",
            channelId = channel.Id.ToString().ToLowerInvariant(),
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