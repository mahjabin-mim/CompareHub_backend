using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

public interface IProductDiscoveryService
{
    Task<ProductSearchResponseDto> SearchProductsAsync(string query, CancellationToken cancellationToken);
}
