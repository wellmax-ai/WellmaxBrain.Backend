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
    public async Task<IActionResult> GetChannelMessages(Guid channelId)
    {
        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.CreatedAtUnix)
            .Take(300)
            .Select(m => new
            {
                id = m.Id.ToString(),
                channelId = m.ChannelId.ToString(),
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
                isDeleted = m.IsDeleted
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, [FromQuery] string? geminiKey, [FromQuery] string model = "gemini-2.5-flash")
    {
        if (!Guid.TryParse(dto.ChannelId, out var channelGuid))
        {
            return BadRequest(new { message = "Mã ChannelId không hợp lệ (cần là Guid từ database)." });
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

        var broadcastPayload = new
        {
            id = msg.Id.ToString(),
            channelId = msg.ChannelId.ToString(),
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
            isDeleted = msg.IsDeleted
        };

        // 1. Phát realtime qua SignalR tới đúng Group channelId từ Database
        await _hub.Clients.Group(channelGuid.ToString()).SendAsync("ReceiveMessage", broadcastPayload);

        // 2. Kích hoạt AI Agent bóc tách đơn hàng nếu có tin nhắn chứa số điện thoại
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

                        await _hub.Clients.Group(channelGuid.ToString()).SendAsync("OrderExtracted", order);
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