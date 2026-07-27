using System.Collections.Generic;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.Tests;

public class ProductExtractionNormalizerTests
{
    [Fact]
    public void ParsePrice_ReturnsZeroForInvalidStrings()
    {
        Assert.Equal(0m, ProductExtractionNormalizer.ParsePrice(null));
        Assert.Equal(0m, ProductExtractionNormalizer.ParsePrice(string.Empty));
        Assert.Equal(0m, ProductExtractionNormalizer.ParsePrice("free"));
    }

    [Fact]
    public void ParsePrice_ParsesNumericString()
    {
        Assert.Equal(199.99m, ProductExtractionNormalizer.ParsePrice("$199.99"));
        Assert.Equal(1000m, ProductExtractionNormalizer.ParsePrice("1,000"));
    }

    [Fact]
    public void MakeAbsoluteUrl_ReturnsAbsoluteUrlWhenInputIsAbsolute()
    {
        var result = ProductExtractionNormalizer.MakeAbsoluteUrl("https://example.com", "https://store.test/product");

        Assert.Equal("https://store.test/product", result);
    }

    [Fact]
    public void MakeAbsoluteUrl_CombinesRelativePath()
    {
        var result = ProductExtractionNormalizer.MakeAbsoluteUrl("https://example.com/base", "/product/1");

        Assert.Equal("https://example.com/product/1", result);
    }

    [Fact]
    public void Normalize_RemovesInvalidProductsAndKeepsLowestPriceDuplicate()
    {
        var products = new List<ProductResultDto>
        {
            new("A", 100m, "https://example.com/a", string.Empty, "site"),
            new(string.Empty, 50m, "https://example.com/b", string.Empty, "site"),
            new("A", 90m, "https://example.com/a", string.Empty, "site"),
            new("C", 0m, "https://example.com/c", string.Empty, "site")
        };

        var normalized = ProductExtractionNormalizer.Normalize(products);

        Assert.Single(normalized);
        Assert.Equal(90m, normalized[0].Price);
        Assert.Equal("https://example.com/a", normalized[0].ProductUrl);
    }
}
