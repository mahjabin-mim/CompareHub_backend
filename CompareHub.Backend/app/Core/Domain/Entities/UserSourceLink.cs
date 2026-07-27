namespace CompareHub.Backend.app.Core.Domain.Entities;

public class UserSourceLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string WebsiteName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public AppUser User { get; set; } = null!;
}
