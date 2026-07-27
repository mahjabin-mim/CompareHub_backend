using Microsoft.Playwright;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Models;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Strategies;

public class PlaywrightExtractionStrategy : IProductExtractionStrategy
{
    private readonly ILogger<PlaywrightExtractionStrategy> _logger;

    public string Name => nameof(PlaywrightExtractionStrategy);
    public int Order => 4;

    public PlaywrightExtractionStrategy(ILogger<PlaywrightExtractionStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<List<ProductResultDto>> ExtractAsync(ProductSourceContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync();
            await page.GotoAsync(context.SearchUrl, new PageGotoOptions
            {
                Timeout = 45000,
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.WaitForTimeoutAsync(1200);
            var html = await page.ContentAsync();
            context.Html = html;

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
}
