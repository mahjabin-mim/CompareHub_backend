using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Scrapers;

public class KireiBdProductScraper : ProductSourceScraperBase
{
    public KireiBdProductScraper(IHttpClientFactory httpClientFactory, ILogger<KireiBdProductScraper> logger)
        : base(httpClientFactory, logger, "kireibd.com", "https://kireibd.com/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(query);
        var endpoints = new[]
        {
            $"https://frontendapi.kireibd.com/api/v2/gigalogy/items/search?search={encoded}&page=1&limit=24",
            $"https://frontendapi.kireibd.com/api/v2/gigalogy/items/search?search={encoded}&page=2&limit=24"
        };

        var tasks = endpoints
            .Select(async endpoint =>
            {
                var raw = await FetchStringAsync(new Uri(endpoint), cancellationToken);
                if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
                {
                    return new List<ProductResultDto>();
                }

                try
                {
                    using var document = JsonDocument.Parse(raw);
                    return ProductScraperHelpers.NormalizeResults(
                        ProductScraperHelpers.ExtractFromKireiGigalogySearch(document.RootElement, SourceName),
                        query,
                        24);
                }
                catch (JsonException ex)
                {
                    Logger.LogDebug(ex, "Unable to parse Kirei API response.");
                    return [];
                }
            });

        var resultGroups = await Task.WhenAll(tasks);
        return ProductScraperHelpers.NormalizeResults(
            resultGroups.SelectMany(x => x),
            query,
            24);
    }
}

public class GroomlyBdProductScraper : ProductSourceScraperBase
{
    public GroomlyBdProductScraper(IHttpClientFactory httpClientFactory, ILogger<GroomlyBdProductScraper> logger)
        : base(httpClientFactory, logger, "groomlybd.com", "https://groomlybd.com/")
    {
    }
}

public class SkinCareShopProductScraper : ProductSourceScraperBase
{
    public SkinCareShopProductScraper(IHttpClientFactory httpClientFactory, ILogger<SkinCareShopProductScraper> logger)
        : base(httpClientFactory, logger, "skincareshop.com.bd", "https://skincareshop.com.bd/")
    {
    }
}

public class BeautyBoothProductScraper : ProductSourceScraperBase
{
    private static readonly Uri ApiUri = new("https://cms.beautybooth.com.bd/api/v2/ajax-search");
    private static readonly Uri ImageBaseUri = new("https://cms.beautybooth.com.bd/");

    public BeautyBoothProductScraper(IHttpClientFactory httpClientFactory, ILogger<BeautyBoothProductScraper> logger)
        : base(httpClientFactory, logger, "beautybooth.com.bd", "https://beautybooth.com.bd/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUri)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { search = trimmedQuery }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36 CompareHub/1.0");

            using var response = await CreateClient().SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
            {
                return [];
            }

            using var document = JsonDocument.Parse(raw);
            if (!ProductScraperHelpers.TryGetPropertyIgnoreCase(document.RootElement, "products", out var products) ||
                products.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<ProductResultDto>();
            foreach (var item in products.EnumerateArray())
            {
                var name = ProductScraperHelpers.GetString(item, "name");
                var slug = ProductScraperHelpers.GetString(item, "slug");
                var image = ProductScraperHelpers.GetString(item, "image");
                if (string.IsNullOrWhiteSpace(image))
                {
                    image = ProductScraperHelpers.GetString(item, "thumbnail_image");
                }
                var price = ProductScraperHelpers.ExtractPriceValue(item, "price", "main_price");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
                {
                    continue;
                }

                results.Add(new ProductResultDto(
                    ProductScraperHelpers.CleanText(name),
                    price,
                    $"{BaseUrl}product/{slug}",
                    ProductScraperHelpers.MakeAbsoluteUrl(ImageBaseUri, image),
                    SourceName));
            }

            return ProductScraperHelpers.NormalizeResults(results, trimmedQuery, 24);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogInformation("Timed out while searching Beauty Booth for {Query}.", trimmedQuery);
            return [];
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Beauty Booth ajax-search response.");
            return [];
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to search Beauty Booth.");
            return [];
        }
    }
}

