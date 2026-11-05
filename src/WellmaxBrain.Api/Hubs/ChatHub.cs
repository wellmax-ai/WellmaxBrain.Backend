using Microsoft.AspNetCore.SignalR;
using WellmaxBrain.Api.Services;

namespace WellmaxBrain.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IOrderAiService _orderAiService;

    public ChatHub(IOrderAiService orderAiService)
    {
        _orderAiService = orderAiService;
    }

    public async Task JoinChannel(string channelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channelId);
    }

    public async Task SendMessage(string channelId, string senderId, string senderName, string text)
    {
        var msgId = $"msg-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var timeStr = DateTime.Now.ToString("HH:mm");

        // 1. Gửi tin nhắn tức thì cho tất cả client trong nhóm
        await Clients.Group(channelId).SendAsync("ReceiveMessage", new
        {
            Id = msgId,
            SenderId = senderId,
            SenderName = senderName,
            Text = text,
            Timestamp = timeStr,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        // 2. Kích hoạt xử lý AI nếu có @AI hoặc độ dài thông tin đơn hàng
        if (text.Contains("@Wellmax") || text.Contains("@AI") || text.Contains("@bot") || text.Length > 20)
        {
            await _orderAiService.ProcessChannelMessageAsync(channelId, senderId, senderName, text);
        }
    }
}
