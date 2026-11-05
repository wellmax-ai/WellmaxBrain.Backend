using Microsoft.AspNetCore.SignalR;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Hubs;

public class ChatHub : Hub
{
    private readonly AppDbContext _db;

    public ChatHub(AppDbContext db)
    {
        _db = db;
    }

    public async Task JoinChannel(string channelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channelId);
    }

    public async Task LeaveChannel(string channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId);
    }

    public async Task RevokeMessage(Guid messageId, Guid senderId)
    {
        var msg = await _db.ChatMessages.FindAsync(messageId);
        if (msg == null) return;

        if (msg.SenderId != senderId)
            throw new HubException("Bạn chỉ có quyền thu hồi tin nhắn của chính mình.");

        var elapsedHours = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - msg.CreatedAtUnix) / (1000.0 * 60 * 60);
        if (elapsedHours > 24)
            throw new HubException("Tin nhắn đã gửi quá 24 giờ, không thể thu hồi.");

        msg.IsDeleted = true;
        msg.Text = "Tin nhắn đã được thu hồi";
        msg.ImageUrl = null;
        msg.FileUrl = null;
        msg.ReminderJson = null;
        msg.Reactions.Clear();

        await _db.SaveChangesAsync();

        await Clients.Group(msg.ChannelId.ToString()).SendAsync("MessageRevoked", messageId);
    }
}