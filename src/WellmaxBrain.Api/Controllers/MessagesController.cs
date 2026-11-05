using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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
    public async Task<IActionResult> GetChannelMessages(Guid channelId)
    {
        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.CreatedAtUnix)
            .Take(300)
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, [FromQuery] string? geminiKey, [FromQuery] string model = "gemini-2.5-flash")
    {
        if (!Guid.TryParse(dto.ChannelId, out var channelGuid))
        {
            return BadRequest(new { message = "Mã ChannelId không hợp lệ." });
        }

        Guid senderGuid;
        if (!Guid.TryParse(dto.SenderId, out senderGuid))
        {
            senderGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        var msg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            ChannelId = channelGuid,
            SenderId = senderGuid,
            SenderName = dto.SenderName ?? "Người dùng",
            SenderAvatar = dto.SenderAvatar,
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

        // Broadcast realtime qua SignalR tới nhóm chat
        await _hub.Clients.Group(channelGuid.ToString().ToLowerInvariant()).SendAsync("ReceiveMessage", msg);

        // Kích hoạt AI Agent bóc tách đơn hàng nếu có tin nhắn chứa số điện thoại
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

                        await _hub.Clients.Group(channelGuid.ToString().ToLowerInvariant()).SendAsync("OrderExtracted", order);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Agent Error] {ex.Message}");
                }
            });
        }

        return Ok(msg);
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

        // Broadcast cập nhật cảm xúc
        await _hub.Clients.Group(msg.ChannelId.ToString().ToLowerInvariant()).SendAsync("ReactionUpdated", new
        {
            messageId = msg.Id.ToString(),
            channelId = msg.ChannelId.ToString(),
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
            return BadRequest(new { message = "Tin nhắn đã gửi quá 24 giờ, không thể thu hồi theo quy định bảo mật." });
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