using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

public interface IProductExtractionStrategy
{
    string Name { get; }
    int Order { get; }

    Task<List<ProductResultDto>> ExtractAsync(ProductSourceContext context, CancellationToken cancellationToken);
}
