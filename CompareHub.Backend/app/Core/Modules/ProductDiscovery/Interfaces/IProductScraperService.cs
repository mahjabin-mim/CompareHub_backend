using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

public interface IProductScraperService
{
    Task<List<ProductResultDto>> SearchProductsAsync(string sourceUrl, string query, CancellationToken cancellationToken);
}
