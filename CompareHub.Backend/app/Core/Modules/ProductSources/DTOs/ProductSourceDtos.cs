namespace CompareHub.Backend.app.Core.Modules.ProductSources.DTOs;

public record ProductSourceDto(
    Guid Id,
    string SourceName,
    string SourceType,
    string BaseUrl,
    string SearchEndpoint,
    string QueryParamName,
    string HttpMethod,
    string HeadersJson,
    string NamePath,
    string PricePath,
    string ImagePath,
    string ProductUrlPath,
    bool IsActive,
    DateTime CreatedAt);

public record UpsertProductSourceRequestDto(
    string SourceName,
    string SourceType,
    string BaseUrl,
    string SearchEndpoint,
    string QueryParamName,
    string HttpMethod,
    string? ApiKey,
    string HeadersJson,
    string NamePath,
    string PricePath,
    string ImagePath,
    string ProductUrlPath,
    bool IsActive);

public record TestProductSourceConnectionRequestDto(
    string BaseUrl,
    string SearchEndpoint,
    string QueryParamName,
    string HttpMethod,
    string? ApiKey,
    string HeadersJson,
    string NamePath,
    string PricePath,
    string ImagePath,
    string ProductUrlPath,
    string TestQuery);

public record TestProductSourceConnectionResponseDto(bool Success, string Message, int ResultCount);
