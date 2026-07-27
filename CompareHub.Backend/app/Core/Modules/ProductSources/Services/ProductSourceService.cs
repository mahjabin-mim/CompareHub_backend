using System.Text.Json;
using FluentValidation;
using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Infrastructure.Auth;
using CompareHub.Backend.app.Core.Infrastructure.Services;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductSources.DTOs;
using CompareHub.Backend.app.Core.Modules.ProductSources.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductSources.Specifications;
using CompareHub.Backend.app.Core.Shared.Contracts;

namespace CompareHub.Backend.app.Core.Modules.ProductSources.Services;

public class ProductSourceService : IProductSourceService
{
    private readonly IRepository<UserProductSource> _sources;
    private readonly ICurrentUserService _currentUser;
    private readonly IApiKeyProtector _apiKeyProtector;
    private readonly IProductSourceConnector _productSourceConnector;

    public ProductSourceService(
        IRepository<UserProductSource> sources,
        ICurrentUserService currentUser,
        IApiKeyProtector apiKeyProtector,
        IProductSourceConnector productSourceConnector)
    {
        _sources = sources;
        _currentUser = currentUser;
        _apiKeyProtector = apiKeyProtector;
        _productSourceConnector = productSourceConnector;
    }

    public async Task<List<ProductSourceDto>> GetCurrentUserSourcesAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var entities = await _sources.ListAsync(new ActiveProductSourcesByUserSpecification(userId), cancellationToken);
        return entities.OrderByDescending(x => x.CreatedAt).Select(MapToDto).ToList();
    }

    public async Task<ProductSourceDto> CreateAsync(UpsertProductSourceRequestDto request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var entity = new UserProductSource
        {
            UserId = _currentUser.GetUserId(),
            SourceName = request.SourceName.Trim(),
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "API" : request.SourceType.Trim(),
            BaseUrl = request.BaseUrl.Trim(),
            SearchEndpoint = request.SearchEndpoint.Trim(),
            QueryParamName = request.QueryParamName.Trim(),
            HttpMethod = request.HttpMethod.Trim().ToUpperInvariant(),
            ApiKeyEncrypted = string.IsNullOrWhiteSpace(request.ApiKey) ? string.Empty : _apiKeyProtector.Protect(request.ApiKey.Trim()),
            HeadersJson = NormalizeHeadersJson(request.HeadersJson),
            NamePath = request.NamePath.Trim(),
            PricePath = request.PricePath.Trim(),
            ImagePath = request.ImagePath.Trim(),
            ProductUrlPath = request.ProductUrlPath.Trim(),
            IsActive = request.IsActive
        };

        await _sources.AddAsync(entity, cancellationToken);
        await _sources.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<ProductSourceDto> UpdateAsync(Guid id, UpsertProductSourceRequestDto request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var entity = await _sources.FirstOrDefaultAsync(new ProductSourceByIdAndUserSpecification(id, _currentUser.GetUserId()), cancellationToken)
            ?? throw new ValidationException("Product source not found.");

        entity.SourceName = request.SourceName.Trim();
        entity.SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "API" : request.SourceType.Trim();
        entity.BaseUrl = request.BaseUrl.Trim();
        entity.SearchEndpoint = request.SearchEndpoint.Trim();
        entity.QueryParamName = request.QueryParamName.Trim();
        entity.HttpMethod = request.HttpMethod.Trim().ToUpperInvariant();
        entity.HeadersJson = NormalizeHeadersJson(request.HeadersJson);
        entity.NamePath = request.NamePath.Trim();
        entity.PricePath = request.PricePath.Trim();
        entity.ImagePath = request.ImagePath.Trim();
        entity.ProductUrlPath = request.ProductUrlPath.Trim();
        entity.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            entity.ApiKeyEncrypted = _apiKeyProtector.Protect(request.ApiKey.Trim());
        }

        await _sources.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _sources.FirstOrDefaultAsync(new ProductSourceByIdAndUserSpecification(id, _currentUser.GetUserId()), cancellationToken)
            ?? throw new ValidationException("Product source not found.");
        await _sources.DeleteAsync(entity, cancellationToken);
        await _sources.SaveChangesAsync(cancellationToken);
    }

    public async Task<TestProductSourceConnectionResponseDto> TestConnectionAsync(TestProductSourceConnectionRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TestQuery))
        {
            throw new ValidationException("TestQuery is required.");
        }

        var source = new UserProductSource
        {
            UserId = _currentUser.GetUserId(),
            SourceName = "Test Source",
            SourceType = "API",
            BaseUrl = request.BaseUrl.Trim(),
            SearchEndpoint = request.SearchEndpoint.Trim(),
            QueryParamName = request.QueryParamName.Trim(),
            HttpMethod = request.HttpMethod.Trim().ToUpperInvariant(),
            ApiKeyEncrypted = string.IsNullOrWhiteSpace(request.ApiKey) ? string.Empty : _apiKeyProtector.Protect(request.ApiKey.Trim()),
            HeadersJson = NormalizeHeadersJson(request.HeadersJson),
            NamePath = request.NamePath.Trim(),
            PricePath = request.PricePath.Trim(),
            ImagePath = request.ImagePath.Trim(),
            ProductUrlPath = request.ProductUrlPath.Trim(),
            IsActive = true
        };

        var results = await _productSourceConnector.SearchAsync(source, request.TestQuery.Trim(), cancellationToken);
        return new TestProductSourceConnectionResponseDto(true, "Connection successful.", results.Count);
    }

    private static ProductSourceDto MapToDto(UserProductSource entity)
        => new(
            entity.Id,
            entity.SourceName,
            entity.SourceType,
            entity.BaseUrl,
            entity.SearchEndpoint,
            entity.QueryParamName,
            entity.HttpMethod,
            entity.HeadersJson,
            entity.NamePath,
            entity.PricePath,
            entity.ImagePath,
            entity.ProductUrlPath,
            entity.IsActive,
            entity.CreatedAt);

    private static void ValidateRequest(UpsertProductSourceRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceName))
        {
            throw new ValidationException("SourceName is required.");
        }

        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ValidationException("BaseUrl must be a valid absolute URL.");
        }

        if (!string.Equals(request.SourceType?.Trim(), "API", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("SourceType must be API.");
        }

        if (string.IsNullOrWhiteSpace(request.SearchEndpoint))
        {
            throw new ValidationException("SearchEndpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(request.QueryParamName))
        {
            throw new ValidationException("QueryParamName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.NamePath) || string.IsNullOrWhiteSpace(request.PricePath))
        {
            throw new ValidationException("NamePath and PricePath are required.");
        }

        if (string.IsNullOrWhiteSpace(request.ImagePath) || string.IsNullOrWhiteSpace(request.ProductUrlPath))
        {
            throw new ValidationException("ImagePath and ProductUrlPath are required.");
        }
    }

    private static string NormalizeHeadersJson(string headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(headersJson);
            return document.RootElement.ValueKind == JsonValueKind.Object ? headersJson : "{}";
        }
        catch
        {
            return "{}";
        }
    }
}
