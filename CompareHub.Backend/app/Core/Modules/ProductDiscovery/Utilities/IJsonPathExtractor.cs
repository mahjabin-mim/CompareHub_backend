using System.Text.Json;

namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities;

public interface IJsonPathExtractor
{
    List<JsonElement> ExtractCollection(JsonElement root, string path);
    string? ExtractString(JsonElement node, string path);
    decimal? ExtractDecimal(JsonElement node, string path);
}
