using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.API.Hubs;
using WellmaxBrain.Application.Interfaces;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IAIExtractorService _aiService;

    public MessagesController(AppDbContext db, IHubContext<ChatHub> hub, IAIExtractorService aiService)
    {
        _db = db;
        _hub = hub;
        _aiService = aiService;
    }

    [HttpGet("channel/{channelId}")]
    public async Task<IActionResult> GetChannelMessages(string channelId, [FromQuery] string? requesterId = null)
    {
        if (string.IsNullOrWhiteSpace(channelId)) return BadRequest();

        Guid channelGuid;
        if (!Guid.TryParse(channelId, out channelGuid))
        {
            return BadRequest(new { message = "Channel ID không hợp lệ." });
        }

        var channel = await _db.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == channelGuid);

        if (channel == null) return NotFound(new { message = "Nhóm không tồn tại." });

        // KIỂM TRA QUYỀN TRUY CẬP: Chỉ những ai là thành viên (hoặc Super Admin) mới được xem tin nhắn
        if (!string.IsNullOrWhiteSpace(requesterId) && Guid.TryParse(requesterId, out var reqGuid))
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == reqGuid);
            bool isSuperAdmin = user?.IsAdmin == true;
            bool isMember = channel.OwnerId == reqGuid || 
                            channel.DeputyIds.Contains(reqGuid) || 
                            channel.Members.Any(m => m.UserId == reqGuid);

            if (!isSuperAdmin && !isMember)
            {
                return StatusCode(403, new { message = "Bạn không phải thành viên của nhóm này." });
            }
        }

        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelGuid)
            .OrderBy(m => m.CreatedAtUnix)
            .Take(300)
            .Select(m => new
            {
                id = m.Id.ToString(),
                channelId = m.ChannelId.ToString().ToLowerInvariant(),
                senderId = m.SenderId.ToString(),
                senderName = m.SenderName,
                senderAvatar = m.SenderAvatar,
                isBot = m.IsBot,
                isSystem = m.IsSystem,
                text = m.Text,
                imageUrl = m.ImageUrl,
                fileUrl = m.FileUrl,
                fileName = m.FileName,
                fileSize = m.FileSize,
                reminderJson = m.ReminderJson,
                createdAtUnix = m.CreatedAtUnix,
                reactions = m.Reactions,
                isDeleted = m.IsDeleted
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, [FromQuery] string? geminiKey, [FromQuery] string model = "gemini-2.5-flash")
    {
        if (!Guid.TryParse(dto.ChannelId, out var targetChannelGuid))
        {
            return BadRequest(new { message = "ChannelId không hợp lệ." });
        }

        Guid targetSenderGuid;
        if (!Guid.TryParse(dto.SenderId, out targetSenderGuid))
        {
            targetSenderGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        var channel = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == targetChannelGuid);
        if (channel == null) return NotFound(new { message = "Nhóm không tồn tại." });

        // Kiểm tra xem người gửi có thuộc nhóm không
        bool isMember = channel.OwnerId == targetSenderGuid || 
                        channel.DeputyIds.Contains(targetSenderGuid) || 
                        channel.Members.Any(m => m.UserId == targetSenderGuid);

        var senderUser = await _db.Users.FindAsync(targetSenderGuid);
        if (senderUser?.IsAdmin != true && !isMember)
        {
            return StatusCode(403, new { message = "Bạn không có quyền gửi tin nhắn vào nhóm này." });
        }

        var msg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            ChannelId = targetChannelGuid,
            SenderId = targetSenderGuid,
            SenderName = dto.SenderName ?? senderUser?.FullName ?? "Người dùng",
            SenderAvatar = dto.SenderAvatar ?? senderUser?.Avatar,
            IsBot = dto.IsBot,
            IsSystem = dto.IsSystem,
            Text = dto.Text ?? "",
            ImageUrl = dto.ImageUrl,
            FileUrl = dto.FileUrl,
            FileName = dto.FileName,
            FileSize = dto.FileSize,
            ReminderJson = dto.ReminderJson,
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsDeleted = false
        };

        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var broadcastPayload = new
        {
            id = msg.Id.ToString(),
            channelId = msg.ChannelId.ToString().ToLowerInvariant(),
            senderId = msg.SenderId.ToString(),
            senderName = msg.SenderName,
            senderAvatar = msg.SenderAvatar,
            isBot = msg.IsBot,
            isSystem = msg.IsSystem,
            text = msg.Text,
            imageUrl = msg.ImageUrl,
            fileUrl = msg.FileUrl,
            fileName = msg.FileName,
            fileSize = msg.FileSize,
            reminderJson = msg.ReminderJson,
            createdAtUnix = msg.CreatedAtUnix,
            reactions = msg.Reactions,
            isDeleted = msg.IsDeleted
        };

        // Gửi SignalR trực tiếp tới phòng nhóm chat
        var cleanGroup = targetChannelGuid.ToString().ToLowerInvariant();
        await _hub.Clients.Group(cleanGroup).SendAsync("ReceiveMessage", broadcastPayload);

        // Kích hoạt AI Agent nếu có tin nhắn chứa số điện thoại
        if (!string.IsNullOrEmpty(geminiKey) && !msg.IsBot && !msg.IsSystem)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var extracted = await _aiService.ExtractOrderFromTextAsync(msg.Text, msg.TenantId, geminiKey, model);
                    if (extracted != null && !string.IsNullOrEmpty(extracted.PhoneNumber))
                    {
                        var order = new Order
                        {
                            TenantId = msg.TenantId,
                            ChannelId = msg.ChannelId,
                            SourceMessageId = msg.Id,
                            OrderCode = $"WM-ORD-{Random.Shared.Next(1000, 9999)}",
                            CustomerName = extracted.CustomerName,
                            PhoneNumber = extracted.PhoneNumber,
                            ShippingAddress = extracted.Address,
                            TotalAmount = extracted.TotalAmount,
                            Status = OrderStatus.Ready,
                            AutoSyncDeadline = DateTime.UtcNow.AddMinutes(20),
                            Items = extracted.Items.Select(i => new OrderItem
                            {
                                Sku = i.Sku,
                                ProductName = i.Name,
                                Quantity = i.Quantity,
                                UnitPrice = i.Price,
                                IsGift = i.IsGift
                            }).ToList()
                        };

                        _db.Orders.Add(order);
                        await _db.SaveChangesAsync();

                        await _hub.Clients.Group(cleanGroup).SendAsync("OrderExtracted", order);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Agent Error] {ex.Message}");
                }
            });
        }

        return Ok(broadcastPayload);
    }

    [HttpPost("{id}/reaction")]
    public async Task<IActionResult> ToggleReaction(Guid id, [FromBody] ReactionDto dto)
    {
        var msg = await _db.ChatMessages.FindAsync(id);
        if (msg == null) return NotFound();

        if (msg.Reactions.ContainsKey(dto.ReactionType))
        {
            msg.Reactions[dto.ReactionType] += 1;
        }
        else
        {
            msg.Reactions[dto.ReactionType] = 1;
        }

        await _db.SaveChangesAsync();

        await _hub.Clients.Group(msg.ChannelId.ToString().ToLowerInvariant()).SendAsync("ReactionUpdated", new
        {
            messageId = msg.Id.ToString(),
            channelId = msg.ChannelId.ToString().ToLowerInvariant(),
            reactions = msg.Reactions
        });

        return Ok(new { messageId = msg.Id, reactions = msg.Reactions });
    }

    [HttpDelete("{id}/revoke")]
    public async Task<IActionResult> RevokeMessage(Guid id, [FromQuery] string senderId)
    {
        var msg = await _db.ChatMessages.FindAsync(id);
        if (msg == null) return NotFound(new { message = "Không tìm thấy tin nhắn." });

        if (Guid.TryParse(senderId, out var sGuid) && msg.SenderId != sGuid)
        {
            return Forbid("Bạn chỉ có quyền thu hồi tin nhắn của chính mình.");
        }

        var elapsedHours = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - msg.CreatedAtUnix) / (1000.0 * 60 * 60);
        if (elapsedHours > 24)
        {
            return BadRequest(new { message = "Tin nhắn đã gửi quá 24 giờ, không thể thu hồi." });
        }

        msg.IsDeleted = true;
        msg.Text = "Tin nhắn đã được thu hồi";
        msg.ImageUrl = null;
        msg.FileUrl = null;
        msg.ReminderJson = null;
        msg.Reactions.Clear();

        await _db.SaveChangesAsync();

        await _hub.Clients.Group(msg.ChannelId.ToString().ToLowerInvariant()).SendAsync("MessageRevoked", msg.Id.ToString());

        return Ok(new { message = "Đã thu hồi tin nhắn thành công.", messageId = msg.Id });
    }
}

public record SendMessageDto(
    string? ChannelId,
    string? SenderId,
    string? SenderName,
    string? SenderAvatar,
    string? Text,
    string? ImageUrl,
    string? FileUrl,
    string? FileName,
    string? FileSize,
    string? ReminderJson,
    bool IsBot,
    bool IsSystem
);

public record ReactionDto(string ReactionType, string? UserId);