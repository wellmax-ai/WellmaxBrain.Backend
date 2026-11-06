namespace WellmaxBrain.Domain.Entities;

/// <summary>
/// Nhat ky hoat dong cua Brain AI (test ket noi, trich xuat don, tra loi chat, huan luyen...).
/// </summary>
public class AiActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public Guid? ChannelId { get; set; }

    /// <summary>TestConnection | ExtractOrder | ChatReply | Training</summary>
    public string Action { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? RequestSummary { get; set; }
    public string? ResponseSummary { get; set; }
    public bool IsSuccess { get; set; } = true;
    public int LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}