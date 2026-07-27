namespace CompareHub.Backend.app.Core.Domain.Entities;

public class UserProductSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceType { get; set; } = "API";
    public string BaseUrl { get; set; } = string.Empty;
    public string SearchEndpoint { get; set; } = string.Empty;
    public string QueryParamName { get; set; } = "q";
    public string HttpMethod { get; set; } = "GET";
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string HeadersJson { get; set; } = "{}";
    public string NamePath { get; set; } = string.Empty;
    public string PricePath { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string ProductUrlPath { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
}
