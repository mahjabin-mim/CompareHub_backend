namespace CompareHub.Backend.app.Core.Modules.SourceLinks.DTOs;

public record CreateSourceLinkRequestDto(string Url, string WebsiteName);
public record SourceLinkDto(Guid Id, string Url, string WebsiteName, bool IsActive, DateTime CreatedAt);
