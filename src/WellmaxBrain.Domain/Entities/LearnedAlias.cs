namespace WellmaxBrain.Domain.Entities;

public class LearnedAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public string RawPhrase { get; set; } = string.Empty;
    public string NormalizedPhrase { get; set; } = string.Empty;
    public string MatchedSku { get; set; } = string.Empty;
    public string MatchedName { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public int UsageCount { get; set; } = 1;
    public string? ConfirmedBy { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}