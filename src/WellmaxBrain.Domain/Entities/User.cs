namespace WellmaxBrain.Domain.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Tên đầy đủ
    /// </summary>
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty; // SĐT dùng để đăng nhập
    public string PasswordHash { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string Department { get; set; } = "Kinh Doanh & Sale";
    public string RoleGroup { get; set; } = "Nhân viên kinh doanh";
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "Nam";
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
