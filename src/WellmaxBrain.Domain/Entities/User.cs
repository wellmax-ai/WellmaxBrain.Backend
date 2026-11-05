namespace WellmaxBrain.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "wellmax-default";
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    
    public string? BirthDate { get; set; }
    public string? DateOfBirth 
    { 
        get => BirthDate; 
        set => BirthDate = value; 
    }

    public string? Gender { get; set; }
    public string Department { get; set; } = "Kinh Doanh & Sale";
    public string RoleGroup { get; set; } = "Nhân viên kinh doanh (Sale Rep)";
    public string? Avatar { get; set; }
    public string? Note { get; set; }
    public string? Bio { get; set; }
    public bool IsAdmin { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChannelMember> ChannelMemberships { get; set; } = new List<ChannelMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}