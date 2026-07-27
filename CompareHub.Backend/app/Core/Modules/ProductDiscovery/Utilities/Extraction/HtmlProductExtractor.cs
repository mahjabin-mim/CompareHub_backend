using HtmlAgilityPack;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

public static class HtmlProductExtractor
{
    private static readonly string[] ContainerXPaths =
    [
        "//*[contains(@class,'product-card')]",
        "//*[contains(@class,'product-item')]",
        "//*[contains(@class,'grid-product')]",
        "//*[contains(@class,'product')]",
        "//*[contains(@class,'item')]"
    ];

    private static readonly string[] TitleXPaths =
    [
        ".//*[contains(@class,'product-title')]",
        ".//*[contains(@class,'product-name')]",
        ".//*[contains(@class,'title')]",
        ".//h2",
        ".//h3",
        ".//a[@title]"
    ];

    private static readonly string[] PriceXPaths =
    [
        ".//*[contains(@class,'current-price')]",
        ".//*[contains(@class,'sale-price')]",
        ".//*[contains(@class,'product-price')]",
        ".//*[contains(@class,'price')]"
    ];

    public static List<ProductResultDto> Extract(string html, string baseUrl, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var nodes = new List<HtmlNode>();
        foreach (var xpath in ContainerXPaths)
        {
            var found = document.DocumentNode.SelectNodes(xpath);
            if (found is not null)
            {
                nodes.AddRange(found);
            }
        }

        var products = new List<ProductResultDto>();
        foreach (var node in nodes.Distinct())
        {
            var name = ExtractFirstText(node, TitleXPaths);
            var priceText = ExtractFirstText(node, PriceXPaths);
            var price = ProductExtractionNormalizer.ParsePrice(priceText);

            var linkNode = node.SelectSingleNode(".//a[@href]");
            var imageNode = node.SelectSingleNode(".//img[@src or @data-src]");

            var url = ProductExtractionNormalizer.MakeAbsoluteUrl(baseUrl, linkNode?.GetAttributeValue("href", string.Empty));
            var image = ProductExtractionNormalizer.MakeAbsoluteUrl(baseUrl,
                imageNode?.GetAttributeValue("data-src", string.Empty) ?? imageNode?.GetAttributeValue("src", string.Empty));

            products.Add(new ProductResultDto(
                ProductExtractionNormalizer.CleanText(name),
                price,
                url,
                image,
                sourceName));
        }

        return ProductExtractionNormalizer.Normalize(products);
    }

    private static string ExtractFirstText(HtmlNode node, IEnumerable<string> xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var found = node.SelectSingleNode(xpath);
            var text = found?.InnerText ?? found?.GetAttributeValue("title", string.Empty);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }
}
