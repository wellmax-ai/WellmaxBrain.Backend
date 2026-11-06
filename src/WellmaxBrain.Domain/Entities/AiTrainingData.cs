namespace WellmaxBrain.Domain.Entities;

public enum AiTrainingSource
{
    /// <summary>File huan luyen / tai lieu tri thuc duoc tai len hoac nhap tay</summary>
    KnowledgeFile = 0,
    /// <summary>Huan luyen truc tiep ngay trong noi dung chat</summary>
    ChatMessage = 1,
    /// <summary>Quy tac nghiep vu thu cong</summary>
    ManualRule = 2
}

/// <summary>
/// Du lieu huan luyen dong cua Brain AI (luu trong Database).
/// </summary>
public class AiTrainingData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public AiTrainingSource SourceType { get; set; } = AiTrainingSource.KnowledgeFile;

    /// <summary>Ten file huan luyen hoac Id tin nhan nguon</summary>
    public string? SourceRef { get; set; }
    public Guid? ChannelId { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}