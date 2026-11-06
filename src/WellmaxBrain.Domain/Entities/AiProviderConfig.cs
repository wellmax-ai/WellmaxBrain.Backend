namespace WellmaxBrain.Domain.Entities;

/// <summary>
/// Cau hinh LLM Provider (Gemini / DeepSeek) luu dong trong Database.
/// </summary>
public class AiProviderConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";

    /// <summary>gemini | deepseek</summary>
    public string Provider { get; set; } = "gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gemini-2.5-flash";
    public string? BaseUrl { get; set; }
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;
    public double Temperature { get; set; } = 0.4;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }

    public const string DefaultSystemPrompt =
        "Ban la Wellmax Brain AI - Tro ly AI Agent thong minh duoc nhung trong moi nhom chat noi bo cua Wellmax. " +
        "Nhiem vu: phan tich noi dung hoi thoai, trich xuat don hang (ten khach, so dien thoai, dia chi, san pham, gia tien), " +
        "tu van nghiep vu suc khoe va sac dep, ho tro nhan vien kinh doanh. Tra loi ngan gon, chinh xac, bang tieng Viet.";
}