public class EmartwaySkincareProductScraper : ProductSourceScraperBase
{
    private static readonly Uri ApiBaseUri = new("https://api.emartwayskincare.com.bd/");
    private static readonly Uri ImageBaseUri = new("https://d1puc9h291tp0h.cloudfront.net/");

    public EmartwaySkincareProductScraper(IHttpClientFactory httpClientFactory, ILogger<EmartwaySkincareProductScraper> logger)
        : base(httpClientFactory, logger, "emartwayskincare.com.bd", "https://www.emartwayskincare.com.bd/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        var encodedQuery = Uri.EscapeDataString(trimmedQuery);
        var endpoints = Enumerable
            .Range(1, 3)
            .Select(page => new Uri($"{ApiBaseUri}api/v3/products/search?name={encodedQuery}&page={page}"))
            .ToList();

        var resultGroups = await Task.WhenAll(endpoints.Select(endpoint => SearchApiAsync(endpoint, trimmedQuery, cancellationToken)));
        return ProductScraperHelpers.NormalizeResults(
            resultGroups.SelectMany(x => x),
            trimmedQuery,
            30);
    }

    private async Task<List<ProductResultDto>> SearchApiAsync(Uri endpoint, string query, CancellationToken cancellationToken)
    {
        var raw = await FetchStringAsync(endpoint, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!ProductScraperHelpers.TryGetPropertyIgnoreCase(document.RootElement, "data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<ProductResultDto>();
            foreach (var item in data.EnumerateArray())
            {
                var name = ProductScraperHelpers.GetString(item, "name");
                var slug = ProductScraperHelpers.GetString(item, "slug");
                var image = ProductScraperHelpers.GetString(item, "thumbnail_image");
                var price = ProductScraperHelpers.ExtractPriceValue(item, "nonformated_price", "price");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
                {
                    continue;
                }

                results.Add(new ProductResultDto(
                    ProductScraperHelpers.CleanText(name),
                    price,
                    $"{BaseUrl}products/{slug}",
                    ProductScraperHelpers.MakeAbsoluteUrl(ImageBaseUri, image),
                    SourceName));
            }

            return ProductScraperHelpers.NormalizeResults(results, query, 20);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Emartway search API response.");
            return [];
        }
    }
}

public class TekkaProductScraper : ProductSourceScraperBase
{
    public TekkaProductScraper(IHttpClientFactory httpClientFactory, ILogger<TekkaProductScraper> logger)
        : base(httpClientFactory, logger, "tekka.com.bd", "https://tekka.com.bd/")
    {
    }
}

public class DhaliShopProductScraper : ProductSourceScraperBase
{
    private static readonly Uri ApiBaseUri = new("https://admin.dhalishop.com/");
    private static readonly Uri ImageBaseUri = new("https://admin.dhalishop.com/public/");

    public DhaliShopProductScraper(IHttpClientFactory httpClientFactory, ILogger<DhaliShopProductScraper> logger)
        : base(httpClientFactory, logger, "dhalishop.com", "https://dhalishop.com/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        var encodedQuery = Uri.EscapeDataString(trimmedQuery);
        var endpoints = Enumerable
            .Range(1, 3)
            .Select(page => new Uri($"{ApiBaseUri}api/v2/products/search?name={encodedQuery}&page={page}"))
            .ToList();

        var resultGroups = await Task.WhenAll(endpoints.Select(endpoint => SearchApiAsync(endpoint, trimmedQuery, cancellationToken)));
        return ProductScraperHelpers.NormalizeResults(
            resultGroups.SelectMany(x => x),
            trimmedQuery,
            30);
    }

