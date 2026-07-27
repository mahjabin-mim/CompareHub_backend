namespace CompareHub.Backend.app.Core.Modules.ProductDiscovery.DTOs;

public record ProductResultDto(
    string ProductName,
    decimal Price,
    string ProductUrl,
    string ImageUrl,
    string SourceWebsite);

public record ProductSourceCheckDto(
    string SourceWebsite,
    bool IsChecked,
    bool HasResults,
    int ResultCount);

public record ProductSearchResponseDto(
    string Query,
    List<ProductResultDto> Results,
    List<ProductSourceCheckDto>? SourceChecks = null);
