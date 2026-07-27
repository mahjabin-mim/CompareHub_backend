using System.Text.Json;
using System.Text.RegularExpressions;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Strategies;

public class JsonLdExtractionStrategy : IProductExtractionStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JsonLdExtractionStrategy> _logger;

    public string Name => nameof(JsonLdExtractionStrategy);
    public int Order => 2;

    public JsonLdExtractionStrategy(IHttpClientFactory httpClientFactory, ILogger<JsonLdExtractionStrategy> logger)
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

            if (string.IsNullOrWhiteSpace(context.Html))
            {
                return [];
            }

            var pageUri = new Uri(context.SearchUrl);
            var products = new List<ProductResultDto>();

            var scripts = Regex.Matches(
                context.Html,
                "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match script in scripts)
            {
                var json = System.Net.WebUtility.HtmlDecode(script.Groups[1].Value.Trim());
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(json);
                    products.AddRange(ExtractProducts(document.RootElement, context.SourceName, context.BaseUrl, pageUri));
                }
                catch
                {
                    // Continue with remaining JSON-LD blocks.
                }
            }

            return ProductExtractionNormalizer.Normalize(products);
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

    private static IEnumerable<ProductResultDto> ExtractProducts(JsonElement element, string sourceName, string baseUrl, Uri pageUri)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                foreach (var product in ExtractProducts(child, sourceName, baseUrl, pageUri))
                {
                    yield return product;
                }
            }
            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var type = GetString(element, "@type");
        if (type.Contains("Product", StringComparison.OrdinalIgnoreCase))
        {
            var name = GetString(element, "name");
            var url = FirstNonEmpty(GetString(element, "url"), pageUri.ToString());
            var image = ExtractImage(element);
            var price = ExtractPrice(element);

            yield return new ProductResultDto(
                ProductExtractionNormalizer.CleanText(name),
                price,
                ProductExtractionNormalizer.MakeAbsoluteUrl(baseUrl, url),
                ProductExtractionNormalizer.MakeAbsoluteUrl(baseUrl, image),
                sourceName);
        }

        if (type.Contains("ItemList", StringComparison.OrdinalIgnoreCase) &&
            TryGetPropertyIgnoreCase(element, "itemListElement", out var itemList) &&
            itemList.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemList.EnumerateArray())
            {
                if (TryGetPropertyIgnoreCase(item, "item", out var nestedItem))
                {
                    foreach (var product in ExtractProducts(nestedItem, sourceName, baseUrl, pageUri))
                    {
                        yield return product;
                    }
                }
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var product in ExtractProducts(property.Value, sourceName, baseUrl, pageUri))
            {
                yield return product;
            }
        }
    }

    private static decimal ExtractPrice(JsonElement element)
    {
        if (TryGetPropertyIgnoreCase(element, "offers", out var offers))
        {
            if (offers.ValueKind == JsonValueKind.Array)
            {
                foreach (var offer in offers.EnumerateArray())
                {
                    var p = ExtractPriceValue(offer);
                    if (p > 0) return p;
                }
            }

            return ExtractPriceValue(offers);
        }

        return ExtractPriceValue(element);
    }

    private static decimal ExtractPriceValue(JsonElement element)
    {
        foreach (var key in new[] { "price", "lowPrice", "highPrice" })
        {
            if (TryGetPropertyIgnoreCase(element, key, out var value))
            {
                var text = value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.GetString();
                var price = ProductExtractionNormalizer.ParsePrice(text);
                if (price > 0)
                {
                    return price;
                }
            }
        }

        return 0;
    }

    private static string ExtractImage(JsonElement element)
    {
        if (!TryGetPropertyIgnoreCase(element, "image", out var image))
        {
            return string.Empty;
        }

        return image.ValueKind switch
        {
            JsonValueKind.String => image.GetString() ?? string.Empty,
            JsonValueKind.Array => image.EnumerateArray().FirstOrDefault().GetString() ?? string.Empty,
            JsonValueKind.Object => GetString(image, "url"),
            _ => string.Empty
        };
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return TryGetPropertyIgnoreCase(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in element.EnumerateObject())
            {
                if (item.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = item.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    }
}