    private async Task<List<ProductResultDto>> SearchApiAsync(Uri endpoint, string query, CancellationToken cancellationToken)
    {
        var raw = await FetchStringAsync(endpoint, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!ProductScraperHelpers.TryGetPropertyIgnoreCase(document.RootElement, "data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<ProductResultDto>();
            foreach (var item in data.EnumerateArray())
            {
                var name = ProductScraperHelpers.GetString(item, "name");
                var slug = ProductScraperHelpers.GetString(item, "slug");
                var image = ProductScraperHelpers.GetString(item, "thumbnail_image");
                var price = ProductScraperHelpers.ExtractPriceValue(item, "main_price", "price");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
                {
                    continue;
                }

                results.Add(new ProductResultDto(
                    ProductScraperHelpers.CleanText(name),
                    price,
                    $"{BaseUrl}product/{slug}",
                    ProductScraperHelpers.MakeAbsoluteUrl(ImageBaseUri, image),
                    SourceName));
            }

            return ProductScraperHelpers.NormalizeResults(results, query, 20);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse DhaliShop search API response.");
            return [];
        }
    }
}

public class TheAlamsProductScraper : ProductSourceScraperBase
{
    public TheAlamsProductScraper(IHttpClientFactory httpClientFactory, ILogger<TheAlamsProductScraper> logger)
        : base(httpClientFactory, logger, "thealamsbd.com", "https://thealamsbd.com/")
    {
    }
}

public class TheLiliumProductScraper : ProductSourceScraperBase
{
    public TheLiliumProductScraper(IHttpClientFactory httpClientFactory, ILogger<TheLiliumProductScraper> logger)
        : base(httpClientFactory, logger, "thelilium.shop", "https://www.thelilium.shop/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var baseResults = await base.SearchAsync(query, cancellationToken);
        if (baseResults.Count == 0)
        {
            return [];
        }

        var enriched = new List<ProductResultDto>(baseResults.Count);
        foreach (var product in baseResults)
        {
            if (!NeedsImageEnrichment(product.ImageUrl))
            {
                enriched.Add(product);
                continue;
            }

            var imageUrl = await TryFetchOgImageAsync(product.ProductUrl, cancellationToken);
            enriched.Add(string.IsNullOrWhiteSpace(imageUrl)
                ? product
                : product with { ImageUrl = imageUrl });
        }

        return ProductScraperHelpers.NormalizeResults(enriched, query, 30);
    }

    private static bool NeedsImageEnrichment(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return true;
        }

        return imageUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) ||
               imageUrl.Contains("svg+xml", StringComparison.OrdinalIgnoreCase) ||
               imageUrl.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
               !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> TryFetchOgImageAsync(string productUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productUrl) ||
            !Uri.TryCreate(productUrl, UriKind.Absolute, out var productUri))
        {
            return string.Empty;
        }

