using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Strategies;

public class HtmlSelectorExtractionStrategy : IProductExtractionStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HtmlSelectorExtractionStrategy> _logger;

    public string Name => nameof(HtmlSelectorExtractionStrategy);
    public int Order => 3;

    public HtmlSelectorExtractionStrategy(
        IHttpClientFactory httpClientFactory,
        ILogger<HtmlSelectorExtractionStrategy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<ProductResultDto>> ExtractAsync(ProductSourceContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(context.Html))
            {
                context.Html = await FetchHtmlAsync(context.SearchUrl, cancellationToken);
            }

            return HtmlProductExtractor.Extract(context.Html, context.BaseUrl, context.SourceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "{Strategy} failed for source {SourceName} ({SourceUrl}).",
                Name,
                context.SourceName,
                context.SourceUrl);
            return [];
        }
    }

    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("ProductExtractionClient");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 CompareHub/1.0");
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
