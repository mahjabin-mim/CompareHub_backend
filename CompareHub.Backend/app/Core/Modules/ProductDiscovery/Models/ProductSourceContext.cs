using CompareHub.Backend.app.Core.Domain.Entities;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;

public class ProductSourceContext
{
    public Guid SourceId { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string SearchUrl { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public UserProductSource Source { get; init; } = null!;
}
