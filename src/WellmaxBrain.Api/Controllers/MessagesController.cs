using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessage msg, [FromQuery] string? geminiKey, [FromQuery] string model = "gemini-2.5-flash")
    {
        msg.CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        // Broadcast realtime qua SignalR
        await _hub.Clients.Group(msg.ChannelId.ToString()).SendAsync("ReceiveMessage", msg);

        // Kích hoạt AI Agent bóc tách đơn hàng nếu có tin nhắn chứa thông tin liên hệ
        if (!string.IsNullOrEmpty(geminiKey) && !msg.IsBot && !msg.IsSystem)
        {
            _ = Task.Run(async () =>
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

                    // Bắn realtime OrderCard về Sidebar phải của Client
                    await _hub.Clients.Group(msg.ChannelId.ToString()).SendAsync("OrderExtracted", order);
                }
            });
        }

        return Ok(msg);
    }
}