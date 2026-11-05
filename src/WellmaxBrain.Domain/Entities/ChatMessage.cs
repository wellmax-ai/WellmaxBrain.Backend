namespace WellmaxBrain.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public Guid SenderId { get; set; }
    public User? Sender { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public bool IsBot { get; set; } = false;
    public bool IsSystem { get; set; } = false;
    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public string? ReminderJson { get; set; }
    public Dictionary<string, int> Reactions { get; set; } = new();
    public bool IsDeleted { get; set; } = false;
    public long CreatedAtUnix { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}