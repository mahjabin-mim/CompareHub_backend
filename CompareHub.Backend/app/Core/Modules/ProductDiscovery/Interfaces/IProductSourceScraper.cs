using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

public interface IProductSourceScraper
{
    string SourceName { get; }
    string BaseUrl { get; }
    bool CanHandle(string sourceUrl);
    Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken);
}
