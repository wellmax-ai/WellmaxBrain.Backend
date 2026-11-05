namespace WellmaxBrain.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int StockQty { get; set; }
    public string Unit { get; set; } = "Hộp";
    public bool IsActive { get; set; } = true;
}