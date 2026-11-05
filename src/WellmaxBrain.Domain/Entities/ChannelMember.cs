namespace WellmaxBrain.Domain.Entities;

public class ChannelMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string RoleInChannel { get; set; } = "Member"; // Owner, Deputy, Member
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}