using System.Text.Json;
using System.Net.Http.Json;
using CompareHub.Backend.app.Core.Infrastructure.Services;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Strategies;

public class ApiExtractionStrategy : IProductExtractionStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJsonPathExtractor _jsonPathExtractor;
    private readonly IApiKeyProtector _apiKeyProtector;
    private readonly ILogger<ApiExtractionStrategy> _logger;

    public string Name => nameof(ApiExtractionStrategy);
    public int Order => 1;

    public ApiExtractionStrategy(
        IHttpClientFactory httpClientFactory,
        IJsonPathExtractor jsonPathExtractor,
        IApiKeyProtector apiKeyProtector,
        ILogger<ApiExtractionStrategy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _jsonPathExtractor = jsonPathExtractor;
        _apiKeyProtector = apiKeyProtector;
        _logger = logger;
    }

    public async Task<List<ProductResultDto>> ExtractAsync(ProductSourceContext context, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ProductExtractionClient");
            using var request = new HttpRequestMessage(ParseMethod(context.Source.HttpMethod), context.SearchUrl);
            AddHeaders(request, context.Source.HeadersJson, context.Source.ApiKeyEncrypted);

            if (request.Method == HttpMethod.Post)
            {
                request.Content = JsonContent.Create(new Dictionary<string, string>
                {
                    [context.Source.QueryParamName] = context.Query
                });
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            context.ResponseJson = raw;

            if (!response.IsSuccessStatusCode || !LooksLikeJson(raw))
            {
                return [];
            }

            using var document = JsonDocument.Parse(raw);
            return ExtractMappedProducts(context, document.RootElement);
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

    private List<ProductResultDto> ExtractMappedProducts(ProductSourceContext context, JsonElement root)
    {
        var collectionPath = ResolveCollectionPath(
            context.Source.NamePath,
            context.Source.PricePath,
            context.Source.ImagePath,
            context.Source.ProductUrlPath);
        if (string.IsNullOrWhiteSpace(collectionPath))
        {
            return [];
        }

        var items = _jsonPathExtractor.ExtractCollection(root, collectionPath);
        var namePath = RemovePrefix(context.Source.NamePath, collectionPath);
        var pricePath = RemovePrefix(context.Source.PricePath, collectionPath);
        var imagePath = RemovePrefix(context.Source.ImagePath, collectionPath);
        var urlPath = RemovePrefix(context.Source.ProductUrlPath, collectionPath);

        var products = new List<ProductResultDto>();
        foreach (var item in items)
        {
            var name = _jsonPathExtractor.ExtractString(item, namePath) ?? string.Empty;
            var price = _jsonPathExtractor.ExtractDecimal(item, pricePath) ?? 0;
            var image = _jsonPathExtractor.ExtractString(item, imagePath) ?? string.Empty;
            var url = _jsonPathExtractor.ExtractString(item, urlPath) ?? string.Empty;

            products.Add(new ProductResultDto(
                ProductExtractionNormalizer.CleanText(name),
                price,
                ProductExtractionNormalizer.MakeAbsoluteUrl(context.BaseUrl, url),
                ProductExtractionNormalizer.MakeAbsoluteUrl(context.BaseUrl, image),
                context.SourceName));
        }

        return ProductExtractionNormalizer.Normalize(products);
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string? ResolveCollectionPath(params string[] paths)
    {
        foreach (var path in paths)
        {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var collected = new List<string>();
            foreach (var segment in segments)
            {
                collected.Add(segment);
                if (segment.EndsWith("[]", StringComparison.Ordinal))
                {
                    return string.Join('.', collected);
                }
            }
        }

        return null;
    }

    private static string RemovePrefix(string path, string prefix)
    {
        var candidate = path.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
            ? path[(prefix.Length + 1)..]
            : path;

        return candidate.Replace("[]", string.Empty, StringComparison.Ordinal);
    }

    private static HttpMethod ParseMethod(string method)
        => method.Equals("POST", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Get;

    private void AddHeaders(HttpRequestMessage request, string headersJson, string encryptedApiKey)
    {
        if (!string.IsNullOrWhiteSpace(headersJson) && headersJson.TrimStart().StartsWith('{'))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [];
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            catch
            {
                // Ignore malformed headers input; later strategies still run.
            }
        }

        // Existing model stores encrypted api key; keeping behavior unchanged for now.
        if (!string.IsNullOrWhiteSpace(encryptedApiKey))
        {
            var key = _apiKeyProtector.Unprotect(encryptedApiKey);
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", key);
            }
        }

        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 CompareHub/1.0");
    }
}
