namespace WellmaxBrain.Domain.Entities;

public class Channel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public string Name { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public List<Guid> DeputyIds { get; set; } = new();
    public bool IsPinned { get; set; } = false;
    public long? PinnedAtUnix { get; set; }
    public List<Guid> PinnedMessageIds { get; set; } = new();
    public bool IsHidden { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChannelMember> Members { get; set; } = new List<ChannelMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}