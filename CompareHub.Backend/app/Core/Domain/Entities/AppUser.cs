namespace CompareHub.Backend.app.Core.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserSourceLink> SourceLinks { get; set; } = new List<UserSourceLink>();
    public ICollection<UserProductSource> ProductSources { get; set; } = new List<UserProductSource>();
}
