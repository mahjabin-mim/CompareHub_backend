using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services;

public class ProductSourceConnector : IProductSourceConnector
{
    private readonly IProductExtractionPipeline _pipeline;

    public ProductSourceConnector(IProductExtractionPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task<List<ProductResultDto>> SearchAsync(UserProductSource source, string query, CancellationToken cancellationToken)
    {
        var searchUrl = BuildSearchUrl(source, query);
        var context = new ProductSourceContext
        {
            SourceId = source.Id,
            SourceName = source.SourceName,
            SourceUrl = source.BaseUrl,
            BaseUrl = source.BaseUrl,
            SearchUrl = searchUrl,
            Query = query,
            Source = source
        };

        var products = await _pipeline.ExtractAsync(context, cancellationToken);
        return ProductExtractionNormalizer.Normalize(products);
    }

    private static string BuildSearchUrl(UserProductSource source, string query)
    {
        var baseUrl = source.BaseUrl.TrimEnd('/');
        var encodedQuery = Uri.EscapeDataString(query);

        if (source.SourceType.Equals("WEBSITE", StringComparison.OrdinalIgnoreCase))
        {
            // For website links, prefer ecommerce-friendly query patterns.
            return $"{baseUrl}/?s={encodedQuery}&post_type=product";
        }

        var endpoint = string.IsNullOrWhiteSpace(source.SearchEndpoint) ? string.Empty : source.SearchEndpoint.Trim();
        var queryParam = string.IsNullOrWhiteSpace(source.QueryParamName) ? "q" : source.QueryParamName.Trim();

        if (endpoint.Contains("{query}", StringComparison.OrdinalIgnoreCase))
        {
            return (baseUrl + "/" + endpoint.TrimStart('/'))
                .Replace("{query}", encodedQuery, StringComparison.OrdinalIgnoreCase);
        }

        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var separator = endpoint.Contains('?') ? "&" : "?";
            return $"{endpoint}{separator}{Uri.EscapeDataString(queryParam)}={encodedQuery}";
        }

        var uri = string.IsNullOrWhiteSpace(endpoint)
            ? baseUrl
            : $"{baseUrl}/{endpoint.TrimStart('/')}";

        var join = uri.Contains('?') ? "&" : "?";
        return $"{uri}{join}{Uri.EscapeDataString(queryParam)}={encodedQuery}";
    }
}
