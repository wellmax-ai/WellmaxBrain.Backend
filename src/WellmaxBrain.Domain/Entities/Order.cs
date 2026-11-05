namespace WellmaxBrain.Domain.Entities;

public enum OrderStatus
{
    Ready,
    Confirmed,
    SyncedPOS
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public string OrderCode { get; set; } = string.Empty;
    public Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public Guid SourceMessageId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; } = 0;
    public OrderStatus Status { get; set; } = OrderStatus.Ready;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AutoSyncDeadline { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}