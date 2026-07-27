using System.Globalization;
using System.Text.RegularExpressions;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

public static class ProductExtractionNormalizer
{
    public static List<ProductResultDto> Normalize(IEnumerable<ProductResultDto> products)
    {
        return products
            .Where(IsValid)
            .GroupBy(x => x.ProductUrl.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.Price).First())
            .OrderBy(x => x.Price)
            .ToList();
    }

    public static bool IsValid(ProductResultDto product)
    {
        return !string.IsNullOrWhiteSpace(product.ProductName)
               && product.Price > 0
               && !string.IsNullOrWhiteSpace(product.ProductUrl);
    }

    public static decimal ParsePrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var normalized = Regex.Replace(raw, "[^0-9.,]", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        normalized = normalized.Replace(",", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : 0;
    }

    public static string MakeAbsoluteUrl(string baseUrl, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return string.Empty;
        }

        if (value.StartsWith("/"))
        {
            var root = new Uri(baseUri.GetLeftPart(UriPartial.Authority));
            return new Uri(root, value).ToString();
        }

        return new Uri(baseUri, value.TrimStart('/')).ToString();
    }

    public static string CleanText(string value)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(value ?? string.Empty);
        var withoutTags = Regex.Replace(decoded, "<.*?>", string.Empty);
        return Regex.Replace(withoutTags, "\\s+", " ").Trim();
    }
}
