using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services;

public class MockProductScraperService : IProductScraperService
{
    public Task<List<ProductResultDto>> SearchProductsAsync(string sourceUrl, string query, CancellationToken cancellationToken)
    {
        // Replace this mock with a real scraper adapter (Playwright, HtmlAgilityPack, Selenium, or external APIs).
        // Keeping this interface stable allows swapping implementations without touching ProductDiscoveryService.
        var host = new Uri(sourceUrl).Host;
        var random = new Random(host.GetHashCode() + query.GetHashCode());

        var results = Enumerable.Range(1, 4)
            .Select(index => new ProductResultDto(
                ProductName: $"{query} Model {index} ({host})",
                Price: Math.Round((decimal)(random.NextDouble() * 900 + 100), 2),
                ProductUrl: $"{sourceUrl.TrimEnd('/')}/products/{query.ToLowerInvariant()}-{index}",
                ImageUrl: $"https://placehold.co/320x320?text={Uri.EscapeDataString(query)}+{index}",
                SourceWebsite: host))
            .ToList();

        return Task.FromResult(results);
    }
}