        var html = await FetchStringAsync(productUri, cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var jsonLdImage = TryFetchJsonLdImage(html, productUri);
        if (!string.IsNullOrWhiteSpace(jsonLdImage))
        {
            return jsonLdImage;
        }

        var ogMatch = Regex.Match(
            html,
            "<meta[^>]+property=[\"']og:image[\"'][^>]+content=[\"'](?<url>[^\"']+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (ogMatch.Success)
        {
            return NormalizeImageUrl(productUri, ogMatch.Groups["url"].Value);
        }

        var twitterMatch = Regex.Match(
            html,
            "<meta[^>]+name=[\"']twitter:image[\"'][^>]+content=[\"'](?<url>[^\"']+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return twitterMatch.Success
            ? NormalizeImageUrl(productUri, twitterMatch.Groups["url"].Value)
            : string.Empty;
    }

    private static string TryFetchJsonLdImage(string html, Uri productUri)
    {
        var imageMatch = Regex.Match(
            html,
            "\"(?:thumbnailUrl|contentUrl|image|url)\"\\s*:\\s*\"(?<url>https?:\\\\/\\\\/[^\"\\\\]+|\\\\/[^\"\\\\]+|\\/[^\"\\\\]+|https?:\\/\\/[^\"\\\\]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!imageMatch.Success)
        {
            return string.Empty;
        }

        var rawUrl = Regex.Unescape(imageMatch.Groups["url"].Value).Replace("\\/", "/");
        return NormalizeImageUrl(productUri, rawUrl);
    }

    private static string NormalizeImageUrl(Uri productUri, string imageUrl)
    {
        var absoluteUrl = ProductScraperHelpers.MakeAbsoluteUrl(productUri, imageUrl);
        if (string.IsNullOrWhiteSpace(absoluteUrl) ||
            !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var imageUri))
        {
            return string.Empty;
        }

        if (productUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            imageUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            imageUri.Host.Equals(productUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            var secureBuilder = new UriBuilder(imageUri)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1
            };

            return secureBuilder.Uri.ToString();
        }

        return imageUri.ToString();
    }
}

public class KlassyProductScraper : ProductSourceScraperBase
{
    private static readonly Uri ApiBaseUri = new("https://api.klassy.com.bd/");
    private static readonly Uri ImageBaseUri = new("https://cdn.klassy.com.bd/");

    public KlassyProductScraper(IHttpClientFactory httpClientFactory, ILogger<KlassyProductScraper> logger)
        : base(httpClientFactory, logger, "klassy.com.bd", "https://klassy.com.bd/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        var encodedQuery = Uri.EscapeDataString(trimmedQuery);
        var endpoints = Enumerable
            .Range(1, 3)
            .Select(page => new Uri($"{ApiBaseUri}client/v3/product/search?pages={page}&pageSize=18&name={encodedQuery}"))
            .ToList();

        var resultGroups = await Task.WhenAll(endpoints.Select(endpoint => SearchApiAsync(endpoint, trimmedQuery, cancellationToken)));
        return ProductScraperHelpers.NormalizeResults(
            resultGroups.SelectMany(x => x),
            trimmedQuery,
            24);
    }

    private async Task<List<ProductResultDto>> SearchApiAsync(Uri endpoint, string query, CancellationToken cancellationToken)
    {
        var raw = await FetchStringAsync(endpoint, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!ProductScraperHelpers.TryGetPropertyIgnoreCase(document.RootElement, "products", out var products) ||
                products.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<ProductResultDto>();
            foreach (var item in products.EnumerateArray())
            {
                var name = ProductScraperHelpers.GetString(item, "name");
                var slug = ProductScraperHelpers.GetString(item, "slug");
                var image = ProductScraperHelpers.GetString(item, "thumbnail_img");
                if (string.IsNullOrWhiteSpace(image))
                {
                    image = ProductScraperHelpers.GetString(item, "thumbnail_image");
                }

                var price = ExtractKlassyPrice(item);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
                {
                    continue;
                }

                results.Add(new ProductResultDto(
                    ProductScraperHelpers.CleanText(name),
                    price,
                    $"https://www.klassy.com.bd/products/{slug}",
                    ProductScraperHelpers.MakeAbsoluteUrl(ImageBaseUri, image),
                    SourceName));
            }

            return ProductScraperHelpers.NormalizeResults(results, query, 18);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Klassy search API response.");
            return [];
        }
    }

    private static decimal ExtractKlassyPrice(JsonElement item)
    {
        if (ProductScraperHelpers.TryGetPropertyIgnoreCase(item, "product_summary", out var productSummary) &&
            ProductScraperHelpers.TryGetPropertyIgnoreCase(productSummary, "stocks", out var stocks) &&
            stocks.ValueKind == JsonValueKind.Array)
        {
            foreach (var stock in stocks.EnumerateArray())
            {
                if (!ProductScraperHelpers.TryGetPropertyIgnoreCase(stock, "pricing", out var pricing))
                {
                    continue;
                }

                var discountedPrice = ProductScraperHelpers.ExtractPriceValue(pricing, "price_after_discount");
                if (discountedPrice > 0)
                {
                    return discountedPrice;
                }
            }
        }

        return ProductScraperHelpers.ExtractPriceValue(
            item,
            "sale_price",
            "discount_price",
            "current_price",
            "discounted_price",
            "offer_price",
            "special_price",
            "price");
    }

}

public class PixieLabellaProductScraper : ProductSourceScraperBase
{
    public PixieLabellaProductScraper(IHttpClientFactory httpClientFactory, ILogger<PixieLabellaProductScraper> logger)
        : base(httpClientFactory, logger, "pixielabellabangladesh.com", "https://pixielabellabangladesh.com/")
    {
    }
}

public class SkinnoraProductScraper : ProductSourceScraperBase
{
    public SkinnoraProductScraper(IHttpClientFactory httpClientFactory, ILogger<SkinnoraProductScraper> logger)
        : base(httpClientFactory, logger, "skinnora.com", "https://www.skinnora.com/")
    {
    }
}

public class MakeupChariProductScraper : ProductSourceScraperBase
{
    public MakeupChariProductScraper(IHttpClientFactory httpClientFactory, ILogger<MakeupChariProductScraper> logger)
        : base(httpClientFactory, logger, "makeupchari.com", "https://makeupchari.com/")
    {
    }
}

public class PerfectoBdProductScraper : ProductSourceScraperBase
{
    public PerfectoBdProductScraper(IHttpClientFactory httpClientFactory, ILogger<PerfectoBdProductScraper> logger)
        : base(httpClientFactory, logger, "perfectobd.com", "https://perfectobd.com/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://app.perfectobd.com/api/products-name"));
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36 CompareHub/1.0");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { pagination = 24, search = query }),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await CreateClient().SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromPerfectoSearch(document.RootElement, SourceName),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Perfecto response.");
            return [];
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to fetch Perfecto results.");
            return [];
        }
    }

}

