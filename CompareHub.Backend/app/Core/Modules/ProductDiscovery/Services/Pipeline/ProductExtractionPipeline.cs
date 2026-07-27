using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Pipeline;

public class ProductExtractionPipeline : IProductExtractionPipeline
{
    private readonly IReadOnlyList<IProductExtractionStrategy> _strategies;
    private readonly ILogger<ProductExtractionPipeline> _logger;

    public ProductExtractionPipeline(IEnumerable<IProductExtractionStrategy> strategies, ILogger<ProductExtractionPipeline> logger)
    {
        _strategies = strategies.OrderBy(x => x.Order).ToList();
        _logger = logger;
    }

    public async Task<List<ProductResultDto>> ExtractAsync(ProductSourceContext context, CancellationToken cancellationToken)
    {
        foreach (var strategy in _strategies)
        {
            var result = await strategy.ExtractAsync(context, cancellationToken);
            var normalized = ProductExtractionNormalizer.Normalize(result);
            if (normalized.Count > 0)
            {
                _logger.LogInformation("Source {SourceName} resolved by {Strategy} with {Count} products.",
                    context.SourceName,
                    strategy.Name,
                    normalized.Count);
                return normalized;
            }
        }

        return [];
    }
}
