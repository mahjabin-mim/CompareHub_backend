using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Scrapers;
using CompareHub.Backend.app.Core.Shared.Common;

namespace CompareHub.Backend.app.Host.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private const string KoreanMartBaseUrl = "https://koreanmartbd.com/";
    private static readonly string[] PublicSourceUrls =
    [
        "https://kireibd.com/",
        KoreanMartBaseUrl,
        "https://shop.shajgoj.com/",
        "https://groomlybd.com/",
        "https://skincareshop.com.bd/",
        "https://beautybooth.com.bd/",
        "https://www.emartwayskincare.com.bd/",
        "https://tekka.com.bd/",
        "https://dhalishop.com/",
        "https://thealamsbd.com/",
        "https://www.thelilium.shop/",
        "https://klassy.com.bd/",
        "https://pixielabellabangladesh.com/",
        "https://themartbangladesh.tumblr.com/",
        "https://www.skinnora.com/",
        "https://makeupchari.com/",
        "https://perfectobd.com/",
        "https://www.arogga.com/"
    ];

    private readonly IProductDiscoveryService _productDiscoveryService;
    private readonly IProductScraperService _productScraperService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductDiscoveryService productDiscoveryService,
        IProductScraperService productScraperService,
        ILogger<ProductsController> logger)
    {
        _productDiscoveryService = productDiscoveryService;
        _productScraperService = productScraperService;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        var result = await _productDiscoveryService.SearchProductsAsync(query, cancellationToken);
        return Ok(ApiResponse<ProductSearchResponseDto>.Ok(result));
    }

    [AllowAnonymous]
    [HttpGet("search/koreanmart")]
    public async Task<IActionResult> SearchKoreanMart([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(ApiResponse<string>.Fail("Search query is required."));
        }

        var results = await _productScraperService.SearchProductsAsync(KoreanMartBaseUrl, query.Trim(), cancellationToken);
        return Ok(ApiResponse<ProductSearchResponseDto>.Ok(new ProductSearchResponseDto(query.Trim(), results)));
    }

    [AllowAnonymous]
    [HttpGet("search/public")]
    public async Task<IActionResult> SearchPublicSources([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(ApiResponse<string>.Fail("Search query is required."));
        }

        var trimmedQuery = query.Trim();
        var searchTasks = PublicSourceUrls.Select(async sourceUrl =>
        {
            try
            {
                var sourceResults = await _productScraperService.SearchProductsAsync(sourceUrl, trimmedQuery, cancellationToken);
                return new PublicSourceSearchResult(sourceUrl, sourceResults);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Public source search failed for {SourceUrl}.", sourceUrl);
                return new PublicSourceSearchResult(sourceUrl, []);
            }
        });

        var sourceSearchResults = await Task.WhenAll(searchTasks);
        var results = ProductScraperHelpers.NormalizeResults(
            sourceSearchResults.SelectMany(x => x.Results),
            trimmedQuery,
            60);
        var sourceChecks = sourceSearchResults
            .Select(x => new ProductSourceCheckDto(
                GetSourceWebsiteName(x.SourceUrl),
                true,
                x.Results.Count > 0,
                x.Results.Count))
            .ToList();

        return Ok(ApiResponse<ProductSearchResponseDto>.Ok(new ProductSearchResponseDto(trimmedQuery, results, sourceChecks)));
    }

    private static string GetSourceWebsiteName(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return sourceUrl;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }

    private sealed record PublicSourceSearchResult(string SourceUrl, List<ProductResultDto> Results);
}