public class AroggaProductScraper : ProductSourceScraperBase
{
    public AroggaProductScraper(IHttpClientFactory httpClientFactory, ILogger<AroggaProductScraper> logger)
        : base(httpClientFactory, logger, "arogga.com", "https://www.arogga.com/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var uri = new Uri($"https://api.arogga.com/general/v3/search?_type=web&_page=1&_perPage=24&_search={Uri.EscapeDataString(query)}");
        var raw = await FetchStringAsync(uri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromAroggaSearch(document.RootElement, SourceName),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Arogga response.");
            return [];
        }
    }
}

public class TheMartBangladeshTumblrProductScraper : ProductSourceScraperBase
{
    public TheMartBangladeshTumblrProductScraper(IHttpClientFactory httpClientFactory, ILogger<TheMartBangladeshTumblrProductScraper> logger)
        : base(httpClientFactory, logger, "themartbangladesh.tumblr.com", "https://themartbangladesh.tumblr.com/")
    {
    }

    protected override IEnumerable<Uri> BuildHtmlSearchUris(string query)
    {
        var encoded = Uri.EscapeDataString(query);
        yield return new Uri($"https://themartbangladesh.tumblr.com/search/{encoded}");
        yield return new Uri($"https://themartbangladesh.tumblr.com/tagged/{encoded}");
    }
}

public class KoreanMartProductScraper : ProductSourceScraperBase
{
    public KoreanMartProductScraper(IHttpClientFactory httpClientFactory, ILogger<KoreanMartProductScraper> logger)
        : base(httpClientFactory, logger, "koreanmartbd.com", "https://koreanmartbd.com/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var origin = $"{BaseUri.Scheme}://{BaseUri.Host}";
        var uri = new Uri($"{origin}/api/v1/catalog/products?filter_text={Uri.EscapeDataString(query)}&per_page=24&page=1");
        var raw = await FetchStringAsync(uri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromKoreanMartSearch(document.RootElement, BaseUri, SourceName),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse KoreanMart response.");
            return [];
        }
    }
}

public class ShajgojProductScraper : ProductSourceScraperBase
{
    public ShajgojProductScraper(IHttpClientFactory httpClientFactory, ILogger<ShajgojProductScraper> logger)
        : base(httpClientFactory, logger, "shop.shajgoj.com", "https://shop.shajgoj.com/")
    {
    }

    public override async Task<List<ProductResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var uri = new Uri($"https://khoj.shajgoj.com/products?s={Uri.EscapeDataString(query)}&facet=true");
        var raw = await FetchStringAsync(uri, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return ProductScraperHelpers.NormalizeResults(
                ProductScraperHelpers.ExtractFromShajgojSearch(document.RootElement, SourceName),
                query,
                24);
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to parse Shajgoj response.");
            return [];
        }
    }
}
