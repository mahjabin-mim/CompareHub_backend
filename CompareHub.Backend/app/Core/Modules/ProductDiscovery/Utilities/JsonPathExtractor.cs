using System.Globalization;
using System.Text.Json;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities;

public class JsonPathExtractor : IJsonPathExtractor
{
    public List<JsonElement> ExtractCollection(JsonElement root, string path)
    {
        var segments = SplitPath(path);
        var current = new List<JsonElement> { root };

        foreach (var segment in segments)
        {
            var isArraySegment = segment.EndsWith("[]", StringComparison.Ordinal);
            var key = isArraySegment ? segment[..^2] : segment;
            var next = new List<JsonElement>();

            foreach (var node in current)
            {
                if (node.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(node, key, out var property))
                {
                    continue;
                }

                if (isArraySegment)
                {
                    if (property.ValueKind == JsonValueKind.Array)
                    {
                        next.AddRange(property.EnumerateArray());
                    }
                }
                else
                {
                    next.Add(property);
                }
            }

            current = next;
            if (current.Count == 0)
            {
                break;
            }
        }

        return current;
    }

    public string? ExtractString(JsonElement node, string path)
    {
        var value = TraverseSingle(node, path);
        return value?.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public decimal? ExtractDecimal(JsonElement node, string path)
    {
        var text = ExtractString(node, path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var filtered = new string(text.Where(ch => char.IsDigit(ch) || ch is '.' or ',').ToArray()).Replace(",", string.Empty);
        return decimal.TryParse(filtered, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static JsonElement? TraverseSingle(JsonElement start, string path)
    {
        var segments = SplitPath(path).Where(x => !x.EndsWith("[]", StringComparison.Ordinal)).ToList();
        var current = start;

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement result)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                result = property.Value;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static List<string> SplitPath(string path)
        => path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
