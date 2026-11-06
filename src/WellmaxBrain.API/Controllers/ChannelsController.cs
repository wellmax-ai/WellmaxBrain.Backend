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

        // SỬA TẠI ĐÂY: Bỏ c.DeputyIds.Contains(...) để EF Core dịch được sang SQL
        if (hasUserFilter && !isSuperAdmin)
        {
            query = query.Where(c => c.OwnerId == userGuid ||
                                     c.Members.Any(m => m.UserId == userGuid));
        }

        var channelsList = await query
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.PinnedAtUnix)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = new List<object>();

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

        User? ownerUser = null;
        if (Guid.TryParse(dto.OwnerId, out var parsedGuid))
        {
            ownerUser = await _db.Users.FindAsync(parsedGuid);
        }

        if (ownerUser == null)
        {
            ownerUser = await _db.Users.FirstOrDefaultAsync(u => u.IsAdmin) 
                     ?? await _db.Users.FirstAsync();
        }

        var newChannel = new Channel
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? "Nhóm trao đổi công việc Wellmax",
            Avatar = string.IsNullOrWhiteSpace(dto.Avatar) 
                ? "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80" 
                : dto.Avatar.Trim(),
            OwnerId = ownerUser.Id,
            DeputyIds = new List<Guid>(),
            IsPinned = false,
            PinnedAtUnix = 0,
            IsHidden = false,
            CreatedAt = DateTime.UtcNow
        };

        newChannel.Members.Add(new ChannelMember
        {
            ChannelId = newChannel.Id,
            UserId = ownerUser.Id,
            JoinedAt = DateTime.UtcNow
        });

        var botUser = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == "19008888");
        if (botUser != null && botUser.Id != ownerUser.Id)
        {
            newChannel.Members.Add(new ChannelMember
            {
                ChannelId = newChannel.Id,
                UserId = botUser.Id,
                JoinedAt = DateTime.UtcNow
            });
        }

        _db.Channels.Add(newChannel);
        await _db.SaveChangesAsync();

        var memberIds = newChannel.Members.Select(m => m.UserId.ToString().ToLowerInvariant()).ToList();

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
            members = memberIds,
            lastMessage = "Chưa có tin nhắn mới",
            lastTime = ""
        };

        await _hub.Clients.All.SendAsync("ChannelCreatedOrUpdated", resPayload);

        return Ok(resPayload);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(string id, [FromBody] AddMemberRequest? req)
    {
        try
        {
            // 1. Kiểm tra định dạng Guid của Channel ID
            if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var channelGuid))
            {
                return BadRequest(new { message = "Mã nhóm không hợp lệ hoặc chưa chọn nhóm." });
            }

            // 2. Tìm kiếm Channel và nạp kèm danh sách Members
            var channel = await _db.Channels
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.Id == channelGuid);

            if (channel == null)
            {
                return NotFound(new { message = "Không tìm thấy nhóm trong cơ sở dữ liệu." });
            }

            // 3. Kiểm tra payload và định dạng Guid của User cần thêm
            if (req == null || string.IsNullOrWhiteSpace(req.UserId) || !Guid.TryParse(req.UserId, out var memberGuid))
            {
                return BadRequest(new { message = "Mã người dùng cần thêm không hợp lệ." });
            }

            // 4. Xác minh User có tồn tại trong hệ thống (tránh lỗi Foreign Key Constraint)
            var targetUser = await _db.Users.FindAsync(memberGuid);
            if (targetUser == null)
            {
                return NotFound(new { message = "Người dùng cần thêm không tồn tại trong hệ thống." });
            }

            // 5. Kiểm tra thành viên đã có trong nhóm chưa
            if (channel.Members.Any(m => m.UserId == memberGuid))
            {
                return BadRequest(new { message = "Nhân sự này đã có mặt trong nhóm." });
            }

            // 6. Kiểm tra trực tiếp bảng ChannelMembers để tránh xung đột Tracking/Concurrency
            var existingRecord = await _db.ChannelMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ChannelId == channelGuid && m.UserId == memberGuid);

            if (existingRecord != null)
            {
                return BadRequest(new { message = "Bản ghi thành viên đã tồn tại trong cơ sở dữ liệu." });
            }

            // 7. Tạo thực thể mới và lưu Database
            var newMember = new ChannelMember
            {
                ChannelId = channel.Id,
                UserId = memberGuid,
                JoinedAt = DateTime.UtcNow
            };

            // Nếu Entity của bạn có thuộc tính TenantId, bỏ comment dòng dưới:
            // newMember.TenantId = channel.TenantId;

            _db.ChannelMembers.Add(newMember);
            await _db.SaveChangesAsync();

            // 8. Chuẩn bị danh sách thành viên cập nhật
            var updatedMemberIds = await _db.ChannelMembers
                .Where(m => m.ChannelId == channelGuid)
                .Select(m => m.UserId.ToString().ToLowerInvariant())
                .ToListAsync();

            if (!updatedMemberIds.Contains(channel.OwnerId.ToString().ToLowerInvariant()))
            {
                updatedMemberIds.Add(channel.OwnerId.ToString().ToLowerInvariant());
            }

            var channelPayload = new
            {
                id = channel.Id.ToString().ToLowerInvariant(),
                name = channel.Name,
                avatar = channel.Avatar,
                description = channel.Description,
                ownerId = channel.OwnerId.ToString().ToLowerInvariant(),
                deputyIds = channel.DeputyIds.Select(d => d.ToString().ToLowerInvariant()).ToList(),
                isPinned = channel.IsPinned,
                pinnedAt = channel.PinnedAtUnix,
                isHidden = channel.IsHidden,
                isGroup = true,
                members = updatedMemberIds,
                lastMessage = "Chưa có tin nhắn mới",
                lastTime = ""
            };

            // 9. Bắn SignalR toàn hệ thống để máy người được thêm tự xuất hiện nhóm (0ms không cần F5)
            await _hub.Clients.All.SendAsync("UserAddedToChannel", new
            {
                channel = channelPayload,
                userId = memberGuid.ToString().ToLowerInvariant()
            });

            // Bắn vào phòng nhóm để những người đang mở nhóm cập nhật số lượng thành viên
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
        catch (DbUpdateException dbEx)
        {
            var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
            Console.WriteLine($"[AddMember DbUpdateException] {innerMsg}");
            return StatusCode(500, new { message = $"Lỗi cơ sở dữ liệu khi thêm thành viên: {innerMsg}" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AddMember Error] {ex.Message}");
            return StatusCode(500, new { message = $"Lỗi máy chủ nội bộ: {ex.Message}" });
        }
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(string id, string userId, [FromQuery] string requesterId)
    {
        if (!Guid.TryParse(id, out var channelGuid)) return BadRequest(new { message = "Mã nhóm không hợp lệ." });

        var channel = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == channelGuid);
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