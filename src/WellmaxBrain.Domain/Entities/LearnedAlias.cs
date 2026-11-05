namespace WellmaxBrain.Domain.Entities;

public class LearnedAlias
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "wellmax-core";
    public string RawPhrase { get; set; } = null!; // Cụm từ sale gõ: "nám đêm v2", "kcn"
    public string NormalizedPhrase { get; set; } = null!; // Chuẩn hóa không dấu: "nam dem v2"
    public string MatchedSku { get; set; } = null!; // Mã SKU trong DB: "WM-MELASMA-02"
    public string ConfirmedBy { get; set; } = null!; // ID nhân sự xác nhận
    public int UsageCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
