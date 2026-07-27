using System.Text.Json;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Scrapers;

public abstract class ProductSourceScraperBase : IProductSourceScraper
{
    public const string HttpClientName = "PublicProductScraper";

    private readonly IHttpClientFactory _httpClientFactory;
    protected readonly ILogger Logger;
    protected readonly Uri BaseUri;

    protected ProductSourceScraperBase(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        string sourceName,
        string baseUrl)
    {
        _httpClientFactory = httpClientFactory;
        Logger = logger;
        SourceName = sourceName;
        BaseUrl = baseUrl;
        BaseUri = new Uri(baseUrl);
    }

    public string SourceName { get; }
    public string BaseUrl { get; }

    public virtual bool CanHandle(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
        {
            return false;
        }

        return NormalizeHost(sourceUri.Host).Equals(NormalizeHost(BaseUri.Host), StringComparison.OrdinalIgnoreCase);
    }

    public virtual async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var searchTasks = new List<Task<List<ProductResultDto>>>
        {
            SearchWooCommerceApiAsync(query, cancellationToken),
            SearchWordPressProductApiAsync(query, cancellationToken),
            SearchShopifySuggestApiAsync(query, cancellationToken)
        };

        searchTasks.AddRange(BuildHtmlSearchUris(query).Select(uri => SearchHtmlAsync(uri, cancellationToken)));

        var resultGroups = await Task.WhenAll(searchTasks);
        var products = resultGroups.SelectMany(x => x);
        return ProductScraperHelpers.NormalizeResults(products, query, 30);
    }

    protected virtual IEnumerable<Uri> BuildHtmlSearchUris(string query)
    {
        var encoded = Uri.EscapeDataString(query);
        var origin = $"{BaseUri.Scheme}://{BaseUri.Host}";
        var searchUrls = new[]
        {
            $"{origin}/?s={encoded}&post_type=product",
            $"{origin}/?s={encoded}",
            $"{origin}/search?q={encoded}",
            $"{origin}/search?type=product&q={encoded}",
            $"{origin}/shop?search={encoded}",
            $"{origin}/shop?q={encoded}",
            $"{origin}/collections/all?q={encoded}"
        };

        foreach (var url in searchUrls.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                yield return uri;
            }
        }
    }

    protected async Task<string> FetchStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36 CompareHub/1.0");
            request.Headers.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.8");

            using var response = await CreateClient().SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogInformation("Timed out while fetching {Uri}.", uri);
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to fetch {Uri}.", uri);
            return string.Empty;
        }
    }

    protected async Task<List<ProductResultDto>> SearchWooCommerceApiAsync(string query, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(query);
        var origin = $"{BaseUri.Scheme}://{BaseUri.Host}";
        var apiUri = new Uri($"{origin}/wp-json/wc/store/v1/products?search={encoded}&per_page=24");
        var raw = await FetchStringAsync(apiUri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('['))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromWooStoreApi(document.RootElement, BaseUri, SourceName),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse WooCommerce API response for {SourceName}.", SourceName);
            return [];
        }
    }

    protected async Task<List<ProductResultDto>> SearchWordPressProductApiAsync(string query, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(query);
        var origin = $"{BaseUri.Scheme}://{BaseUri.Host}";
        var apiUri = new Uri($"{origin}/wp-json/wp/v2/product?search={encoded}&per_page=12&_fields=id");
        var raw = await FetchStringAsync(apiUri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('['))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var productIds = document.RootElement
                .EnumerateArray()
                .Select(item => ProductScraperHelpers.GetString(item, "id"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            var productTasks = productIds.Select(id => SearchWooCommerceProductByIdAsync(id, query, cancellationToken));
            return ProductScraperHelpers.NormalizeResults(
                (await Task.WhenAll(productTasks)).SelectMany(x => x),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse WordPress product search response for {SourceName}.", SourceName);
            return [];
        }
    }

    private async Task<List<ProductResultDto>> SearchWooCommerceProductByIdAsync(string productId, string query, CancellationToken cancellationToken)
    {
        var origin = $"{BaseUri.Scheme}://{BaseUri.Host}";
        var apiUri = new Uri($"{origin}/wp-json/wc/store/v1/products/{Uri.EscapeDataString(productId)}");
        var raw = await FetchStringAsync(apiUri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromWooStoreApi(document.RootElement, BaseUri, SourceName),
                query,
                1);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse WooCommerce product detail response for {SourceName}.", SourceName);
            return [];
        }
    }

    private async Task<List<ProductResultDto>> SearchHtmlAsync(Uri searchUri, CancellationToken cancellationToken)
    {
        var html = await FetchStringAsync(searchUri, cancellationToken);
        return string.IsNullOrWhiteSpace(html)
            ? []
            : ProductScraperHelpers.ExtractFromHtml(html, searchUri, SourceName);
    }

    protected async Task<List<ProductResultDto>> SearchShopifySuggestApiAsync(string query, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(query);
        var origin = $"{BaseUri.Scheme}://{BaseUri.Host}";
        var apiUri = new Uri($"{origin}/search/suggest.json?q={encoded}&resources[type]=product&resources[limit]=24");
        var raw = await FetchStringAsync(apiUri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromShopifySuggest(document.RootElement, BaseUri, SourceName),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Shopify suggest response for {SourceName}.", SourceName);
            return [];
        }
    }

    protected HttpClient CreateClient() => _httpClientFactory.CreateClient(HttpClientName);

    private static string NormalizeHost(string host)
    {
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
