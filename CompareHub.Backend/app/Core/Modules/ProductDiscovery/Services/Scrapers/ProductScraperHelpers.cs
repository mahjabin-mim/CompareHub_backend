using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Scrapers;

public static class ProductScraperHelpers
{
    private const decimal MinimumLikelyProductPrice = 50m;
    private static readonly string[] PriceClassHints = ["price", "amount", "sale", "money"];
    private static readonly string[] ProductClassHints = ["product", "card", "item", "grid", "collection"];
    private static readonly string[] BadUrlParts = ["cart", "wishlist", "account", "login", "checkout", "add-to-cart", "compare"];
    private static readonly string[] BadImageParts = ["data:image", "lazy.svg", "placeholder", "spacer", "logo", "favicon"];
    private static readonly Dictionary<string, string[]> ProductTypeSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["serum"] = ["serum", "ampoule"],
        ["cream"] = ["cream", "moisturizer", "moisturiser", "lotion", "gel"],
        ["toner"] = ["toner", "mist"],
        ["cleanser"] = ["cleanser", "wash", "foam"],
        ["sunscreen"] = ["sunscreen", "suncream", "sunblock", "spf"],
        ["mask"] = ["mask", "pack"],
        ["eye"] = ["eye"]
    };

    public static List<ProductResultDto> NormalizeResults(IEnumerable<ProductResultDto> results, string query, int maxResults)
    {
        var tokens = BuildQueryTokens(query);
        var scored = results
            .Select((product, index) => new { Product = product, Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Product.ProductName))
            .Where(x => !string.IsNullOrWhiteSpace(x.Product.ProductUrl))
            .Where(x => x.Product.Price >= MinimumLikelyProductPrice)
            .Where(x => !LooksLikeUtilityUrl(x.Product.ProductUrl))
            .Where(x => !LooksLikeNonProductTitle(x.Product.ProductName))
            .Select(x => new
            {
                Product = x.Product with
                {
                    ProductName = CleanText(x.Product.ProductName),
                    ProductUrl = WebUtility.HtmlDecode(x.Product.ProductUrl),
                    ImageUrl = WebUtility.HtmlDecode(x.Product.ImageUrl ?? string.Empty),
                    SourceWebsite = CleanText(x.Product.SourceWebsite)
                },
                Relevance = CalculateRelevanceScore(x.Product.ProductName, tokens),
                x.Index
            })
            .ToList();

        var relevant = scored
            .Where(x => tokens.Count == 0 || x.Relevance > 0)
            .Where(x => HasRequiredTokenMatches(x.Product.ProductName, tokens))
            .Where(x => tokens.Count <= 1 || x.Relevance >= 0.6m)
            .ToList();

        var selected = relevant.Count > 0 ? relevant : scored;
        return selected
            .GroupBy(x => NormalizeUrlForGrouping(x.Product.ProductUrl), StringComparer.OrdinalIgnoreCase)
            .Select(x => x
                .OrderByDescending(product => product.Relevance)
                .ThenBy(product => product.Index)
                .First())
            .OrderByDescending(x => x.Relevance)
            .ThenBy(x => x.Product.Price)
            .Select(x => x.Product)
            .Take(maxResults)
            .ToList();
    }

    public static IEnumerable<ProductResultDto> ExtractFromWooStoreApi(JsonElement root, Uri baseUri, string sourceName)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var product in ExtractFromWooStoreApiItem(root, baseUri, sourceName))
            {
                yield return product;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in root.EnumerateArray())
        {
            foreach (var product in ExtractFromWooStoreApiItem(item, baseUri, sourceName))
            {
                yield return product;
            }
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromShopifySuggest(JsonElement root, Uri baseUri, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "resources", out var resources) ||
            !TryGetPropertyIgnoreCase(resources, "results", out var results) ||
            !TryGetPropertyIgnoreCase(results, "products", out var products) ||
            products.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in products.EnumerateArray())
        {
            var name = FirstNonEmpty(GetString(item, "title"), GetString(item, "name"));
            var url = FirstNonEmpty(GetString(item, "url"), GetString(item, "handle"));
            if (!string.IsNullOrWhiteSpace(url) && !url.Contains('/'))
            {
                url = $"/products/{url}";
            }

            var image = FirstNonEmpty(GetString(item, "image"), GetString(item, "featured_image"));
            var price = FirstPositive(
                ExtractPriceValue(item, "price"),
                ExtractPriceValue(item, "min_price"),
                ExtractPriceValue(item, "price_min"));

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(
                CleanText(name),
                NormalizeSuspiciousMinorUnitPrice(price),
                MakeAbsoluteUrl(baseUri, url),
                MakeAbsoluteUrl(baseUri, image),
                sourceName);
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromShajgojSearch(JsonElement root, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in hits.EnumerateArray())
        {
            var name = GetString(item, "name");
            var slug = GetString(item, "slug");
            var image = GetString(item, "thumbnail");
            var price = FirstPositive(ExtractPriceValue(item, "sale_price"), ExtractPriceValue(item, "price"));

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(
                CleanText(name),
                price,
                $"https://shop.shajgoj.com/product/{slug}",
                image,
                sourceName);
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromKoreanMartSearch(JsonElement root, Uri baseUri, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var origin = $"{baseUri.Scheme}://{baseUri.Host}";
        foreach (var item in data.EnumerateArray())
        {
            var name = GetString(item, "name");
            var slug = GetString(item, "slug");
            var image = GetString(item, "image");
            var price = FirstPositive(
                ExtractPriceValue(item, "discount_price"),
                ExtractPriceValue(item, "sale_price"),
                ExtractPriceValue(item, "price"));

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(
                CleanText(name),
                price,
                $"{origin}/product/{slug}",
                MakeAbsoluteUrl(baseUri, image),
                sourceName);
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromAroggaSearch(JsonElement root, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in data.EnumerateArray())
        {
            var name = GetString(item, "p_name");
            var productId = FirstNonEmpty(GetString(item, "p_id"), GetString(item, "id"));
            var selectedVariant = SelectAroggaVariant(item, out var price);

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(productId) || price <= 0)
            {
                continue;
            }

            var variantId = selectedVariant.HasValue
                ? FirstNonEmpty(GetString(selectedVariant.Value, "pv_id"), GetString(item, "pv_id"))
                : GetString(item, "pv_id");

            var image = FirstNonEmpty(
                GetString(item, "POSTER"),
                selectedVariant.HasValue ? ExtractFirstImage(selectedVariant.Value, "attachedFiles_pv_images") : string.Empty,
                ExtractFirstImage(item, "attachedFiles_p_images"),
                ExtractFirstImage(item, "p_images"));

            yield return new ProductResultDto(
                CleanText(name),
                price,
                BuildAroggaProductUrl(productId, BuildAroggaProductSlug(item), variantId),
                MakeAbsoluteUrl(new Uri("https://www.arogga.com/"), image),
                sourceName);
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromPerfectoSearch(JsonElement root, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "data", out var data) ||
            !TryGetPropertyIgnoreCase(data, "products", out var products) ||
            !TryGetPropertyIgnoreCase(products, "data", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var imageBaseUri = new Uri("https://app.perfectobd.com/");
        foreach (var item in items.EnumerateArray())
        {
            var name = GetString(item, "name");
            var slug = GetString(item, "slug");
            var image = FirstNonEmpty(GetString(item, "image"), ExtractPerfectoSizeImage(item));
            var price = ExtractPerfectoPrice(item);

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(
                CleanText(name),
                price,
                $"https://perfectobd.com/product/{Uri.EscapeDataString(slug)}",
                MakeAbsoluteUrl(imageBaseUri, image),
                sourceName);
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromKireiSearch(JsonElement root, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var baseUri = new Uri("https://kireibd.com/");
        foreach (var item in data.EnumerateArray())
        {
            var name = GetString(item, "name");
            var image = FirstNonEmpty(GetString(item, "thumbnail_image"), GetString(item, "image"));
            var price = FirstPositive(
                ExtractPriceValue(item, "main_price", "stroked_price", "base_price", "price"),
                ExtractPriceValue(item, "unit_price"));

            var linksWeb = string.Empty;
            var linksDetails = string.Empty;
            if (TryGetPropertyIgnoreCase(item, "links", out var links))
            {
                linksWeb = GetString(links, "web");
                linksDetails = GetString(links, "details");
            }

            var url = FirstNonEmpty(
                GetString(item, "web_url"),
                linksWeb,
                linksDetails);

            if (string.IsNullOrWhiteSpace(url))
            {
                var slug = FirstNonEmpty(GetString(item, "slug"), GetString(item, "product_slug"));
                var productId = FirstNonEmpty(GetString(item, "id"), GetString(item, "product_id"));
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    url = $"/products/{slug}";
                }
                else if (!string.IsNullOrWhiteSpace(productId))
                {
                    url = $"/products/{productId}";
                }
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(
                CleanText(name),
                price,
                MakeAbsoluteUrl(baseUri, url),
                MakeAbsoluteUrl(baseUri, image),
                sourceName);
        }
    }

    public static IEnumerable<ProductResultDto> ExtractFromKireiGigalogySearch(JsonElement root, string sourceName)
    {
        if (!TryGetPropertyIgnoreCase(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var baseUri = new Uri("https://kireibd.com/");
        foreach (var item in data.EnumerateArray())
        {
            var name = GetString(item, "name");
            var slug = GetString(item, "slug");
            var image = FirstNonEmpty(
                GetString(item, "thumbnail_image"),
                GetString(item, "image"),
                ExtractFirstObjectImage(item, "pictures"),
                ExtractFirstObjectImage(item, "small_pictures"),
                ExtractFirstObjectImage(item, "large_pictures"));
            var price = FirstPositive(
                ExtractPriceValue(item, "sale_price", "main_price", "price", "stroked_price"),
                ExtractPriceValue(item, "base_price"));

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(
                CleanText(name),
                price,
                $"https://kireibd.com/product/{Uri.EscapeDataString(slug)}",
                MakeAbsoluteUrl(baseUri, image),
                sourceName);
        }
    }

    private static string ExtractFirstObjectImage(JsonElement item, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(item, propertyName, out var images) || images.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var image in images.EnumerateArray())
        {
            var imageUrl = FirstNonEmpty(GetString(image, "url"), GetString(image, "src"), GetString(image, "image"));
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                return imageUrl;
            }
        }

        return string.Empty;
    }

    public static List<ProductResultDto> ExtractFromHtml(string html, Uri pageUri, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var products = new List<ProductResultDto>();
        products.AddRange(ExtractFromJsonLd(html, pageUri, sourceName));
        products.AddRange(ExtractFromWooCommerceTrackingData(html, pageUri, sourceName));
        products.AddRange(ExtractFromProductCardHtml(html, pageUri, sourceName));
        return NormalizeResults(products, string.Empty, 80);
    }

    public static string CleanText(string value)
    {
        return WebUtility.HtmlDecode(Regex.Replace(value ?? string.Empty, "\\s+", " ")).Trim();
    }

    public static decimal ParsePrice(string? value)
    {
        var text = CleanText(value ?? string.Empty)
            .Replace("৳", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("Tk.", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("Tk", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("BDT", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("TK", " ", StringComparison.OrdinalIgnoreCase);

        var matches = Regex.Matches(text, @"(?<![A-Za-z0-9])(?<price>[0-9][0-9,]*(?:\.[0-9]{1,2})?)(?![A-Za-z0-9])");
        var values = matches
            .Select(match => decimal.TryParse(match.Groups["price"].Value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : 0)
            .Where(price => price >= 10)
            .ToList();

        return values.Count == 0 ? 0 : values.Min();
    }

    public static string MakeAbsoluteUrl(Uri baseUri, string? value)
    {
        value = WebUtility.HtmlDecode(value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.StartsWith("//", StringComparison.OrdinalIgnoreCase))
        {
            value = $"{baseUri.Scheme}:{value}";
        }

        return Uri.TryCreate(baseUri, value, out var uri) ? uri.ToString() : string.Empty;
    }

    public static string GetString(JsonElement element, string propertyName)
    {
        return TryGetPropertyIgnoreCase(element, propertyName, out var property) ? GetStringValue(property) : string.Empty;
    }

    public static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
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

    public static decimal ExtractPriceValue(JsonElement element, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var key in keys.Length == 0
                     ? ["price", "sale_price", "discount_price", "regular_price", "original_price", "current_price", "lowPrice", "highPrice"]
                     : keys)
        {
            if (TryGetPropertyIgnoreCase(element, key, out var value))
            {
                var parsed = value.ValueKind == JsonValueKind.Number
                    ? ParsePrice(value.GetRawText())
                    : ParsePrice(value.GetString());

                if (parsed > 0)
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static IEnumerable<ProductResultDto> ExtractFromJsonLd(string html, Uri pageUri, string sourceName)
    {
        var scripts = Regex.Matches(
            html,
            "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match script in scripts)
        {
            var json = WebUtility.HtmlDecode(script.Groups["json"].Value.Trim());
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            JsonDocument? document = null;
            var extractedProducts = new List<ProductResultDto>();
            try
            {
                document = JsonDocument.Parse(json);
                extractedProducts = ExtractJsonLdProducts(document.RootElement, pageUri, sourceName).ToList();
            }
            catch
            {
                // Some themes emit invalid JSON-LD; HTML/card parsing still runs.
            }
            finally
            {
                document?.Dispose();
            }

            foreach (var product in extractedProducts)
            {
                yield return product;
            }
        }
    }

    private static IEnumerable<ProductResultDto> ExtractJsonLdProducts(JsonElement element, Uri pageUri, string sourceName)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                foreach (var product in ExtractJsonLdProducts(child, pageUri, sourceName))
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
            var image = ExtractJsonImage(element, pageUri);
            var price = ExtractJsonLdPrice(element);

            if (!string.IsNullOrWhiteSpace(name) && price > 0)
            {
                yield return new ProductResultDto(CleanText(name), price, MakeAbsoluteUrl(pageUri, url), image, sourceName);
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var product in ExtractJsonLdProducts(property.Value, pageUri, sourceName))
            {
                yield return product;
            }
        }
    }

    private static IEnumerable<ProductResultDto> ExtractFromWooCommerceTrackingData(string html, Uri pageUri, string sourceName)
    {
        var matches = Regex.Matches(
            html,
            "data-gtm4wp_product_data=(?<quote>[\"'])(?<json>.*?)\\k<quote>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var decodedJson = WebUtility.HtmlDecode(match.Groups["json"].Value);
            if (string.IsNullOrWhiteSpace(decodedJson))
            {
                continue;
            }

            JsonDocument? document = null;
            ProductResultDto? product = null;
            try
            {
                document = JsonDocument.Parse(decodedJson);
                var item = document.RootElement;
                var name = FirstNonEmpty(GetString(item, "item_name"), GetString(item, "name"));
                var url = FirstNonEmpty(GetString(item, "productlink"), GetString(item, "url"));
                var price = ExtractPriceValue(item, "price", "sale_price");
                var image = ExtractNearbyImage(html, match.Index, pageUri);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url) && price > 0)
                {
                    product = new ProductResultDto(CleanText(name), price, MakeAbsoluteUrl(pageUri, url), image, sourceName);
                }
            }
            catch
            {
                // Invalid embedded metadata should not block the whole source.
            }
            finally
            {
                document?.Dispose();
            }

            if (product is not null)
            {
                yield return product;
            }
        }
    }

    private static IEnumerable<ProductResultDto> ExtractFromProductCardHtml(string html, Uri pageUri, string sourceName)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var nodes = document.DocumentNode
            .Descendants()
            .Where(node => node.NodeType == HtmlNodeType.Element)
            .Where(node => HasProductClassHint(node) || HasPriceChild(node))
            .Where(node => node.Descendants("a").Any(anchor => !string.IsNullOrWhiteSpace(anchor.GetAttributeValue("href", string.Empty))))
            .Where(node => node.InnerText.Length is > 20 and < 5000)
            .Take(250)
            .ToList();

        foreach (var node in nodes)
        {
            var link = SelectProductLink(node, pageUri);
            if (string.IsNullOrWhiteSpace(link) || LooksLikeUtilityUrl(link) || !LooksLikeProductUrl(link))
            {
                continue;
            }

            var name = ExtractName(node, link);
            var price = ExtractNodePrice(node);
            var image = ExtractNodeImage(node, pageUri);

            if (string.IsNullOrWhiteSpace(name) || price <= 0)
            {
                continue;
            }

            yield return new ProductResultDto(CleanText(name), price, link, image, sourceName);
        }
    }

    private static string ExtractWooImage(JsonElement item, Uri baseUri)
    {
        if (TryGetPropertyIgnoreCase(item, "images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            var first = images.EnumerateArray().FirstOrDefault();
            return MakeAbsoluteUrl(baseUri, FirstNonEmpty(GetString(first, "src"), GetString(first, "thumbnail")));
        }

        return MakeAbsoluteUrl(baseUri, FirstNonEmpty(GetString(item, "image"), GetString(item, "thumbnail")));
    }

    private static IEnumerable<ProductResultDto> ExtractFromWooStoreApiItem(JsonElement item, Uri baseUri, string sourceName)
    {
        var name = GetString(item, "name");
        var url = FirstNonEmpty(GetString(item, "permalink"), GetString(item, "link"));
        var image = ExtractWooImage(item, baseUri);
        var price = ExtractWooPrice(item);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || price <= 0)
        {
            yield break;
        }

        yield return new ProductResultDto(
            CleanText(name),
            price,
            MakeAbsoluteUrl(baseUri, url),
            image,
            sourceName);
    }

    private static decimal ExtractWooPrice(JsonElement item)
    {
        if (TryGetPropertyIgnoreCase(item, "prices", out var pricesObj))
        {
            var price = FirstPositive(
                ExtractPriceValue(pricesObj, "sale_price"),
                ExtractPriceValue(pricesObj, "price"),
                ExtractPriceValue(pricesObj, "regular_price"));

            if (price > 0 &&
                TryGetPropertyIgnoreCase(pricesObj, "currency_minor_unit", out var minorUnitObj) &&
                minorUnitObj.ValueKind == JsonValueKind.Number &&
                minorUnitObj.TryGetInt32(out var minorUnit) &&
                minorUnit > 0)
            {
                price /= (decimal)Math.Pow(10, minorUnit);
            }

            return price;
        }

        return ExtractPriceValue(item);
    }

    private static JsonElement? SelectAroggaVariant(JsonElement item, out decimal price)
    {
        price = 0;
        if (!TryGetPropertyIgnoreCase(item, "pv", out var variants) || variants.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var variant in variants.EnumerateArray())
        {
            var variantPrice = FirstPositive(
                ExtractPriceValue(variant, "pv_b2c_discounted_price"),
                ExtractPriceValue(variant, "pv_b2c_price"),
                ExtractPriceValue(variant, "pv_mrp"),
                ExtractPriceValue(variant, "pv_b2b_discounted_price"),
                ExtractPriceValue(variant, "pv_b2b_price"));

            if (variantPrice <= 0)
            {
                continue;
            }

            price = variantPrice;
            return variant;
        }

        return null;
    }

    private static string BuildAroggaProductUrl(string productId, string slug, string variantId)
    {
        var url = $"https://www.arogga.com/product/{Uri.EscapeDataString(productId)}/{slug}";
        return string.IsNullOrWhiteSpace(variantId) ? url : $"{url}?pv_id={Uri.EscapeDataString(variantId)}";
    }

    private static string BuildAroggaProductSlug(JsonElement item)
    {
        var rawSlug = CleanText($"{GetString(item, "p_name")} {GetString(item, "p_form")} {GetString(item, "p_strength")}");
        var slug = Regex.Replace(rawSlug.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        var parts = slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Aggregate(new List<string>(), (result, part) =>
            {
                if (result.Count == 0 || !result[^1].Equals(part, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(part);
                }

                return result;
            });

        return parts.Count == 0 ? "product" : string.Join('-', parts);
    }

    private static string ExtractFirstImage(JsonElement item, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(item, propertyName, out var images))
        {
            return string.Empty;
        }

        if (images.ValueKind == JsonValueKind.Array)
        {
            return images
                .EnumerateArray()
                .Select(GetStringValue)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        }

        return GetStringValue(images);
    }

    private static decimal ExtractPerfectoPrice(JsonElement item)
    {
        if (!TryGetPropertyIgnoreCase(item, "product_sizes", out var sizes) || sizes.ValueKind != JsonValueKind.Array)
        {
            return ExtractPriceValue(item);
        }

        return sizes
            .EnumerateArray()
            .Select(size => FirstPositive(
                ExtractPriceValue(size, "offer_discounted_price"),
                ExtractPriceValue(size, "discounted_price"),
                ExtractPriceValue(size, "mobile_discounted_price"),
                ExtractPriceValue(size, "size_price"),
                ExtractPriceValue(size, "mobile_size_price")))
            .FirstOrDefault(price => price > 0);
    }

    private static string ExtractPerfectoSizeImage(JsonElement item)
    {
        if (!TryGetPropertyIgnoreCase(item, "product_sizes", out var sizes) || sizes.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var size in sizes.EnumerateArray())
        {
            if (!TryGetPropertyIgnoreCase(size, "product_size_images", out var images) || images.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var image in images.EnumerateArray())
            {
                var imageUrl = GetString(image, "product_size_image");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    return imageUrl;
                }
            }
        }

        return string.Empty;
    }

    private static string ExtractJsonImage(JsonElement element, Uri pageUri)
    {
        if (!TryGetPropertyIgnoreCase(element, "image", out var image))
        {
            return string.Empty;
        }

        var imageUrl = image.ValueKind switch
        {
            JsonValueKind.String => image.GetString(),
            JsonValueKind.Array => image.EnumerateArray().Select(GetStringValue).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            JsonValueKind.Object => FirstNonEmpty(GetString(image, "url"), GetString(image, "contentUrl")),
            _ => string.Empty
        };

        return MakeAbsoluteUrl(pageUri, imageUrl);
    }

    private static decimal ExtractJsonLdPrice(JsonElement element)
    {
        if (TryGetPropertyIgnoreCase(element, "offers", out var offers))
        {
            if (offers.ValueKind == JsonValueKind.Array)
            {
                return offers.EnumerateArray().Select(offer => ExtractPriceValue(offer)).FirstOrDefault(price => price > 0);
            }

            return ExtractPriceValue(offers);
        }

        return ExtractPriceValue(element);
    }

    private static string ExtractNearbyImage(string html, int matchIndex, Uri pageUri)
    {
        var start = Math.Max(0, matchIndex - 9000);
        var section = html.Substring(start, matchIndex - start);
        var imageMatches = Regex.Matches(
            section,
            "(?:data-src|data-lazy-src|data-original|src|data-srcset|srcset)=[\"'](?<url>[^\"']+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match image in imageMatches.Cast<Match>().Reverse())
        {
            var url = FirstSrcSetUrl(WebUtility.HtmlDecode(image.Groups["url"].Value));
            if (IsUsableImageUrl(url))
            {
                return MakeAbsoluteUrl(pageUri, url);
            }
        }

        return string.Empty;
    }

    private static bool HasProductClassHint(HtmlNode node)
    {
        var className = node.GetAttributeValue("class", string.Empty).ToLowerInvariant();
        return ProductClassHints.Any(className.Contains);
    }

    private static bool HasPriceChild(HtmlNode node)
    {
        return node.Descendants().Any(child =>
            PriceClassHints.Any(hint => child.GetAttributeValue("class", string.Empty).Contains(hint, StringComparison.OrdinalIgnoreCase)));
    }

    private static string SelectProductLink(HtmlNode node, Uri pageUri)
    {
        var anchors = node.Descendants("a")
            .Select(anchor => anchor.GetAttributeValue("href", string.Empty))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(href => MakeAbsoluteUrl(pageUri, href))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Where(href => !LooksLikeUtilityUrl(href))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return anchors.FirstOrDefault(href =>
                   href.Contains("/product/", StringComparison.OrdinalIgnoreCase) ||
                   href.Contains("/products/", StringComparison.OrdinalIgnoreCase))
               ?? anchors.FirstOrDefault() ?? string.Empty;
    }

    private static string ExtractName(HtmlNode node, string productUrl)
    {
        var titleNode = node.SelectSingleNode(".//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'title')]")
            ?? node.SelectSingleNode(".//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'name')]")
            ?? node.SelectSingleNode(".//h1|.//h2|.//h3|.//h4");

        var title = CleanText(titleNode?.InnerText ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var productAnchor = node.Descendants("a")
            .FirstOrDefault(anchor => MakeAbsoluteUrl(new Uri(productUrl), anchor.GetAttributeValue("href", string.Empty)).Equals(productUrl, StringComparison.OrdinalIgnoreCase));

        title = FirstNonEmpty(
            productAnchor?.GetAttributeValue("aria-label", string.Empty),
            productAnchor?.GetAttributeValue("title", string.Empty),
            CleanText(productAnchor?.InnerText ?? string.Empty));

        return title;
    }

    private static decimal ExtractNodePrice(HtmlNode node)
    {
        var priceText = string.Join(" ", node.Descendants()
            .Where(child => PriceClassHints.Any(hint => child.GetAttributeValue("class", string.Empty).Contains(hint, StringComparison.OrdinalIgnoreCase)))
            .Select(child => child.InnerText));

        var price = ParseCurrencyPrice(priceText);
        if (price > 0)
        {
            return price;
        }

        return ParseCurrencyPrice(node.InnerText);
    }

    private static string ExtractNodeImage(HtmlNode node, Uri pageUri)
    {
        foreach (var image in node.Descendants("img"))
        {
            var candidates = new[]
            {
                image.GetAttributeValue("data-src", string.Empty),
                image.GetAttributeValue("data-lazy-src", string.Empty),
                image.GetAttributeValue("data-original", string.Empty),
                image.GetAttributeValue("data-srcset", string.Empty),
                image.GetAttributeValue("srcset", string.Empty),
                image.GetAttributeValue("src", string.Empty)
            };

            foreach (var candidate in candidates)
            {
                var url = FirstSrcSetUrl(candidate);
                if (IsUsableImageUrl(url))
                {
                    return MakeAbsoluteUrl(pageUri, url);
                }
            }
        }

        return string.Empty;
    }

    private static string GetStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Object => FirstNonEmpty(GetString(element, "url"), GetString(element, "src")),
            _ => string.Empty
        };
    }

    private static decimal FirstPositive(params decimal[] values)
    {
        return values.FirstOrDefault(value => value > 0);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static decimal NormalizeSuspiciousMinorUnitPrice(decimal price)
    {
        return price >= 10000 && price % 100 == 0 ? price / 100 : price;
    }

    private static string FirstSrcSetUrl(string? value)
    {
        value = WebUtility.HtmlDecode(value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty)
            .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? string.Empty;
    }

    private static bool IsUsableImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return !BadImageParts.Any(part => url.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCurrencyMarker(string value)
    {
        return value.Contains('৳') ||
               value.Contains("Tk", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("BDT", StringComparison.OrdinalIgnoreCase) ||
               value.Contains('$');
    }

    private static decimal ParseCurrencyPrice(string? value)
    {
        var text = CleanText(value ?? string.Empty);
        if (!HasCurrencyMarker(text))
        {
            return 0;
        }

        var matches = Regex.Matches(
            text,
            @"(?:৳|Tk\.?|BDT|\$)\s*(?<price>[0-9][0-9,]*(?:\.[0-9]{1,2})?)|(?<price>[0-9][0-9,]*(?:\.[0-9]{1,2})?)\s*(?:৳|Tk\.?|BDT|\$)",
            RegexOptions.IgnoreCase);
        var values = matches
            .Select(match => decimal.TryParse(match.Groups["price"].Value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : 0)
            .Where(price => price >= MinimumLikelyProductPrice)
            .ToList();

        return values.Count == 0 ? 0 : values.Min();
    }

    private static bool LooksLikeUtilityUrl(string url)
    {
        return BadUrlParts.Any(part => url.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeNonProductTitle(string title)
    {
        var normalized = CleanText(title).ToLowerInvariant();
        return normalized.Contains("search results for", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("search results", StringComparison.OrdinalIgnoreCase) ||
               (normalized.Contains("up to", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains("off", StringComparison.OrdinalIgnoreCase)) ||
               normalized.Contains("free shipping", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeProductUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return false;
        }

        var lowerPath = path.ToLowerInvariant();
        if (lowerPath.Contains("/product/", StringComparison.OrdinalIgnoreCase) ||
            lowerPath.Contains("/products/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (lowerPath.StartsWith("/search", StringComparison.OrdinalIgnoreCase) ||
            lowerPath.StartsWith("/shop", StringComparison.OrdinalIgnoreCase) ||
            lowerPath.StartsWith("/collections", StringComparison.OrdinalIgnoreCase) ||
            lowerPath.StartsWith("/category", StringComparison.OrdinalIgnoreCase) ||
            lowerPath.StartsWith("/brand", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Some stores (like Tekka) use root-level slug URLs instead of /product/{slug}.
        var trimmed = lowerPath.Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Contains('/'))
        {
            return false;
        }

        return trimmed.Length >= 8 && trimmed.Contains('-');
    }

    private static string NormalizeUrlForGrouping(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Trim();
        }

        return new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri.ToString().TrimEnd('/');
    }

    private static List<string> BuildQueryTokens(string query)
    {
        return BuildSearchTokens(query)
            .Distinct()
            .ToList();
    }

    private static decimal CalculateRelevanceScore(string productName, IReadOnlyCollection<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 1m;
        }

        var productTokens = BuildSearchTokens(productName);
        var matched = queryTokens.Count(token => IsTokenMatch(token, productTokens));
        return (decimal)matched / queryTokens.Count;
    }

    private static bool HasRequiredTokenMatches(string productName, IReadOnlyList<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return true;
        }

        var productTokens = BuildSearchTokens(productName);
        var firstQueryToken = queryTokens[0];
        if (queryTokens.Count > 1 && !IsTokenMatch(firstQueryToken, productTokens))
        {
            return false;
        }

        if (!queryTokens
            .Where(IsNumericToken)
            .All(token => productTokens.Contains(token, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        var queryProductTypes = ExtractProductTypes(queryTokens);
        if (queryProductTypes.Count == 0)
        {
            return true;
        }

        var productTypes = ExtractProductTypes(productTokens);
        return queryProductTypes.All(productTypes.Contains);
    }

    private static List<string> BuildSearchTokens(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ");
        normalized = Regex.Replace(normalized, @"(?<=[a-z])(?=\d)|(?<=\d)(?=[a-z])", " ");
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeQueryToken)
            .Where(x => x.Length >= 2)
            .ToList();
    }

    private static bool IsTokenMatch(string queryToken, IReadOnlyCollection<string> productTokens)
    {
        return IsNumericToken(queryToken)
            ? productTokens.Contains(queryToken, StringComparer.OrdinalIgnoreCase)
            : productTokens.Any(productToken => productToken.Contains(queryToken, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNumericToken(string token)
    {
        return Regex.IsMatch(token, @"^\d+$");
    }

    private static string NormalizeQueryToken(string token)
    {
        return token.StartsWith("niacin", StringComparison.OrdinalIgnoreCase) ? "niacin" : token;
    }

    private static HashSet<string> ExtractProductTypes(IEnumerable<string> tokens)
    {
        var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (type, synonyms) in ProductTypeSynonyms)
        {
            if (synonyms.Any(tokenSet.Contains))
            {
                types.Add(type);
            }
        }

        return types;
    }
}
